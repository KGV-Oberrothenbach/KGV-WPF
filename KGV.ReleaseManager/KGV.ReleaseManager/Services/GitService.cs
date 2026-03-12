using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager.Services;

public sealed class GitService
{
    private readonly ProcessRunner _processRunner = new();

    public async Task EnsureRepositoryAsync(string repoRoot, Action<string> log)
    {
        var exit = await _processRunner.RunAsync("git", "rev-parse --is-inside-work-tree", repoRoot, log);
        if (exit != 0)
        {
            throw new InvalidOperationException("Der angegebene Ordner ist kein Git-Repository.");
        }
    }

    public async Task CommitAllAsync(
        string repoRoot,
        bool includesWpf,
        bool includesAndroid,
        VersionInfo? wpfVersion,
        VersionInfo? androidVersion,
        Action<string> log)
    {
        await EnsureRepositoryAsync(repoRoot, log);

        var addExit = await _processRunner.RunAsync("git", "add -A", repoRoot, log);
        if (addExit != 0)
        {
            throw new InvalidOperationException("git add -A ist fehlgeschlagen.");
        }

        var diffExit = await _processRunner.RunAsync("git", "diff --cached --quiet", repoRoot, log);
        if (diffExit == 0)
        {
            log("Keine Änderungen zum Committen gefunden.");
            return;
        }

        var message = BuildCommitMessage(includesWpf, includesAndroid, wpfVersion, androidVersion);
        var commitExit = await _processRunner.RunAsync("git", $"commit -m \"{message}\"", repoRoot, log);
        if (commitExit != 0)
        {
            throw new InvalidOperationException("git commit ist fehlgeschlagen.");
        }
    }

    private static string BuildCommitMessage(bool includesWpf, bool includesAndroid, VersionInfo? wpfVersion, VersionInfo? androidVersion)
    {
        if (includesWpf && includesAndroid && wpfVersion is not null && androidVersion is not null)
        {
            return $"Release WPF {wpfVersion.DisplayVersion} + Android {androidVersion.DisplayVersion}";
        }

        if (includesWpf && wpfVersion is not null)
        {
            return $"Release WPF {wpfVersion.DisplayVersion}";
        }

        if (includesAndroid && androidVersion is not null)
        {
            return $"Release Android {androidVersion.DisplayVersion}";
        }

        return "Release aktualisiert";
    }
}
