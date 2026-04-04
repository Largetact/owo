using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MelonLoader;

namespace BonelabUtilityUpdater
{
    internal static class AutoUpdater
    {
        private const string MOD_DLL_NAME = "BonelabUtilityMod.dll";

        // ══════════════════════════════════════════════════════════════════
        //  CONFIGURATION — AES-256-CBC encrypted GitHub owner and repo name.
        //  These byte arrays are decrypted at runtime.
        //  The plaintext never appears in the compiled DLL.
        // ══════════════════════════════════════════════════════════════════
        private static readonly byte[] _aesKey = { 0xC7, 0x69, 0x1B, 0x1D, 0xCD, 0x71, 0x90, 0x62, 0xC3, 0xA4, 0x2F, 0xEE, 0x4F, 0x06, 0x02, 0x54, 0x2E, 0xE6, 0x5E, 0xC2, 0x89, 0x25, 0x8F, 0x36, 0xEB, 0x84, 0x2D, 0xE5, 0xA8, 0x9F, 0x31, 0xD1 };
        private static readonly byte[] _aesIv = { 0xB8, 0x46, 0xD2, 0x69, 0xB7, 0x08, 0x10, 0x44, 0x16, 0xD5, 0xDA, 0x0C, 0x81, 0x58, 0x09, 0x4E };
        private static readonly byte[] _encOwner = { 0x5B, 0x7E, 0x8A, 0x36, 0x02, 0xD9, 0x31, 0xA0, 0x05, 0x9A, 0x81, 0x97, 0x68, 0x5C, 0xBF, 0x0B };
        private static readonly byte[] _encRepo = { 0x18, 0xBC, 0x4A, 0x01, 0x44, 0xD9, 0x0A, 0x8B, 0x05, 0x40, 0x03, 0xDD, 0xA4, 0x90, 0x0F, 0x99 };

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static void CheckAndUpdate(MelonLogger.Instance logger)
        {
            string owner = DecryptAes(_encOwner);
            string repo = DecryptAes(_encRepo);

            if (owner == "CHANGE_ME" || repo == "CHANGE_ME")
            {
                logger.Warning("Auto-updater not configured! See AutoUpdater.cs for setup instructions.");
                return;
            }

            // Get currently installed version
            Version current = new Version(0, 0, 0);
            string modPath = UpdaterPlugin.ModAssemblyPath;

            if (File.Exists(modPath))
            {
                try
                {
                    var asmName = AssemblyName.GetAssemblyName(modPath);
                    current = new Version(asmName.Version.Major, asmName.Version.Minor, asmName.Version.Build);
                    logger.Msg($"Installed {MOD_DLL_NAME} v{current}");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Could not read version from {MOD_DLL_NAME}: {ex.Message}");
                }
            }
            else
            {
                logger.Msg($"{MOD_DLL_NAME} not found — will download latest release");
            }

            try
            {
                // Query GitHub Releases API
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("BonelabUtilityUpdater/1.0");
                _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                string json = _http.GetStringAsync(apiUrl).GetAwaiter().GetResult();

                // Parse tag_name
                string latestTag = ExtractJsonString(json, "tag_name");
                if (latestTag == null)
                {
                    logger.Warning("Could not parse latest version from GitHub response");
                    return;
                }

                string versionStr = latestTag.TrimStart('v', 'V');
                if (!Version.TryParse(versionStr, out Version latest))
                {
                    logger.Warning($"Could not parse version from tag: {latestTag}");
                    return;
                }

                if (latest <= current)
                {
                    logger.Msg($"{MOD_DLL_NAME} is up to date (v{current})");
                    return;
                }

                logger.Msg(ConsoleColor.Cyan, $"New version available: v{latest} (installed: v{current})");

                // Download URL follows standard GitHub pattern
                string downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{latestTag}/{MOD_DLL_NAME}";

                logger.Msg($"Downloading {MOD_DLL_NAME} v{latest}...");
                byte[] newDll = _http.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();

                if (newDll.Length < 1024)
                {
                    logger.Error("Downloaded file is suspiciously small — aborting update");
                    return;
                }

                // Back up old DLL
                if (File.Exists(modPath))
                {
                    string backupPath = modPath + ".backup";
                    File.Copy(modPath, backupPath, true);
                }

                // Write new DLL (mod hasn't loaded yet so file isn't locked)
                File.WriteAllBytes(modPath, newDll);
                logger.Msg(ConsoleColor.Green, $"Successfully updated {MOD_DLL_NAME} to v{latest}!");
            }
            catch (HttpRequestException ex)
            {
                logger.Warning($"Network error during update check: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                logger.Warning("Update check timed out (no internet?)");
            }
            catch (Exception ex)
            {
                logger.Error($"Auto-update failed: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  XOR encoding / decoding
        // ═══════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════
        //  AES-256-CBC decryption
        // ═══════════════════════════════════════════════════════

        private static string DecryptAes(byte[] cipher)
        {
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.IV = _aesIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }

        // ═══════════════════════════════════════════════════════
        //  Simple JSON string extraction (no external deps)
        // ═══════════════════════════════════════════════════════

        private static string ExtractJsonString(string json, string key)
        {
            // Matches "key" : "value" with flexible whitespace
            var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
