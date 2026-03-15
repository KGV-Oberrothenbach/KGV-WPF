using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class JsonManifestService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void WriteWindowsVersionJson(string outputPath, string version, string downloadUrl)
    {
        var payload = new
        {
            version,
            downloadUrl,
            notes = "Neue Windows-Version",
        };

        WriteJson(outputPath, payload);
    }

    public void WriteAndroidVersionJson(string outputPath, string version, int build, string fileName, string downloadUrl)
    {
        var payload = new
        {
            platform = "android",
            distribution = "DirectDownload",
            version,
            build,
            fileName,
            downloadUrl,
            mandatory = false,
            notes = "Neue Android-Version",
        };

        WriteJson(outputPath, payload);
    }

    public void WriteAndroidPlayStoreVersionJson(
        string outputPath,
        string version,
        int versionCode,
        string packageName,
        string playTrack,
        string publishingStatus,
        string? storeUrl,
        string? releaseName)
    {
        var payload = new
        {
            platform = "android",
            distribution = "PlayStore",
            version,
            versionCode,
            packageName,
            playTrack,
            publishingStatus,
            storeUrl,
            releaseName,
            notes = "Neue Android-Version (Google Play Store)",
        };

        WriteJson(outputPath, payload);
    }

    private static void WriteJson(string outputPath, object payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Ungültiger JSON-Pfad."));
        File.WriteAllText(outputPath, json);
    }
}
