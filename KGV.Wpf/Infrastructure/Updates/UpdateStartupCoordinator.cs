using KGV.Core.Updates;
using KGV.Views;
using KGV.Wpf.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KGV.Wpf.Infrastructure.Updates;

public static class UpdateStartupCoordinator
{
    private static int _started;

    public static void Start(MainWindowViewModel viewModel, Window owner)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        _ = RunAsync(viewModel, owner);
    }

    private static async Task RunAsync(MainWindowViewModel viewModel, Window owner)
    {
        try
        {
            viewModel.UpdateStatusText = "Updates werden gesucht";

            var result = await UpdateChecker.CheckAsync();

            switch (result.Status)
            {
                case UpdateCheckStatus.NoUpdates:
                    viewModel.UpdateStatusText = "Keine Updates gefunden";
                    break;

                case UpdateCheckStatus.UpdateAvailable:
                    viewModel.UpdateStatusText = "Update verfügbar";
                    await ShowPromptAsync(owner, result.Prompt);
                    break;

                default:
                    viewModel.UpdateStatusText = "Updateprüfung nicht verfügbar";
                    break;
            }
        }
        catch
        {
            viewModel.UpdateStatusText = "Updateprüfung nicht verfügbar";
        }
    }

    private static async Task ShowPromptAsync(Window owner, UpdatePromptInfo? prompt)
    {
        if (prompt == null)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        await dispatcher.InvokeAsync(() =>
        {
            var dlg = new UpdateAvailableWindow(prompt.CurrentVersion, prompt.OnlineVersion, prompt.Notes)
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var dlgResult = dlg.ShowDialog();
            if (dlgResult == true)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(prompt.DownloadUrl) { UseShellExecute = true });
                }
                catch
                {
                }
            }
        });
    }
}
