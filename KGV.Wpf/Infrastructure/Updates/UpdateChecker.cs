using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KGV.Views;
using KGV.Core.Updates;

namespace KGV.Wpf.Infrastructure.Updates
{
    public static class UpdateChecker
    {
        // Central place to change the update endpoint
        public const string VersionJsonUrl = "https://kgv-oberrothenbach.github.io/KGV-WPF/version.json";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(VersionJsonUrl))
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatekonfiguration fehlt.");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                using var response = await Http.GetAsync(VersionJsonUrl, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Update check failed: HTTP {(int)response.StatusCode} for {VersionJsonUrl}");
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
                }

                var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");

                UpdateInfo? info;
                try
                {
                    info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Update check failed: invalid JSON: {ex}");
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen ungültig.");
                }

                if (info == null)
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");

                if (string.IsNullOrWhiteSpace(info.Version) || string.IsNullOrWhiteSpace(info.DownloadUrl))
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");

                if (!Version.TryParse(info.Version, out var onlineVersion))
                    return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateinformationen nicht verfügbar.");

                var currentVersion = GetCurrentVersion();
                if (onlineVersion <= currentVersion)
                    return new UpdateCheckResult(UpdateCheckStatus.NoUpdates, null, null);

                var prompt = new UpdatePromptInfo(
                    CurrentVersion: ToVersionText(currentVersion),
                    CurrentBuild: null,
                    OnlineVersion: ToVersionText(onlineVersion),
                    OnlineBuild: null,
                    DownloadUrl: info.DownloadUrl!,
                    Notes: info.Notes);

                return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, prompt, null);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Update check failed: HTTP request: {ex}");
                return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"Update check failed: {ex}");
                return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updateprüfung fehlgeschlagen.");
            }
            catch (OperationCanceledException)
            {
                return new UpdateCheckResult(UpdateCheckStatus.NotAvailable, null, "Updatequelle nicht erreichbar.");
            }
        }

        // Legacy helper: still available for old call-sites.
        public static async Task CheckForUpdatesAsync(Window? owner)
        {
            var result = await CheckAsync().ConfigureAwait(false);
            if (result.Status != UpdateCheckStatus.UpdateAvailable || result.Prompt == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            await dispatcher.InvokeAsync(() =>
            {
                var dlg = new UpdateAvailableWindow(result.Prompt.CurrentVersion, result.Prompt.OnlineVersion, result.Prompt.Notes)
                {
                    Owner = owner,
                    WindowStartupLocation = owner != null
                        ? WindowStartupLocation.CenterOwner
                        : WindowStartupLocation.CenterScreen
                };

                var dlgResult = dlg.ShowDialog();
                if (dlgResult == true)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(result.Prompt.DownloadUrl) { UseShellExecute = true });
                    }
                    catch
                    {
                        // Intentionally ignore.
                    }
                }
            });
        }

        private static Version GetCurrentVersion()
        {
            try
            {
                return Assembly.GetEntryAssembly()?.GetName().Version
                       ?? Assembly.GetExecutingAssembly().GetName().Version
                       ?? new Version(0, 0, 0, 0);
            }
            catch
            {
                return new Version(0, 0, 0, 0);
            }
        }

        private sealed class UpdateInfo
        {
            public string? Version { get; set; }
            public string? DownloadUrl { get; set; }
            public string? Notes { get; set; }
        }

        private static string ToVersionText(Version v)
        {
            var build = v.Build >= 0 ? v.Build : 0;
            return $"{v.Major}.{v.Minor}.{build}";
        }
    }
}
