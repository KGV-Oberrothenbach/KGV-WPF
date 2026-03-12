using System.IO;
using System.Windows;
using KGV.ReleaseManager.Models;
using KGV.ReleaseManager.Services;

namespace KGV.ReleaseManager;

public partial class MainWindow : Window
{
    private readonly CsprojVersionService _csprojVersionService = new();
    private readonly GitService _gitService = new();
    private readonly GitRepositoryService _gitRepositoryService = new();
    private readonly WpfReleaseService _wpfReleaseService = new();
    private readonly AndroidReleaseService _androidReleaseService = new();
    private readonly FolderPickerService _folderPickerService = new();

    private VersionInfo? _currentWpfVersion;
    private VersionInfo? _currentAndroidVersion;
    private int _currentAndroidBuild;

    public MainWindow()
    {
        InitializeComponent();
        InitializeDefaultPaths();
        Loaded += (_, _) => LoadVersionsSafe();
    }

    private void InitializeDefaultPaths()
    {
        var repoRoot = DetectRepoRoot();
        RepoRootTextBox.Text = repoRoot;
        PublishRootTextBox.Text = DetectPublishRoot();
        GitHubRootTextBox.Text = DetectGitHubRoot();
    }

    private static string DetectPublishRoot()
    {
        const string defaultPath = @"D:\Programmieren\KGV-Publish";
        return defaultPath;
    }

    private static string DetectGitHubRoot()
    {
        const string defaultPath = @"D:\Programmieren\KGV-GitHub";
        return defaultPath;
    }

