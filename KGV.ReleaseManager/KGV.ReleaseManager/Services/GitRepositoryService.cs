using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager.Services;

public sealed class GitRepositoryService
{
    private readonly ProcessRunner _processRunner = new();

    public async Task EnsureRepositoryAsync(string repoRoot, Action<string> log, CancellationToken cancellationToken = default)
    {
        var exit = await _processRunner.RunAsync("git", "rev-parse --is-inside-work-tree", repoRoot, log, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException("Der angegebene Ordner ist kein Git-Repository.");
        }
    }

    public async Task EnsureCleanWorkingTreeAsync(string repoRoot, Action<string> log, CancellationToken cancellationToken = default)
    {
        await EnsureRepositoryAsync(repoRoot, log, cancellationToken);

        var status = await _processRunner.RunCaptureAsync("git", "status --porcelain", repoRoot, _ => { }, cancellationToken);
        if (status.ExitCode != 0)
        {
            throw new InvalidOperationException("git status ist fehlgeschlagen.");
        }

        if (status.Output.Count != 0)
        {
            log("Git status --short:");
            foreach (var line in status.Output)
            {
                log(line);
            }

            throw new InvalidOperationException("Der Git-Ordner hat bereits lokale Änderungen. Bitte zuerst committen/pushen oder bereinigen.");
        }
    }

    public async Task ConfigureForKgvGitHubAsync(ReleaseContext context, Action<string> log, CancellationToken cancellationToken = default)
    {
        await EnsureRepositoryAsync(context.GitHubRoot, log, cancellationToken);

        await RunRequiredAsync(context.GitHubRoot, $"remote set-url origin \"{context.GitRemoteUrl}\"", "git remote set-url origin fehlgeschlagen.", log, cancellationToken);
        await RunRequiredAsync(context.GitHubRoot, $"config --local credential.username \"{context.GitCredentialUsername}\"", "git config credential.username fehlgeschlagen.", log, cancellationToken);
        await RunRequiredAsync(context.GitHubRoot, $"config --local user.name \"{context.GitUserName}\"", "git config user.name fehlgeschlagen.", log, cancellationToken);

        if (!string.IsNullOrWhiteSpace(context.GitUserEmail))
        {
            await RunRequiredAsync(context.GitHubRoot, $"config --local user.email \"{context.GitUserEmail}\"", "git config user.email fehlgeschlagen.", log, cancellationToken);
        }

        await RunRequiredAsync(context.GitHubRoot, "config --local pull.rebase true", "git config pull.rebase fehlgeschlagen.", log, cancellationToken);
    }

    public async Task PullRebaseAsync(string repoRoot, string remote, string branch, Action<string> log, CancellationToken cancellationToken = default)
    {
        await RunRequiredAsync(repoRoot, $"pull --rebase {remote} {branch}", $"git pull --rebase {remote} {branch} fehlgeschlagen.", log, cancellationToken);
    }

    public async Task CommitAndPushIfNeededAsync(string repoRoot, string commitMessage, string remote, string branch, Action<string> log, CancellationToken cancellationToken = default)
    {
        await EnsureRepositoryAsync(repoRoot, log, cancellationToken);

        await RunRequiredAsync(repoRoot, "add .", "git add fehlgeschlagen.", log, cancellationToken);

        var diffExit = await _processRunner.RunAsync("git", "diff --cached --quiet", repoRoot, _ => { }, cancellationToken);
        if (diffExit == 0)
        {
            log("Keine Git-Änderungen zum Committen gefunden.");
            return;
        }

        await RunRequiredAsync(repoRoot, $"commit -m \"{commitMessage}\"", "git commit fehlgeschlagen.", log, cancellationToken);

        var pushExit = await _processRunner.RunAsync("git", $"push --set-upstream {remote} {branch}", repoRoot, log, cancellationToken);
        if (pushExit == 0)
        {
            return;
        }

        log("Erster git push fehlgeschlagen. Versuche git pull --rebase und erneuten Push...");
        await PullRebaseAsync(repoRoot, remote, branch, log, cancellationToken);

        await RunRequiredAsync(repoRoot, $"push --set-upstream {remote} {branch}", "git push fehlgeschlagen.", log, cancellationToken);
    }

    private async Task RunRequiredAsync(string repoRoot, string gitArguments, string errorMessage, Action<string> log, CancellationToken cancellationToken)
    {
        var exit = await _processRunner.RunAsync("git", gitArguments, repoRoot, log, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
