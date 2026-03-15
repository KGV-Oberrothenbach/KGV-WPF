namespace KGV.ReleaseManager.Models;

public sealed record ReleaseStartRequest(
    bool IncludesWindows,
    bool IncludesAndroid,
    VersionInfo MasterVersion,
    WindowsBuildRequest? Windows,
    AndroidBuildRequest? Android);

public sealed record WindowsBuildRequest(
    VersionInfo Version);

public sealed record AndroidBuildRequest(
    VersionInfo Version,
    int VersionCode,
    AndroidPlatformReleaseData PlayStore);
