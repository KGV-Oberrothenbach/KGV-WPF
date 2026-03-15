using System.Text.RegularExpressions;
using KGV.ReleaseManager.Models;
using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class AndroidReleaseService
{
    private readonly ProcessRunner _processRunner = new();
    private readonly JsonManifestService _jsonManifestService = new();

    public async Task<AndroidBuildResult> RunAsync(
        ReleaseContext context,
        VersionInfo version,
        int versionCode,
        AndroidPlatformReleaseData playStore,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var project = Path.Combine(context.RepoRoot, "KGV.Maui", "KGV.Maui.csproj");
        if (!File.Exists(project))
        {
            throw new FileNotFoundException("MAUI-csproj nicht gefunden.", project);
        }

        if (string.IsNullOrWhiteSpace(playStore.PackageName) || string.IsNullOrWhiteSpace(playStore.PlayTrack))
            throw new InvalidOperationException("Android Play Store Metadaten unvollständig (PackageName/Track).");

        var localAndroidRoot = Path.Combine(context.PublishRoot, "android");
        var versionDir = Path.Combine(localAndroidRoot, version.DisplayVersion);
        var aabName = $"KGV-android-v{version.DisplayVersion}.aab";

        var localAabPath = Path.Combine(versionDir, aabName);
        var localJsonPath = Path.Combine(versionDir, "version.json");

        var gitAndroidDir = Path.Combine(context.GitHubRoot, "android");
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
        var publishArgs = $"publish \"{project}\" -c Release -f net9.0-android -p:AndroidPackageFormat=aab -o \"{buildOutput}\"";

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

        log("AAB suchen...");
        var builtAab = LocateAab(buildOutput);
        log($"AAB gefunden: {builtAab}");

        RecreateDirectory(versionDir);

        File.Copy(builtAab, localAabPath, overwrite: true);

        var storeUrl = string.IsNullOrWhiteSpace(playStore.StoreUrl) ? null : playStore.StoreUrl.Trim();
        var releaseName = string.IsNullOrWhiteSpace(playStore.ReleaseName)
            ? $"{version.DisplayVersion} - {playStore.PlayTrack}"
            : playStore.ReleaseName.Trim();

        _jsonManifestService.WriteAndroidPlayStoreVersionJson(
            localJsonPath,
            version.DisplayVersion,
            versionCode,
            playStore.PackageName!,
            playStore.PlayTrack!,
            playStore.PublishingStatus ?? "unknown",
            storeUrl,
            releaseName);

        _jsonManifestService.WriteAndroidPlayStoreVersionJson(
            gitJsonPath,
            version.DisplayVersion,
            versionCode,
            playStore.PackageName!,
            playStore.PlayTrack!,
            playStore.PublishingStatus ?? "unknown",
            storeUrl,
            releaseName);

        TryCopyReleasesJson(context, log);

        CleanupOldVersionDirectories(localAndroidRoot, context.KeepCount);

        return new AndroidBuildResult(
            DisplayVersion: version.DisplayVersion,
            VersionCode: versionCode,
            AabPath: localAabPath);
    }

    private static void TryCopyReleasesJson(ReleaseContext context, Action<string> log)
    {
        try
        {
            var source = Path.Combine(context.RepoRoot, "Documentation", "releases.json");
            if (!File.Exists(source))
            {
                log("Hinweis: releases.json nicht gefunden – wird nicht in den GitHub-Ordner kopiert.");
                return;
            }

            var destination = Path.Combine(context.GitHubRoot, "releases.json");
            File.Copy(source, destination, overwrite: true);
            log("releases.json nach GitHub-Ordner kopiert.");
        }
        catch (Exception ex)
        {
            log("Hinweis: releases.json konnte nicht kopiert werden: " + ex.Message);
        }
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

    private static string LocateAab(string buildOutput)
    {
        static string? FindFirst(string root, string pattern)
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).FirstOrDefault();
        }

        return FindFirst(buildOutput, "*.aab")
               ?? throw new FileNotFoundException("Keine AAB im Build-Output gefunden.");
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

    // Android: keine AABs in GitHubRoot aufräumen, da AAB kein Endnutzer-Download mehr ist.
}
