using System.Text.Json;

namespace KGV.Maui.Settings;

public static class AppSettings
{
    private static readonly string SettingsFile = Path.Combine(FileSystem.AppDataDirectory, "user-settings.json");

    private sealed class UserSettings
    {
        public string? LastEmail { get; set; }
        public string? AppMode { get; set; }
    }

    private static UserSettings _settings = new();

    public static string? LastEmail
    {
        get => _settings.LastEmail;
        set => _settings.LastEmail = value;
    }

    public static string? AppMode
    {
        get => _settings.AppMode;
        set => _settings.AppMode = value;
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                _settings = new UserSettings();
                return;
            }

            var json = File.ReadAllText(SettingsFile);
            if (string.IsNullOrWhiteSpace(json))
            {
                _settings = new UserSettings();
                return;
            }

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loaded = JsonSerializer.Deserialize<UserSettings>(json, opts);
            _settings = loaded ?? new UserSettings();
        }
        catch
        {
            _settings = new UserSettings();
        }
    }

    public static void Save()
    {
        try
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, opts);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
        }
    }
}
