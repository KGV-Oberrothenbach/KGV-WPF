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
        string androidProjectPath,
        VersionInfo version,
        int versionCode,
        AndroidPlatformReleaseData playStore,
        AndroidSettings androidSettings,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var project = androidProjectPath;
        if (string.IsNullOrWhiteSpace(project))
            throw new InvalidOperationException("Android-Projektpfad fehlt.");

        if (!File.Exists(project))
        {
            throw new FileNotFoundException("MAUI-csproj nicht gefunden.", project);
        }

        if (string.IsNullOrWhiteSpace(playStore.PackageName) || string.IsNullOrWhiteSpace(playStore.PlayTrack))
            throw new InvalidOperationException("Android Play Store Metadaten unvollständig (PackageName/Track).");

        var localAndroidRoot = Path.Combine(
            string.IsNullOrWhiteSpace(androidSettings.OutputRoot) ? context.PublishRoot : androidSettings.OutputRoot.Trim(),
            "android");
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
        var tfm = TryReadFirstTargetFramework(project) ?? "net9.0-android";
        var publishArgs = $"publish \"{project}\" -c Release -f {tfm} -p:AndroidPackageFormat=aab -o \"{buildOutput}\"";

        // Play Console: Signing ist Pflicht (keine Secrets im Repo; nur lokale Pfade/Passwortdateien).
        var signingArgs = BuildSigningArgs(androidSettings);
        if (!string.IsNullOrWhiteSpace(signingArgs))
            publishArgs += signingArgs;

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
        var candidates = Directory.EnumerateFiles(buildOutput, "*.aab", SearchOption.AllDirectories)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0)
            throw new FileNotFoundException("Keine AAB im Build-Output gefunden.");

        return candidates[0].FullName;
    }

    private static string? TryReadFirstTargetFramework(string csprojPath)
    {
        try
        {
            var xml = File.ReadAllText(csprojPath);

            string? FindTag(string tag)
            {
                var open = $"<{tag}>";
                var close = $"</{tag}>";
                var start = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
                if (start < 0) return null;
                start += open.Length;
                var end = xml.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
                if (end < 0) return null;
                return xml.Substring(start, end - start).Trim();
            }

            var tfm = FindTag("TargetFramework");
            if (!string.IsNullOrWhiteSpace(tfm))
                return tfm;

            var tfms = FindTag("TargetFrameworks");
            if (string.IsNullOrWhiteSpace(tfms))
                return null;

            return tfms.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildSigningArgs(AndroidSettings settings)
    {
        if (settings == null)
            return string.Empty;

        var keystorePath = (settings.KeystorePath ?? string.Empty).Trim();
        var alias = (settings.KeystoreAlias ?? string.Empty).Trim();
        var storePassFile = (settings.StorePasswordFile ?? string.Empty).Trim();
        var keyPassFile = (settings.KeyPasswordFile ?? string.Empty).Trim();

        // Backward-compatible fallback: allow env vars if settings are empty.
        if (string.IsNullOrWhiteSpace(keystorePath))
            keystorePath = (Environment.GetEnvironmentVariable("KGV_ANDROID_KEYSTORE_PATH") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(alias))
            alias = (Environment.GetEnvironmentVariable("KGV_ANDROID_KEYSTORE_ALIAS") ?? string.Empty).Trim();

        var storePass = ReadSecretFromFileOrEnv(storePassFile, "KGV_ANDROID_KEYSTORE_PASS");
        var keyPass = ReadSecretFromFileOrEnv(string.IsNullOrWhiteSpace(keyPassFile) ? storePassFile : keyPassFile, "KGV_ANDROID_KEY_PASS");

        var hasAnySigning = !string.IsNullOrWhiteSpace(keystorePath)
                            || !string.IsNullOrWhiteSpace(alias)
                            || !string.IsNullOrWhiteSpace(storePass)
                            || !string.IsNullOrWhiteSpace(keyPass);

        if (!hasAnySigning)
        {
            if (settings.RequireSigning)
                throw new InvalidOperationException("Android-Signing ist als Pflicht konfiguriert, aber Keystore/Alias/Passwortquelle fehlt.");

            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(keystorePath) || string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(storePass) || string.IsNullOrWhiteSpace(keyPass))
            throw new InvalidOperationException("Android-Signing ist aktiviert, aber Keystore/Alias/Passwort fehlt.");

        if (!File.Exists(keystorePath))
            throw new FileNotFoundException("Keystore-Datei nicht gefunden.", keystorePath);

        return
            $" -p:AndroidKeyStore=true" +
            $" -p:AndroidSigningKeyStore=\"{keystorePath}\"" +
            $" -p:AndroidSigningKeyAlias=\"{alias}\"" +
            $" -p:AndroidSigningStorePass=\"{storePass}\"" +
            $" -p:AndroidSigningKeyPass=\"{keyPass}\"";
    }

    private static string ReadSecretFromFileOrEnv(string filePath, string envVar)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                var text = File.ReadAllText(filePath);
                return (text ?? string.Empty).Trim();
            }
        }
        catch
        {
        }

        return (Environment.GetEnvironmentVariable(envVar) ?? string.Empty).Trim();
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
