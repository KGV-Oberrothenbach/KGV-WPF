using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KGV.Views;

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

        public static async Task CheckForUpdatesAsync(Window? owner)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));

                var json = await Http.GetStringAsync(VersionJsonUrl, cts.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (info == null)
                    return;

                if (string.IsNullOrWhiteSpace(info.Version) || string.IsNullOrWhiteSpace(info.DownloadUrl))
                    return;

                if (!Version.TryParse(info.Version, out var onlineVersion))
                    return;

                var currentVersion = GetCurrentVersion();
                if (onlineVersion <= currentVersion)
                    return;

                // UI dialog must run on the UI thread.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                await dispatcher.InvokeAsync(() =>
                {
                    var dlg = new UpdateAvailableWindow(currentVersion, onlineVersion, info.Notes)
                    {
                        Owner = owner,
                        WindowStartupLocation = owner != null
                            ? WindowStartupLocation.CenterOwner
                            : WindowStartupLocation.CenterScreen
                    };

                    var result = dlg.ShowDialog();
                    if (result == true)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true });
                        }
                        catch
                        {
                            // Intentionally ignore.
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex}");
                // No user-facing error, app continues normally.
            }
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
    }
}
