using KGV.Core.Updates;
using KGV.Maui.State;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KGV.Maui.Services;

public static class UpdateStartupCoordinator
{
    private static int _started;

    public static void Start(IServiceProvider services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        _ = RunAsync(services);
    }

    private static async Task RunAsync(IServiceProvider services)
    {
        var status = services.GetService<AppStatusState>();
        var updater = services.GetService<IAndroidUpdateService>();

        if (status == null || updater == null)
            return;

        try
        {
            status.UpdateStatusText = "Updates werden gesucht";

            var result = await updater.CheckForUpdatesAsync().ConfigureAwait(false);

            switch (result.Status)
            {
                case UpdateCheckStatus.NoUpdates:
                    status.UpdateStatusText = "Keine Updates gefunden";
                    break;

                case UpdateCheckStatus.UpdateAvailable:
                    status.UpdateStatusText = "Update verfügbar";
                    await TryShowPromptAsync(result.Prompt).ConfigureAwait(false);
                    break;

                default:
                    status.UpdateStatusText = "Updateprüfung nicht verfügbar";
                    break;
            }
        }
        catch
        {
            status.UpdateStatusText = "Updateprüfung nicht verfügbar";
        }
    }

    private static async Task TryShowPromptAsync(UpdatePromptInfo? prompt)
    {
        if (prompt == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page == null)
                return;

            var message = BuildMessage(prompt);

            var download = await page.DisplayAlert("Update verfügbar", message, "Herunterladen", "Später");
            if (!download)
                return;

            try
            {
                if (Uri.TryCreate(prompt.DownloadUrl, UriKind.Absolute, out var uri))
                    await Launcher.Default.OpenAsync(uri);
            }
            catch
            {
            }
        });
    }

    private static string BuildMessage(UpdatePromptInfo prompt)
    {
        var current = prompt.CurrentBuild != null
            ? $"{prompt.CurrentVersion} (Build {prompt.CurrentBuild})"
            : prompt.CurrentVersion;

        var online = prompt.OnlineBuild != null
            ? $"{prompt.OnlineVersion} (Build {prompt.OnlineBuild})"
            : prompt.OnlineVersion;

        var msg = $"Es ist eine neue Version verfügbar.\n\nAktuell: {current}\nNeu: {online}";

        var notes = (prompt.Notes ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(notes))
            msg += "\n\nHinweise:\n" + notes;

        return msg;
    }
}
