using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace KGV.Wpf.Infrastructure.Configuration
{
    public static class ConfigurationInitializer
    {
        // NOTE: This is obfuscation / a practical hurdle, not a secure secret store.
        // Key material is intentionally split.
        private static readonly byte[] KeyPart1 = new byte[]
        {
            87, 22, 191, 44, 9, 130, 77, 211, 18, 240, 61, 152, 5, 99, 36, 171
        };

        private static readonly byte[] KeyPart2 = new byte[]
        {
            14, 209, 58, 133, 77, 6, 181, 92, 235, 47, 160, 12, 198, 74, 101, 33
        };

        private static readonly byte[] Salt = new byte[]
        {
            201, 17, 66, 90, 143, 4, 222, 61
        };

        private const string EmbeddedTemplateResourceName = "KGV.Wpf.Configuration.appsettings.enc";

        public static string GetConfigPath()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "KGV", "appsettings.json");
        }

        public static void EnsureConfigExists()
        {
            var configPath = GetConfigPath();
            if (File.Exists(configPath))
                return;

            var dir = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(dir))
                throw new InvalidOperationException($"Invalid config directory for path '{configPath}'.");

            Directory.CreateDirectory(dir);

            var templateBytes = ReadEmbeddedTemplate();
            var json = DecryptToUtf8(templateBytes);

            File.WriteAllText(configPath, json, Encoding.UTF8);
        }

        private static byte[] ReadEmbeddedTemplate()
        {
            var asm = Assembly.GetExecutingAssembly();

            // Prefer explicit LogicalName.
            var stream = asm.GetManifestResourceStream(EmbeddedTemplateResourceName);
            if (stream == null)
            {
                // Fallback: allow slight differences in resource naming.
                var name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("appsettings.enc", StringComparison.OrdinalIgnoreCase));

                if (name != null)
                    stream = asm.GetManifestResourceStream(name);
            }

            if (stream == null)
                throw new FileNotFoundException(
                    $"Embedded configuration template not found. Expected resource '{EmbeddedTemplateResourceName}'.");

            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static string DecryptToUtf8(byte[] payload)
        {
            if (payload.Length < 16)
                throw new InvalidDataException("Encrypted template payload is too small (missing IV).");

            var iv = payload.Take(16).ToArray();
            var cipher = payload.Skip(16).ToArray();

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DeriveKey();
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }

        private static byte[] DeriveKey()
        {
            using var sha = SHA256.Create();

            var all = new byte[KeyPart1.Length + KeyPart2.Length + Salt.Length];
            Buffer.BlockCopy(KeyPart1, 0, all, 0, KeyPart1.Length);
            Buffer.BlockCopy(KeyPart2, 0, all, KeyPart1.Length, KeyPart2.Length);
            Buffer.BlockCopy(Salt, 0, all, KeyPart1.Length + KeyPart2.Length, Salt.Length);

            return sha.ComputeHash(all);
        }
    }
}
