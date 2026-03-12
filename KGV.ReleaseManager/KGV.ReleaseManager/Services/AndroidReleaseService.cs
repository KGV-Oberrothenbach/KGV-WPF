using System.Text.RegularExpressions;
using KGV.ReleaseManager.Models;
using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class AndroidReleaseService
{
    private readonly ProcessRunner _processRunner = new();
    private readonly JsonManifestService _jsonManifestService = new();

    public async Task RunAsync(ReleaseContext context, VersionInfo version, int buildNumber, Action<string> log, CancellationToken cancellationToken = default)
    {
        var project = Path.Combine(context.RepoRoot, "KGV.Maui", "KGV.Maui.csproj");
        if (!File.Exists(project))
        {
            throw new FileNotFoundException("MAUI-csproj nicht gefunden.", project);
        }

        var localAndroidRoot = Path.Combine(context.PublishRoot, "android");
        var versionDir = Path.Combine(localAndroidRoot, version.DisplayVersion);
        var apkName = $"KGV-android-v{version.DisplayVersion}.apk";

        var localApkPath = Path.Combine(versionDir, apkName);
        var localJsonPath = Path.Combine(versionDir, "version.json");

        var gitAndroidDir = Path.Combine(context.GitHubRoot, "android");
        var gitApkPath = Path.Combine(gitAndroidDir, apkName);
        var gitJsonPath = Path.Combine(gitAndroidDir, "version.json");

        var buildOutput = Path.Combine(context.RepoRoot, "artifacts", "android", "publish", version.DisplayVersion);

        Directory.CreateDirectory(localAndroidRoot);
        Directory.CreateDirectory(gitAndroidDir);

        RecreateDirectory(buildOutput);

        log("dotnet clean (Android)...");
        await RunDotnetAsync(context.RepoRoot, $"clean \"{project}\" -c Release", log, cancellationToken);

        log("dotnet restore (Android)...");
        await RunDotnetAsync(context.RepoRoot, $"restore \"{project}\"", log, cancellationToken);

        log("dotnet publish (Android)...");
        var publishArgs = $"publish \"{project}\" -c Release -f net9.0-android -p:AndroidPackageFormat=apk -o \"{buildOutput}\"";

        var keystorePath = Environment.GetEnvironmentVariable("KGV_ANDROID_KEYSTORE_PATH");
        if (!string.IsNullOrWhiteSpace(keystorePath))
        {
            var alias = Environment.GetEnvironmentVariable("KGV_ANDROID_KEYSTORE_ALIAS");
            var storePass = Environment.GetEnvironmentVariable("KGV_ANDROID_KEYSTORE_PASS");
            var keyPass = Environment.GetEnvironmentVariable("KGV_ANDROID_KEY_PASS");

            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(storePass) || string.IsNullOrWhiteSpace(keyPass))
            {
                throw new InvalidOperationException("Android-Signing ist aktiviert (KGV_ANDROID_KEYSTORE_PATH), aber Alias/Passwörter fehlen.");
            }

            publishArgs +=
                $" -p:AndroidKeyStore=true" +
                $" -p:AndroidSigningKeyStore=\"{keystorePath}\"" +
                $" -p:AndroidSigningKeyAlias=\"{alias}\"" +
                $" -p:AndroidSigningStorePass=\"{storePass}\"" +
                $" -p:AndroidSigningKeyPass=\"{keyPass}\"";
        }

        await RunDotnetAsync(context.RepoRoot, publishArgs, log, cancellationToken);

        log("APK suchen...");
        var builtApk = LocateApk(buildOutput);
        log($"APK gefunden: {builtApk}");

        RecreateDirectory(versionDir);

        File.Copy(builtApk, localApkPath, overwrite: true);
        File.Copy(builtApk, gitApkPath, overwrite: true);

        var downloadUrl = context.BaseUrl.TrimEnd('/') + "/android/" + apkName;
        _jsonManifestService.WriteAndroidVersionJson(localJsonPath, version.DisplayVersion, buildNumber, apkName, downloadUrl);
        _jsonManifestService.WriteAndroidVersionJson(gitJsonPath, version.DisplayVersion, buildNumber, apkName, downloadUrl);

        CleanupOldVersionDirectories(localAndroidRoot, context.KeepCount);
        CleanupOldGitApks(gitAndroidDir, context.KeepCount);
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

    private static string LocateApk(string buildOutput)
    {
        static string? FindFirst(string root, string pattern)
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).FirstOrDefault();
        }

        return FindFirst(buildOutput, "*-Signed.apk")
               ?? FindFirst(buildOutput, "*Signed.apk")
               ?? FindFirst(buildOutput, "*.apk")
               ?? throw new FileNotFoundException("Keine APK im Build-Output gefunden.");
    }

    private static void CleanupOldVersionDirectories(string localAndroidRoot, int keepCount)
    {
        var versionRegex = new Regex(@"^\d+(\.\d+){1,3}$", RegexOptions.Compiled);

        var dirs = Directory.EnumerateDirectories(localAndroidRoot)
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

    private static void CleanupOldGitApks(string gitAndroidDir, int keepCount)
    {
        var files = Directory.EnumerateFiles(gitAndroidDir, "KGV-android-v*.apk", SearchOption.TopDirectoryOnly)
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
        var name = Path.GetFileNameWithoutExtension(file.Name);
        if (!name.StartsWith("KGV-android-v", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionPart = name["KGV-android-v".Length..];
        return Version.TryParse(versionPart, out var parsed) ? parsed : null;
    }
}
