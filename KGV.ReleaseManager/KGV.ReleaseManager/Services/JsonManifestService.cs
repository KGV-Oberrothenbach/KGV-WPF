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
            version,
            build,
            fileName,
            downloadUrl,
            mandatory = false,
            notes = "Neue Android-Version",
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
