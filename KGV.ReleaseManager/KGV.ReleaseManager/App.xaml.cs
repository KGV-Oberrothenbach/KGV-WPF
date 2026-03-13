using System.IO;
using System.Windows;

namespace KGV.ReleaseManager;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            HandleFatal(e.Exception, "UI");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleFatal(ex, "AppDomain");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            HandleFatal(e.Exception, "TaskScheduler");
        };
    }

    private void HandleFatal(Exception exception, string source)
    {
        var logPath = TryWriteCrashLog(exception, source);

        var message = logPath is null
            ? "Ein unerwarteter Fehler ist aufgetreten und die Anwendung wird beendet."
            : $"Ein unerwarteter Fehler ist aufgetreten und die Anwendung wird beendet.\n\nCrash-Log: {logPath}";

        try
        {
            MessageBox.Show(message, "KGV Release Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // ignore UI errors
        }

        try
        {
            Shutdown(-1);
        }
        catch
        {
            // ignore shutdown errors
        }
    }

    private static string? TryWriteCrashLog(Exception exception, string source)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KGV.ReleaseManager");

            Directory.CreateDirectory(root);

            var path = Path.Combine(root, "crash.log");
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Source={source}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(path, text);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
