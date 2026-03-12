using System.Text.RegularExpressions;
using KGV.ReleaseManager.Models;
using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class WpfReleaseService
{
    private readonly ProcessRunner _processRunner = new();
    private readonly InnoSetupService _innoSetupService = new();
    private readonly JsonManifestService _jsonManifestService = new();

    public async Task RunAsync(ReleaseContext context, VersionInfo version, Action<string> log, CancellationToken cancellationToken = default)
    {
        var wpfProject = Path.Combine(context.RepoRoot, "KGV.Wpf", "KGV.Wpf.csproj");
        var innoScript = Path.Combine(context.RepoRoot, "Installer", "InnoSetup", "KGV.Wpf.iss");

        if (!File.Exists(wpfProject))
        {
            throw new FileNotFoundException("WPF-csproj nicht gefunden.", wpfProject);
        }

        if (!File.Exists(innoScript))
        {
            throw new FileNotFoundException("Inno-Setup-Skript nicht gefunden.", innoScript);
        }

        var localWpfRoot = Path.Combine(context.PublishRoot, "wpf");
        var appFilesCurrent = Path.Combine(localWpfRoot, "AppFiles", "Current");
        var currentInstallerDir = Path.Combine(localWpfRoot, "Installers", "Current");
        var versionDir = Path.Combine(localWpfRoot, version.DisplayVersion);

        var localVersionedSetup = Path.Combine(versionDir, $"KGV-Setup-{version.DisplayVersion}.exe");
        var localCurrentSetup = Path.Combine(versionDir, "KGV-Setup.exe");
        var localJsonPath = Path.Combine(versionDir, "version.json");

        var gitVersionedSetup = Path.Combine(context.GitHubRoot, $"KGV-Setup-{version.DisplayVersion}.exe");
        var gitCurrentSetup = Path.Combine(context.GitHubRoot, "KGV-Setup.exe");
        var gitJsonPath = Path.Combine(context.GitHubRoot, "version.json");

        Directory.CreateDirectory(localWpfRoot);
        Directory.CreateDirectory(context.GitHubRoot);

        RecreateDirectory(versionDir);
        RecreateDirectory(appFilesCurrent);
        RecreateDirectory(currentInstallerDir);

        log("dotnet clean (WPF)...");
        await RunDotnetAsync(context.RepoRoot, $"clean \"{wpfProject}\" -c Release", log, cancellationToken);

        log("dotnet restore (WPF)...");
        await RunDotnetAsync(context.RepoRoot, $"restore \"{wpfProject}\"", log, cancellationToken);

        log("dotnet publish (WPF)...");
        await RunDotnetAsync(context.RepoRoot, $"publish \"{wpfProject}\" -c Release -r win-x64 --self-contained false -o \"{appFilesCurrent}\"", log, cancellationToken);

        var expectedExe = Path.Combine(appFilesCurrent, "KGV.Wpf.exe");
        if (!File.Exists(expectedExe))
        {
            throw new FileNotFoundException("KGV.Wpf.exe wurde im Publish-Ordner nicht gefunden.", expectedExe);
        }

        log("Inno Setup bauen...");
        var iscc = _innoSetupService.LocateIsccExe();
        await _innoSetupService.BuildInstallerAsync(iscc, innoScript, appFilesCurrent, currentInstallerDir, context.RepoRoot, log, cancellationToken);

        var setupSource = LocateSetupExe(currentInstallerDir);
        log($"Setup gefunden: {setupSource}");

        File.Copy(setupSource, localVersionedSetup, overwrite: true);
        File.Copy(setupSource, localCurrentSetup, overwrite: true);
        File.Copy(setupSource, gitVersionedSetup, overwrite: true);
        File.Copy(setupSource, gitCurrentSetup, overwrite: true);

        var downloadUrl = context.BaseUrl.TrimEnd('/') + "/KGV-Setup.exe";
        _jsonManifestService.WriteWindowsVersionJson(localJsonPath, version.DisplayVersion, downloadUrl);
        _jsonManifestService.WriteWindowsVersionJson(gitJsonPath, version.DisplayVersion, downloadUrl);

        CleanupOldVersionDirectories(localWpfRoot, context.KeepCount);
        CleanupOldGitInstallers(context.GitHubRoot, context.KeepCount);
    }

    private async Task RunDotnetAsync(string workingDirectory, string args, Action<string> log, CancellationToken cancellationToken)
    {
        var exit = await _processRunner.RunAsync("dotnet", args, workingDirectory, log, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException($"dotnet {args} ist fehlgeschlagen.");
        }
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static string LocateSetupExe(string installerDir)
    {
        var primary = Path.Combine(installerDir, "KGV-Setup.exe");
        if (File.Exists(primary))
        {
            return primary;
        }

        var candidates = Directory.GetFiles(installerDir, "KGV-Setup-*.exe", SearchOption.TopDirectoryOnly);
        if (candidates.Length == 0)
        {
            throw new FileNotFoundException("Kein Installer gefunden.");
        }

        return candidates.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase).First();
    }

    private static void CleanupOldVersionDirectories(string localWpfRoot, int keepCount)
    {
        var versionRegex = new Regex(@"^\d+(\.\d+){1,3}$", RegexOptions.Compiled);

        var dirs = Directory.EnumerateDirectories(localWpfRoot)
            .Select(d => new DirectoryInfo(d))
            .Where(d => versionRegex.IsMatch(d.Name) && Version.TryParse(d.Name, out _))
            .OrderByDescending(d => Version.Parse(d.Name))
            .ToList();

        foreach (var dir in dirs.Skip(keepCount))
        {
            try
            {
                dir.Delete(recursive: true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    private static void CleanupOldGitInstallers(string gitRoot, int keepCount)
    {
        var files = Directory.EnumerateFiles(gitRoot, "KGV-Setup-*.exe", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .Select(f => new
            {
                File = f,
                Version = TryParseVersionFromFileName(f),
            })
            .Where(x => x.Version is not null)
            .OrderByDescending(x => x.Version)
            .ToList();

        foreach (var item in files.Skip(keepCount))
        {
            try
            {
                item.File.Delete();
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    private static Version? TryParseVersionFromFileName(FileInfo file)
    {
        // KGV-Setup-<version>.exe
        var name = Path.GetFileNameWithoutExtension(file.Name);
        if (!name.StartsWith("KGV-Setup-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionPart = name["KGV-Setup-".Length..];
        return Version.TryParse(versionPart, out var parsed) ? parsed : null;
    }
}
