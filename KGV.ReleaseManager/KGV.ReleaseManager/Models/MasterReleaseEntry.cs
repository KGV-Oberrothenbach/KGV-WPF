namespace KGV.ReleaseManager.Models;

public sealed record MasterReleaseEntry(
    string Version,
    string ReleaseDate,
    string Title,
    string ShortText,
    string FullText,
    string[]? Categories,
    string Status,
    PlatformReleaseEntry[] Platforms);

public sealed record PlatformReleaseEntry(
    string Platform,
    bool Enabled,
    string DistributionType,
    string Status,
    WindowsPlatformReleaseData? Windows,
    AndroidPlatformReleaseData? Android);

public sealed record WindowsPlatformReleaseData(
    string? DownloadUrl,
    string? FileName,
    long? FileSizeBytes,
    string? Sha256);

public sealed record AndroidPlatformReleaseData(
    string? PackageName,
    string? PlayTrack,
    string? PublishingStatus,
    string? StoreUrl,
    string? ReleaseName,
    int? VersionCode,
    string? AabArtifactPath);

public static class PlatformReleaseDefaults
{
    public static PlatformReleaseEntry CreateWindows(bool enabled, WindowsPlatformReleaseData? data = null, string status = "Entwurf")
        => new(
            Platform: "windows",
            Enabled: enabled,
            DistributionType: "DirectDownload",
            Status: status,
            Windows: data,
            Android: null);

    public static PlatformReleaseEntry CreateAndroidPlayStore(bool enabled, AndroidPlatformReleaseData? data = null, string status = "Entwurf")
        => new(
            Platform: "android",
            Enabled: enabled,
            DistributionType: "PlayStore",
            Status: status,
            Windows: null,
            Android: data);
}
