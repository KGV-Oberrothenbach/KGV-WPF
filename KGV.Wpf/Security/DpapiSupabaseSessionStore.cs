using KGV.Core.Interfaces;
using Supabase.Gotrue;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KGV.Wpf.Security
{
    internal sealed class DpapiSupabaseSessionStore : ISupabaseSessionStore
    {
        private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KGV");
        private static readonly string SessionFile = Path.Combine(SettingsDir, "supabase-session.bin");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KGV|SupabaseSession|v1");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public Session? Load()
        {
            try
            {
                if (!File.Exists(SessionFile))
                    return null;

                var protectedBytes = File.ReadAllBytes(SessionFile);
                if (protectedBytes == null || protectedBytes.Length == 0)
                    return null;

                var jsonBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                if (jsonBytes == null || jsonBytes.Length == 0)
                    return null;

                var session = JsonSerializer.Deserialize<Session>(jsonBytes, JsonOpts);
                return session;
            }
            catch
            {
                return null;
            }
        }

        public void Save(Session session)
        {
            try
            {
                if (session == null)
                    return;

                Directory.CreateDirectory(SettingsDir);

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(session, JsonOpts);
                var protectedBytes = ProtectedData.Protect(jsonBytes, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(SessionFile, protectedBytes);
            }
            catch
            {
            }
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(SessionFile))
                    File.Delete(SessionFile);
            }
            catch
            {
            }
        }
    }
}
