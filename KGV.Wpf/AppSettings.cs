using System;
using System.IO;
using System.Text.Json;

namespace KGV.Wpf
{
    internal static class AppSettings
    {
        private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KGV");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "user-settings.json");

        private class UserSettings
        {
            public string? LastEmail { get; set; }

            public string? ImpressumVerantwortlichText { get; set; }

            // Supabase-Konfiguration
            public SupabaseSettings? Supabase { get; set; }
        }

        public class SupabaseSettings
        {
            public string Url { get; set; } = "";
            public string Key { get; set; } = "";
        }

        private static UserSettings _settings = new();

        public static string? LastEmail
        {
            get => _settings.LastEmail;
            set => _settings.LastEmail = value;
        }

        public static string ImpressumVerantwortlichText
        {
            get => string.IsNullOrWhiteSpace(_settings.ImpressumVerantwortlichText)
                ? "Kleingartenverein Oberrothenbach e.V."
                : _settings.ImpressumVerantwortlichText!;
            set => _settings.ImpressumVerantwortlichText = value;
        }

        // Neue Properties für Supabase
        public static string SupabaseUrl => _settings.Supabase?.Url ?? "";
        public static string SupabaseAnonKey => _settings.Supabase?.Key ?? "";

        // Load settings from %AppData%\KGV\user-settings.json. Errors are ignored.
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

        // Save settings to %AppData%\KGV\user-settings.json. Errors are ignored.
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, opts);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // ignore
            }
        }
    }
}