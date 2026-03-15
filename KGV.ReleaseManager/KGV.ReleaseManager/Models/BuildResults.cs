namespace KGV.ReleaseManager.Models;

public sealed record WindowsBuildResult(
    string DisplayVersion,
    string DownloadUrl,
    string CurrentInstallerFileName,
    string VersionedInstallerFileName);

public sealed record AndroidBuildResult(
    string DisplayVersion,
    int VersionCode,
    string? AabPath);
