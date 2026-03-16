using System.IO;
using System.Text.Json;
using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
    }

    public ReleaseManagerSettings LoadOrDefault()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return ReleaseManagerSettings.CreateDefaults();

            var json = File.ReadAllText(SettingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return ReleaseManagerSettings.CreateDefaults();

            var loaded = JsonSerializer.Deserialize<ReleaseManagerSettings>(json, JsonOptions);
            return loaded ?? ReleaseManagerSettings.CreateDefaults();
        }
        catch
        {
            return ReleaseManagerSettings.CreateDefaults();
        }
    }

    public void Save(ReleaseManagerSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static string GetDefaultSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "KGV.ReleaseManager", "settings.json");
    }
}
