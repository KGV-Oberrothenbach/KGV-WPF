namespace KGV.ReleaseManager.Models;

public sealed record ReleaseContext(
    string RepoRoot,
    string PublishRoot,
    string GitHubRoot,
    string BaseUrl,
    string GitRemoteUrl,
    string GitCredentialUsername,
    string GitUserName,
    string? GitUserEmail,
    int KeepCount);
