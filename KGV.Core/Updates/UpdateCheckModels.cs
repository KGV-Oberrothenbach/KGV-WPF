namespace KGV.Core.Updates;

public enum UpdateCheckStatus
{
    Checking = 0,
    NoUpdates = 1,
    UpdateAvailable = 2,
    NotAvailable = 3
}

public sealed record UpdatePromptInfo(
    string CurrentVersion,
    string? CurrentBuild,
    string OnlineVersion,
    string? OnlineBuild,
    string DownloadUrl,
    string? Notes);

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdatePromptInfo? Prompt,
    string? ErrorMessage);
