using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using KGV.ReleaseManager.Models;
using KGV.ReleaseManager.Services;

namespace KGV.ReleaseManager;

public partial class MainWindow : Window
{
    private static readonly Regex AndroidPackageNameRegex = new(
        @"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$",
        RegexOptions.Compiled);

    private readonly CsprojVersionService _csprojVersionService = new();
    private readonly GitService _gitService = new();
    private readonly GitRepositoryService _gitRepositoryService = new();
    private readonly WpfReleaseService _wpfReleaseService = new();
    private readonly AndroidReleaseService _androidReleaseService = new();
    private readonly FolderPickerService _folderPickerService = new();
    private readonly ReleaseNotesService _releaseNotesService = new();

    private string _loadedChangelogHeader = "## [Unreleased]";

    private VersionInfo? _currentWpfVersion;
    private VersionInfo? _currentAndroidVersion;
    private int _currentAndroidBuild;

    private bool _androidReleaseNameManuallyEdited;
    private bool _isUpdatingAndroidReleaseName;

    public MainWindow()
    {
        InitializeComponent();
        InitializeDefaultPaths();
        Loaded += (_, _) => LoadVersionsSafe();
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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

    private string GetReleaseNotesTargetVersion()
    {
        var includesWpf = WpfOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;
        var includesAndroid = AndroidOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;

        if (includesWpf)
        {
            var v = ReadTargetVersion(WpfMajorTextBox.Text, WpfMinorTextBox.Text, WpfPatchTextBox.Text);
            return v.DisplayVersion;
        }

        if (includesAndroid)
        {
            var v = ReadTargetVersion(AndroidMajorTextBox.Text, AndroidMinorTextBox.Text, AndroidPatchTextBox.Text);
            return v.DisplayVersion;
        }

        throw new InvalidOperationException("Bitte eine Release-Variante auswählen.");
    }

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

    private void CopyReleaseData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                throw new InvalidOperationException("Der KGV-Projektordner ist ungültig.");

            var version = GetReleaseNotesTargetVersion();

            var changelog = GetChangelogBlockForTargetVersion(repoRoot, version);
            var block = changelog.Block;
            if (string.IsNullOrWhiteSpace(block))
                block = "(Kein Changelog-Block gefunden oder leer.)";

            var context = _releaseNotesService.TryReadLatestReleaseNotesSummary(repoRoot);
            var prompt = _releaseNotesService.BuildClipboardPrompt(version, DateTime.Today, block, context);

            Clipboard.SetText(prompt);
            System.Windows.MessageBox.Show(this, "Daten kopiert.", "Release-Daten kopieren", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Release-Daten kopieren", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private (string Header, string Block) GetChangelogBlockForTargetVersion(string repoRoot, string version)
    {
        var changelogText = _releaseNotesService.ReadChangelogOrEmpty(repoRoot);
        changelogText = _releaseNotesService.EnsureChangelogSkeleton(changelogText);

        var versionHeader = $"## [{version}]";
        var versionBlock = _releaseNotesService.ExtractChangelogBlock(changelogText, versionHeader);
        if (!string.IsNullOrWhiteSpace(versionBlock))
            return (versionHeader, versionBlock);

        const string unreleasedHeader = "## [Unreleased]";
        var unreleasedBlock = _releaseNotesService.ExtractChangelogBlock(changelogText, unreleasedHeader);
        return (unreleasedHeader, unreleasedBlock);
    }

    private void OpenChangelog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                throw new InvalidOperationException("Der KGV-Projektordner ist ungültig.");

            _releaseNotesService.OpenChangelogInEditor(repoRoot);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Changelog öffnen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadChangelogBlock_Click(object sender, RoutedEventArgs e)
    {
        LoadChangelogBlockSafe(showMessageOnSuccess: true);
    }

    private void SaveChangelogBlock_Click(object sender, RoutedEventArgs e)
    {
        SaveChangelogBlockSafe();
    }

    private void PasteReleaseText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
                ReleaseNotesTextBox.Text = Clipboard.GetText();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Einfügen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveReleaseText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                throw new InvalidOperationException("Der KGV-Projektordner ist ungültig.");

            var version = GetReleaseNotesTargetVersion();
            var fullText = (ReleaseNotesTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fullText))
                throw new InvalidOperationException("Release-Text ist leer.");

            var exists = _releaseNotesService.ReleaseEntryExists(repoRoot, version);
            if (exists)
            {
                var decision = System.Windows.MessageBox.Show(
                    this,
                    $"Für Version {version} existiert bereits ein Eintrag. Soll er aktualisiert werden?",
                    "Release-Text speichern",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (decision != MessageBoxResult.Yes)
                    return;
            }

            var (entry, _) = _releaseNotesService.ParseReleaseNotesText(version, DateTime.Today, fullText);

            var includesWpf = WpfOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;
            var includesAndroid = AndroidOnlyRadioButton.IsChecked == true || BothRadioButton.IsChecked == true;

            // Beim reinen Speichern (Entwurf) darf kein fertiger Android-Build/VersionCode vorausgesetzt werden.
            int? androidBuild = null;
            if (includesAndroid)
            {
                if (int.TryParse(AndroidBuildTextBox.Text, out var parsed) && parsed > 0)
                    androidBuild = parsed;
            }

            var windowsPlatform = PlatformReleaseDefaults.CreateWindows(enabled: includesWpf, status: "Entwurf");

            var androidData = includesAndroid
                ? new AndroidPlatformReleaseData(
                    PackageName: TrimOrNull(AndroidPackageNameTextBox.Text),
                    PlayTrack: TrimOrNull(GetComboValue(AndroidPlayTrackComboBox)),
                    PublishingStatus: TrimOrNull(GetComboValue(AndroidPublishingStatusComboBox)),
                    StoreUrl: TrimOrNull(AndroidStoreUrlTextBox.Text),
                    ReleaseName: TrimOrNull(AndroidReleaseNameTextBox.Text) ?? TrimOrNull(BuildAutoAndroidReleaseNameOrEmpty()),
                    VersionCode: androidBuild,
                    AabArtifactPath: null)
                : null;

            var androidPlatform = PlatformReleaseDefaults.CreateAndroidPlayStore(
                enabled: includesAndroid,
                data: androidData,
                status: includesAndroid ? "Entwurf" : "deaktiviert");

            _releaseNotesService.SaveReleaseNotes(repoRoot, entry, new[] { windowsPlatform, androidPlatform }, masterStatus: "Entwurf");

            System.Windows.MessageBox.Show(this, "Release-Text gespeichert.", "Release-Text speichern", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Release-Text speichern", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadVersionsSafe()
    {
        try
        {
            LogTextBox.Clear();
            LoadVersions();
            LoadChangelogBlockSafe(showMessageOnSuccess: false);
            AppendLog("Versionen erfolgreich geladen.");
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER beim Laden: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "Versionen laden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadChangelogBlockSafe(bool showMessageOnSuccess)
    {
        try
        {
            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                return;

            var version = GetReleaseNotesTargetVersion();
            var result = GetChangelogBlockForTargetVersion(repoRoot, version);

            _loadedChangelogHeader = result.Header;
            LoadedChangelogHeaderRun.Text = _loadedChangelogHeader;
            ChangelogBlockTextBox.Text = result.Block;

            AppendLog($"Changelog-Block geladen: {_loadedChangelogHeader}");

            if (showMessageOnSuccess)
            {
                System.Windows.MessageBox.Show(this, "Changelog-Block geladen.", "CHANGELOG laden", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER beim Laden des CHANGELOG: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "CHANGELOG laden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveChangelogBlockSafe()
    {
        try
        {
            var repoRoot = RepoRootTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                throw new InvalidOperationException("Der KGV-Projektordner ist ungültig.");

            var inputBlock = (ChangelogBlockTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(inputBlock))
                throw new InvalidOperationException("Changelog-Block ist leer.");

            var current = _releaseNotesService.ReadChangelogOrEmpty(repoRoot);
            var updated = _releaseNotesService.UpsertChangelogBlock(current, _loadedChangelogHeader, inputBlock);
            _releaseNotesService.WriteChangelog(repoRoot, updated);

            AppendLog($"Changelog-Block gespeichert: {_loadedChangelogHeader}");
            System.Windows.MessageBox.Show(this, "Changelog-Block gespeichert.", "CHANGELOG speichern", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog("FEHLER beim Speichern des CHANGELOG: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "CHANGELOG speichern", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadVersions()
    {
        var repoRoot = RepoRootTextBox.Text.Trim();

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

        if (!string.Equals(_currentWpfVersion.DisplayVersion, _currentAndroidVersion.DisplayVersion, StringComparison.OrdinalIgnoreCase))
        {
            AppendLog($"WARNUNG: Windows/Android Versionsstände laufen auseinander: Windows={_currentWpfVersion.DisplayVersion}, Android={_currentAndroidVersion.DisplayVersion}.");
        }

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

        TryPrefillAndroidDefaults(repoRoot);
        UpdateAndroidReleaseNameIfAuto();
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

            // Gemeinsame Versionsführung: Wenn beide Plattformen aktiv sind, gilt die WPF-Version als Master-Version.
            var masterVersion = includesWpf
                ? ReadTargetVersion(WpfMajorTextBox.Text, WpfMinorTextBox.Text, WpfPatchTextBox.Text)
                : ReadTargetVersion(AndroidMajorTextBox.Text, AndroidMinorTextBox.Text, AndroidPatchTextBox.Text);

            var wpfVersion = includesWpf ? masterVersion : null;
            var androidVersion = includesAndroid ? masterVersion : null;
            var androidVersionCode = includesAndroid ? ResolveAndroidVersionCode() : 0;

            AndroidPlatformReleaseData? androidPlayStore = null;
            if (includesAndroid)
            {
                androidPlayStore = new AndroidPlatformReleaseData(
                    PackageName: TrimOrNull(AndroidPackageNameTextBox.Text),
                    PlayTrack: TrimOrNull(GetComboValue(AndroidPlayTrackComboBox)),
                    PublishingStatus: TrimOrNull(GetComboValue(AndroidPublishingStatusComboBox)),
                    StoreUrl: TrimOrNull(AndroidStoreUrlTextBox.Text),
                    ReleaseName: TrimOrNull(AndroidReleaseNameTextBox.Text) ?? TrimOrNull(BuildAutoAndroidReleaseNameOrEmpty()),
                    VersionCode: androidVersionCode,
                    AabArtifactPath: null);

                ValidateAndroidPreBuild(androidPlayStore);
            }

            var request = new ReleaseStartRequest(
                IncludesWindows: includesWpf,
                IncludesAndroid: includesAndroid,
                MasterVersion: masterVersion,
                Windows: includesWpf ? new WindowsBuildRequest(wpfVersion!) : null,
                Android: includesAndroid ? new AndroidBuildRequest(androidVersion!, androidVersionCode, androidPlayStore!) : null);

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

            if (request.IncludesWindows)
            {
                AppendLog($"WPF-Version wird auf {wpfVersion!.DisplayVersion} gesetzt.");
                _csprojVersionService.UpdateWpfVersion(WpfCsprojPath, wpfVersion);
            }

            if (request.IncludesAndroid)
            {
                AppendLog($"Android-Version wird auf {androidVersion!.DisplayVersion} (VersionCode {androidVersionCode}) gesetzt.");
                _csprojVersionService.UpdateAndroidVersion(AndroidCsprojPath, androidVersion, androidVersionCode);
            }

            AppendLog("GitHub-Ordner synchronisieren...");
            await _gitRepositoryService.ConfigureForKgvGitHubAsync(context, AppendLog);
            await _gitRepositoryService.EnsureCleanWorkingTreeAsync(context.GitHubRoot, AppendLog);
            await _gitRepositoryService.PullRebaseAsync(context.GitHubRoot, "origin", "main", AppendLog);

            if (request.IncludesWindows)
            {
                AppendLog("Windows-Release wird gestartet...");
                await _wpfReleaseService.RunAsync(context, wpfVersion!, AppendLog);

                try
                {
                    var downloadUrl = context.BaseUrl.TrimEnd('/') + "/KGV-Setup.exe";
                    _releaseNotesService.UpdatePlatformRelease(
                        repoRoot,
                        masterVersion.DisplayVersion,
                        PlatformReleaseDefaults.CreateWindows(
                            enabled: true,
                            data: new WindowsPlatformReleaseData(downloadUrl, "KGV-Setup.exe", null, null),
                            status: "gebaut"));
                }
                catch
                {
                }
            }

            if (request.IncludesAndroid)
            {
                AppendLog("Android-Release wird gestartet...");
                if (androidPlayStore == null)
                    throw new InvalidOperationException("Android Play Store Metadaten fehlen.");

                var androidResult = await _androidReleaseService.RunAsync(context, androidVersion!, androidVersionCode, androidPlayStore, AppendLog);

                try
                {
                    _releaseNotesService.UpdatePlatformRelease(
                        repoRoot,
                        masterVersion.DisplayVersion,
                        PlatformReleaseDefaults.CreateAndroidPlayStore(
                            enabled: true,
                            data: androidPlayStore with { AabArtifactPath = androidResult.AabPath },
                            status: "AAB erstellt"));
                }
                catch
                {
                }
            }

            AppendLog("GitHub-Ordner committen und pushen...");
            var gitHubCommitMessage = BuildGitHubCommitMessage(request.IncludesWindows, request.IncludesAndroid, wpfVersion, androidVersion);
            await _gitRepositoryService.CommitAndPushIfNeededAsync(context.GitHubRoot, gitHubCommitMessage, "origin", "main", AppendLog);

            if (CommitProjectCheckBox.IsChecked == true)
            {
                AppendLog("KGV-Projekt wird committed.");
                await _gitService.CommitAllAsync(repoRoot, request.IncludesWindows, request.IncludesAndroid, wpfVersion, androidVersion, AppendLog);
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
            return $"Release {wpfVersion.DisplayVersion} (Windows+Android)";
        }

        if (includesWpf && wpfVersion is not null)
        {
            return $"WPF {wpfVersion.DisplayVersion} veroeffentlicht";
        }

        if (includesAndroid && androidVersion is not null)
        {
            return $"Android {androidVersion.DisplayVersion} (Play Store)";
        }

        return "Release aktualisiert";
    }

    private static string GetComboValue(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            return (item.Content?.ToString() ?? string.Empty).Trim();

        return (comboBox.Text ?? string.Empty).Trim();
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

    private int ResolveAndroidVersionCode()
    {
        if (int.TryParse(AndroidBuildTextBox.Text, out var parsed) && parsed > 0)
            return parsed;

        try
        {
            var current = _currentAndroidBuild;
            if (current <= 0)
            {
                var android = _csprojVersionService.ReadAndroidVersion(AndroidCsprojPath);
                _currentAndroidVersion = android.DisplayVersion;
                _currentAndroidBuild = android.BuildVersion;
                current = _currentAndroidBuild;
            }

            var next = Math.Max(1, current + 1);
            AndroidBuildTextBox.Text = next.ToString();
            AppendLog($"Android VersionCode war leer/ungültig – automatisch auf {next} gesetzt.");
            return next;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Android VersionCode ist ungültig und konnte nicht automatisch ermittelt werden.", ex);
        }
    }

    private static void ValidateAndroidPreBuild(AndroidPlatformReleaseData playStore)
    {
        if (string.IsNullOrWhiteSpace(playStore.PackageName))
            throw new InvalidOperationException("Android aktiviert: PackageName / ApplicationId fehlt.");

        if (!AndroidPackageNameRegex.IsMatch(playStore.PackageName.Trim()))
            throw new InvalidOperationException($"Android aktiviert: PackageName / ApplicationId ist ungültig: '{playStore.PackageName}'.");

        if (string.IsNullOrWhiteSpace(playStore.PlayTrack))
            throw new InvalidOperationException("Android aktiviert: Play Track fehlt.");

        var track = playStore.PlayTrack.Trim().ToLowerInvariant();
        if (track is not ("internal" or "closed" or "open" or "production"))
            throw new InvalidOperationException($"Android aktiviert: Play Track ist ungültig: '{playStore.PlayTrack}'.");
    }

    private void TryPrefillAndroidDefaults(string repoRoot)
    {
        var draft = _releaseNotesService.TryReadLatestAndroidPlatformDraft(repoRoot);

        var projectId = _csprojVersionService.TryReadAndroidApplicationId(AndroidCsprojPath);

        if (string.IsNullOrWhiteSpace(AndroidPackageNameTextBox.Text))
        {
            var candidate = projectId ?? draft?.PackageName;
            if (!string.IsNullOrWhiteSpace(candidate))
                AndroidPackageNameTextBox.Text = candidate.Trim();
        }

        if (draft is not null)
        {
            TrySelectComboBoxValue(AndroidPlayTrackComboBox, draft.PlayTrack);
            TrySelectComboBoxValue(AndroidPublishingStatusComboBox, draft.PublishingStatus);

            if (string.IsNullOrWhiteSpace(AndroidReleaseNameTextBox.Text) && !string.IsNullOrWhiteSpace(draft.ReleaseName))
            {
                var auto = BuildAutoAndroidReleaseNameOrEmpty();
                var isManual = string.IsNullOrWhiteSpace(auto)
                               || !string.Equals(draft.ReleaseName.Trim(), auto, StringComparison.OrdinalIgnoreCase);

                SetAndroidReleaseName(draft.ReleaseName.Trim(), isManual);
            }
        }

        if (string.IsNullOrWhiteSpace(AndroidReleaseNameTextBox.Text))
        {
            var auto = BuildAutoAndroidReleaseNameOrEmpty();
            if (!string.IsNullOrWhiteSpace(auto))
                SetAndroidReleaseName(auto, isManual: false);
        }
    }

    private static void TrySelectComboBoxValue(System.Windows.Controls.ComboBox comboBox, string? value)
    {
        value = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var item in comboBox.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem cbi
                && string.Equals((cbi.Content?.ToString() ?? string.Empty).Trim(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = cbi;
                return;
            }
        }
    }

    private void AndroidPlayTrackComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateAndroidReleaseNameIfAuto();
    }

    private void VersionFields_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateAndroidReleaseNameIfAuto();
    }

    private void AndroidReleaseNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingAndroidReleaseName)
            return;

        var current = (AndroidReleaseNameTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(current))
        {
            _androidReleaseNameManuallyEdited = false;
            return;
        }

        var auto = BuildAutoAndroidReleaseNameOrEmpty();
        _androidReleaseNameManuallyEdited = string.IsNullOrWhiteSpace(auto)
                                            || !string.Equals(current, auto, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateAndroidReleaseNameIfAuto()
    {
        if (_androidReleaseNameManuallyEdited)
            return;

        var auto = BuildAutoAndroidReleaseNameOrEmpty();
        if (string.IsNullOrWhiteSpace(auto))
            return;

        var current = (AndroidReleaseNameTextBox.Text ?? string.Empty).Trim();
        if (string.Equals(current, auto, StringComparison.OrdinalIgnoreCase))
            return;

        SetAndroidReleaseName(auto, isManual: false);
    }

    private string BuildAutoAndroidReleaseNameOrEmpty()
    {
        try
        {
            var version = GetReleaseNotesTargetVersion();
            var track = GetComboValue(AndroidPlayTrackComboBox);
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(track))
                return version;

            return $"{version} - {track}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void SetAndroidReleaseName(string value, bool isManual)
    {
        _isUpdatingAndroidReleaseName = true;
        AndroidReleaseNameTextBox.Text = value;
        _isUpdatingAndroidReleaseName = false;
        _androidReleaseNameManuallyEdited = isManual;
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

        CopyReleaseDataButton.IsEnabled = enabled;
        OpenChangelogButton.IsEnabled = enabled;
        ReleaseNotesTextBox.IsEnabled = enabled;
        PasteReleaseTextButton.IsEnabled = enabled;
        SaveReleaseTextButton.IsEnabled = enabled;
    }
}