    private string DetectRepoRoot()
    {
        var candidates = new List<string>();
        var baseDirectory = AppContext.BaseDirectory;
        candidates.Add(baseDirectory);

        var parent = Directory.GetParent(baseDirectory);
        while (parent is not null)
        {
            candidates.Add(parent.FullName);
            parent = parent.Parent;
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, "KGV.Wpf", "KGV.Wpf.csproj"))
                && File.Exists(Path.Combine(candidate, "KGV.Maui", "KGV.Maui.csproj")))
            {
                return candidate;
            }
        }

        return baseDirectory;
    }

    private string WpfCsprojPath => Path.Combine(RepoRootTextBox.Text.Trim(), "KGV.Wpf", "KGV.Wpf.csproj");

    private string AndroidCsprojPath => Path.Combine(RepoRootTextBox.Text.Trim(), "KGV.Maui", "KGV.Maui.csproj");

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }

    private void BrowseRepoRoot_Click(object sender, RoutedEventArgs e)
    {
        var selected = _folderPickerService.PickFolder(this, RepoRootTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            RepoRootTextBox.Text = selected;
        }
    }

    private void BrowsePublishRoot_Click(object sender, RoutedEventArgs e)
    {
        var selected = _folderPickerService.PickFolder(this, PublishRootTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            PublishRootTextBox.Text = selected;
        }
    }

    private void BrowseGitHubRoot_Click(object sender, RoutedEventArgs e)
    {
        var selected = _folderPickerService.PickFolder(this, GitHubRootTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            GitHubRootTextBox.Text = selected;
        }
    }

    private void LoadVersions_Click(object sender, RoutedEventArgs e)
    {
        LoadVersionsSafe();
    }

    private void LoadVersionsSafe()
    {
        try
        {
            LogTextBox.Clear();
            LoadVersions();
            AppendLog("Versionen erfolgreich geladen.");
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER beim Laden: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "Versionen laden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadVersions()
    {
        if (!File.Exists(WpfCsprojPath))
        {
            throw new FileNotFoundException("WPF-csproj nicht gefunden.", WpfCsprojPath);
        }

        if (!File.Exists(AndroidCsprojPath))
        {
            throw new FileNotFoundException("MAUI-csproj nicht gefunden.", AndroidCsprojPath);
        }

        _currentWpfVersion = _csprojVersionService.ReadWpfVersion(WpfCsprojPath);
        var android = _csprojVersionService.ReadAndroidVersion(AndroidCsprojPath);
        _currentAndroidVersion = android.DisplayVersion;
        _currentAndroidBuild = android.BuildVersion;

        var nextWpf = _currentWpfVersion.IncrementPatch();
        var nextAndroid = _currentAndroidVersion.IncrementPatch(_currentAndroidBuild + 1);

        CurrentWpfVersionTextBlock.Text = _currentWpfVersion.DisplayVersion;
        CurrentAndroidVersionTextBlock.Text = $"{_currentAndroidVersion.DisplayVersion} (Build {_currentAndroidBuild})";

        WpfMajorTextBox.Text = nextWpf.Major.ToString();
        WpfMinorTextBox.Text = nextWpf.Minor.ToString();
        WpfPatchTextBox.Text = nextWpf.Patch.ToString();

        AndroidMajorTextBox.Text = nextAndroid.Major.ToString();
        AndroidMinorTextBox.Text = nextAndroid.Minor.ToString();
        AndroidPatchTextBox.Text = nextAndroid.Patch.ToString();
        AndroidBuildTextBox.Text = nextAndroid.AndroidBuildVersion.ToString();
    }

    private async void StartRelease_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetUiEnabled(false);
            AppendLog("Release gestartet.");

            var includesWpf = WpfOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;
            var includesAndroid = AndroidOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;

            if (!includesWpf && !includesAndroid)
            {
                throw new InvalidOperationException("Bitte eine Release-Variante auswählen.");
            }

            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            {
                throw new InvalidOperationException("Der KGV-Projektordner ist ungültig.");
            }

            var publishRoot = PublishRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(publishRoot))
            {
                throw new InvalidOperationException("Publish-Root ist ungültig.");
            }
            Directory.CreateDirectory(publishRoot);

            var gitHubRoot = GitHubRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(gitHubRoot) || !Directory.Exists(gitHubRoot))
            {
                throw new InvalidOperationException("Der GitHub-Ordner ist ungültig oder existiert nicht.");
            }

            var wpfVersion = includesWpf ? ReadTargetVersion(WpfMajorTextBox.Text, WpfMinorTextBox.Text, WpfPatchTextBox.Text) : null;
            var androidVersion = includesAndroid ? ReadTargetVersion(AndroidMajorTextBox.Text, AndroidMinorTextBox.Text, AndroidPatchTextBox.Text) : null;
            var androidBuild = includesAndroid ? ReadBuildVersion(AndroidBuildTextBox.Text) : 0;

            var context = new ReleaseContext(
                RepoRoot: repoRoot,
                PublishRoot: publishRoot,
                GitHubRoot: gitHubRoot,
                BaseUrl: "https://kgv-oberrothenbach.github.io/KGV-WPF",
                GitRemoteUrl: "https://KGV-Oberrothenbach@github.com/KGV-Oberrothenbach/KGV-WPF.git",
                GitCredentialUsername: "KGV-Oberrothenbach",
                GitUserName: "KGV-Oberrothenbach",
                GitUserEmail: null,
                KeepCount: 3);

            if (includesWpf)
            {
                AppendLog($"WPF-Version wird auf {wpfVersion!.DisplayVersion} gesetzt.");
                _csprojVersionService.UpdateWpfVersion(WpfCsprojPath, wpfVersion);
            }

            if (includesAndroid)
            {
                AppendLog($"Android-Version wird auf {androidVersion!.DisplayVersion} (Build {androidBuild}) gesetzt.");
                _csprojVersionService.UpdateAndroidVersion(AndroidCsprojPath, androidVersion, androidBuild);
            }

            AppendLog("GitHub-Ordner synchronisieren...");
            await _gitRepositoryService.ConfigureForKgvGitHubAsync(context, AppendLog);
            await _gitRepositoryService.EnsureCleanWorkingTreeAsync(context.GitHubRoot, AppendLog);
            await _gitRepositoryService.PullRebaseAsync(context.GitHubRoot, "origin", "main", AppendLog);

            if (includesWpf)
            {
                AppendLog("Windows-Release wird gestartet...");
                await _wpfReleaseService.RunAsync(context, wpfVersion!, AppendLog);
            }

            if (includesAndroid)
            {
                AppendLog("Android-Release wird gestartet...");
                await _androidReleaseService.RunAsync(context, androidVersion!, androidBuild, AppendLog);
            }

            AppendLog("GitHub-Ordner committen und pushen...");
            var gitHubCommitMessage = BuildGitHubCommitMessage(includesWpf, includesAndroid, wpfVersion, androidVersion);
            await _gitRepositoryService.CommitAndPushIfNeededAsync(context.GitHubRoot, gitHubCommitMessage, "origin", "main", AppendLog);

            if (CommitProjectCheckBox.IsChecked == true)
            {
                AppendLog("KGV-Projekt wird committed.");
                await _gitService.CommitAllAsync(repoRoot, includesWpf, includesAndroid, wpfVersion, androidVersion, AppendLog);
            }

            AppendLog("Release erfolgreich abgeschlossen.");
            System.Windows.MessageBox.Show(this, "Release erfolgreich abgeschlossen.", "KGV Release Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadVersionsSafe();
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "Release fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private static string BuildGitHubCommitMessage(bool includesWpf, bool includesAndroid, VersionInfo? wpfVersion, VersionInfo? androidVersion)
    {
        if (includesWpf && includesAndroid && wpfVersion is not null && androidVersion is not null)
        {
            return $"Release WPF {wpfVersion.DisplayVersion} + Android {androidVersion.DisplayVersion}";
        }

        if (includesWpf && wpfVersion is not null)
        {
            return $"WPF {wpfVersion.DisplayVersion} veroeffentlicht";
        }

        if (includesAndroid && androidVersion is not null)
        {
            return $"Android {androidVersion.DisplayVersion} veroeffentlicht";
        }

        return "Release aktualisiert";
    }

    private static VersionInfo ReadTargetVersion(string majorText, string minorText, string patchText)
    {
        if (!int.TryParse(majorText, out var major) || major < 0)
        {
            throw new InvalidOperationException("Major ist ungültig.");
        }

        if (!int.TryParse(minorText, out var minor) || minor < 0)
        {
            throw new InvalidOperationException("Minor ist ungültig.");
        }

        if (!int.TryParse(patchText, out var patch) || patch < 0)
        {
            throw new InvalidOperationException("Patch ist ungültig.");
        }

        return new VersionInfo(major, minor, patch);
    }

    private static int ReadBuildVersion(string buildText)
    {
        if (!int.TryParse(buildText, out var build) || build <= 0)
        {
            throw new InvalidOperationException("Android-Build ist ungültig.");
        }

        return build;
    }

    private void SetUiEnabled(bool enabled)
    {
        LoadVersionsButton.IsEnabled = enabled;
        StartReleaseButton.IsEnabled = enabled;
        RepoRootTextBox.IsEnabled = enabled;
        PublishRootTextBox.IsEnabled = enabled;
        GitHubRootTextBox.IsEnabled = enabled;
        WpfOnlyRadioButton.IsEnabled = enabled;
        AndroidOnlyRadioButton.IsEnabled = enabled;
        BothRadioButton.IsEnabled = enabled;
        WpfMajorTextBox.IsEnabled = enabled;
        WpfMinorTextBox.IsEnabled = enabled;
        WpfPatchTextBox.IsEnabled = enabled;
        AndroidMajorTextBox.IsEnabled = enabled;
        AndroidMinorTextBox.IsEnabled = enabled;
        AndroidPatchTextBox.IsEnabled = enabled;
        AndroidBuildTextBox.IsEnabled = enabled;
        CommitProjectCheckBox.IsEnabled = enabled;
    }
}
