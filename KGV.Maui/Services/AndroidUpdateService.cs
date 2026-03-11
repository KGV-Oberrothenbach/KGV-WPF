using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KGV.Maui.Services;

public interface IAndroidUpdateService
{
    Task<AndroidUpdatePromptInfo?> TryGetUpdatePromptAsync();
}

public sealed class AndroidUpdatePromptInfo
{
    public AndroidUpdatePromptInfo(string currentVersion, string currentBuild, string onlineVersion, string onlineBuild, string downloadUrl, string? notes)
    {
        CurrentVersion = currentVersion;
        CurrentBuild = currentBuild;
        OnlineVersion = onlineVersion;
        OnlineBuild = onlineBuild;
        DownloadUrl = downloadUrl;
        Notes = notes;
    }

    public string CurrentVersion { get; }
    public string CurrentBuild { get; }
    public string OnlineVersion { get; }
    public string OnlineBuild { get; }
    public string DownloadUrl { get; }
    public string? Notes { get; }

    public string BuildMessage()
    {
        var msg =
            $"Es ist eine neue Version verfügbar.\n\n" +
            $"Aktuell: {CurrentVersion} (Build {CurrentBuild})\n" +
            $"Neu: {OnlineVersion} (Build {OnlineBuild})";

        var notes = (Notes ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(notes))
            msg += "\n\nHinweise:\n" + notes;

        return msg;
    }
}

public sealed class AndroidUpdateService : IAndroidUpdateService
{
    // Central place to change the update endpoint.
    // GitHub Pages:
    //   https://<user>.github.io/<repo>/android/version.json
    public const string VersionJsonUrl = "https://abraeuer20-png.github.io/KGV/android/version.json";

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private readonly ILogger<AndroidUpdateService> _logger;

    private int _checkStarted;
    private int _promptShown;
    private Task? _checkTask;
    private AndroidUpdatePromptInfo? _update;

    public AndroidUpdateService(ILogger<AndroidUpdateService> logger)
    {
        _logger = logger;
    }

    public async Task<AndroidUpdatePromptInfo?> TryGetUpdatePromptAsync()
    {
        // Safety guard for future multi-targeting. (Project is currently Android-only.)
        if (DeviceInfo.Platform != DevicePlatform.Android)
            return null;

        await EnsureCheckedAsync().ConfigureAwait(false);

        if (_update == null)
            return null;

        if (Interlocked.CompareExchange(ref _promptShown, 1, 0) != 0)
            return null;

        return _update;
    }

    private Task EnsureCheckedAsync()
    {
        if (Interlocked.CompareExchange(ref _checkStarted, 1, 0) != 0)
            return _checkTask ?? Task.CompletedTask;

        _checkTask = CheckCoreAsync();
        return _checkTask;
    }

    private async Task CheckCoreAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(VersionJsonUrl).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var info = JsonSerializer.Deserialize<AndroidUpdateInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (info == null)
                return;

            if (!string.Equals((info.Platform ?? string.Empty).Trim(), "android", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(info.Version) || string.IsNullOrWhiteSpace(info.DownloadUrl))
                return;

            var localVersionText = (AppInfo.Current.VersionString ?? string.Empty).Trim();
            var localBuildText = (AppInfo.Current.BuildString ?? string.Empty).Trim();

            var onlineVersionText = info.Version.Trim();
            var onlineBuild = info.Build;

            var hasLocalVersion = Version.TryParse(localVersionText, out var localVersion);
            var hasOnlineVersion = Version.TryParse(onlineVersionText, out var onlineVersion);

            _ = int.TryParse(localBuildText, out var localBuild);

            var isNewer = false;

            if (hasLocalVersion && hasOnlineVersion)
            {
                if (onlineVersion > localVersion)
                    isNewer = true;
                else if (onlineVersion == localVersion && onlineBuild.HasValue && onlineBuild.Value > localBuild)
                    isNewer = true;
            }
            else if (onlineBuild.HasValue && onlineBuild.Value > localBuild)
            {
                // Fallback: if version parsing fails for any reason, use build as a last resort.
                isNewer = true;
            }

            if (!isNewer)
                return;

            _update = new AndroidUpdatePromptInfo(
                localVersionText,
                localBuildText,
                onlineVersionText,
                onlineBuild.HasValue ? onlineBuild.Value.ToString() : "?",
                info.DownloadUrl!,
                info.Notes);
        }
        catch (Exception ex)
        {
            // Network/JSON errors must not break the app.
            _logger.LogDebug(ex, "Android update check failed");
        }
    }

    private sealed class AndroidUpdateInfo
    {
        public string? Platform { get; set; }
        public string? Version { get; set; }
        public int? Build { get; set; }
        public string? FileName { get; set; }
        public string? DownloadUrl { get; set; }
        public bool Mandatory { get; set; }
        public string? Notes { get; set; }
    }
}
