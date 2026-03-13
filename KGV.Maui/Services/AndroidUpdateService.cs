using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KGV.Core.Updates;

namespace KGV.Maui.Services;

public interface IAndroidUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

public sealed class AndroidUpdateService : IAndroidUpdateService
{
    // Central place to change the update endpoint.
    // GitHub Pages:
    //   https://<user>.github.io/<repo>/android/version.json
    // Must match the publish location used by the ReleaseManager (BaseUrl + /android/version.json).
    public const string VersionJsonUrl = "https://kgv-oberrothenbach.github.io/KGV-WPF/android/version.json";

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private readonly ILogger<AndroidUpdateService> _logger;

    private int _checkStarted;
    private Task? _checkTask;
    private UpdateCheckResult? _result;

    public AndroidUpdateService(ILogger<AndroidUpdateService> logger)
    {
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // Safety guard for future multi-targeting. (Project is currently Android-only.)
        if (DeviceInfo.Platform != DevicePlatform.Android)
            return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Nicht unterstützt auf dieser Plattform.");

        await EnsureCheckedAsync(cancellationToken).ConfigureAwait(false);
        return _result ?? new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateprüfung nicht verfügbar.");
    }

    private Task EnsureCheckedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _checkStarted, 1, 0) != 0)
            return _checkTask ?? Task.CompletedTask;

        _checkTask = CheckCoreAsync(cancellationToken);
        return _checkTask;
    }

    private async Task CheckCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(VersionJsonUrl))
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatekonfiguration fehlt.");
                return;
            }

            using var response = await Http.GetAsync(VersionJsonUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Android update check: HTTP {StatusCode} for {Url}", (int)response.StatusCode, VersionJsonUrl);
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");
                return;
            }

            AndroidUpdateInfo? info;
            try
            {
                info = JsonSerializer.Deserialize<AndroidUpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Android update check failed: invalid JSON");
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen ungültig.");
                return;
            }

            if (info == null)
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");
                return;
            }

            if (!string.Equals((info.Platform ?? string.Empty).Trim(), "android", StringComparison.OrdinalIgnoreCase))
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");
                return;
            }

            if (string.IsNullOrWhiteSpace(info.Version) || string.IsNullOrWhiteSpace(info.DownloadUrl))
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");
                return;
            }

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
            {
                _result = new UpdateCheckResult(UpdateCheckStatus.NoUpdates, null, null);
                return;
            }

            _result = new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                new UpdatePromptInfo(
                    localVersionText,
                    localBuildText,
                    onlineVersionText,
                    onlineBuild.HasValue ? onlineBuild.Value.ToString() : "?",
                    info.DownloadUrl!,
                    info.Notes),
                null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Android update check failed: HTTP request");
            _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Network/JSON errors must not break the app.
            _logger.LogDebug(ex, "Android update check failed");
            _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateprüfung fehlgeschlagen.");
        }
        catch (OperationCanceledException)
        {
            _result = new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
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
