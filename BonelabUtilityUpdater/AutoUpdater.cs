using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MelonLoader;

namespace BonelabUtilityUpdater
{
    internal static class AutoUpdater
    {
        private const string MOD_DLL_NAME = "BonelabUtilityMod.dll";
        private const byte XOR_KEY = 0xC7;

        // ══════════════════════════════════════════════════════════════════
        //  CONFIGURATION — XOR-encoded GitHub owner and repo name.
        //  These byte arrays decode to your GitHub username and repo name
        //  at runtime. The plaintext never appears in the compiled DLL.
        //
        //  HOW TO SET UP:
        //  1) In UpdaterPlugin.cs, uncomment the LogEncodedStrings line
        //  2) Fill in your real GitHub username and repo name
        //  3) Build & run the game once — check MelonLoader console log
        //  4) Copy the logged byte arrays and paste them below
        //  5) Re-comment the LogEncodedStrings line in UpdaterPlugin.cs
        //  6) Rebuild
        // ══════════════════════════════════════════════════════════════════
        private static readonly byte[] _encodedOwner = { 0x8B, 0xA7, 0xB7, 0xA3, 0xA6, 0xB6, 0xA0, 0xA3, 0xBB };
        private static readonly byte[] _encodedRepo = { 0xA8, 0xB1, 0xAA };

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static void CheckAndUpdate(MelonLogger.Instance logger)
        {
            string owner = Decode(_encodedOwner);
            string repo = Decode(_encodedRepo);

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

        private static string Decode(byte[] data)
        {
            char[] c = new char[data.Length];
            for (int i = 0; i < data.Length; i++)
                c[i] = (char)(data[i] ^ XOR_KEY ^ (byte)(i & 0x1F));
            return new string(c);
        }

        private static byte[] Encode(string text)
        {
            byte[] result = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
                result[i] = (byte)((byte)text[i] ^ XOR_KEY ^ (byte)(i & 0x1F));
            return result;
        }

        /// <summary>
        /// Call this once from UpdaterPlugin.OnPreInitialization to generate
        /// the encoded byte arrays for your GitHub owner and repo name.
        /// Check the MelonLoader console log for the output.
        /// </summary>
        public static void LogEncodedStrings(string owner, string repo, MelonLogger.Instance logger)
        {
            logger.Msg("═══ Copy these into AutoUpdater.cs ═══");
            logger.Msg($"_encodedOwner = {FormatByteArray(Encode(owner))};");
            logger.Msg($"_encodedRepo  = {FormatByteArray(Encode(repo))};");
            logger.Msg("═══════════════════════════════════════");
        }

        private static string FormatByteArray(byte[] bytes)
        {
            return "{ " + string.Join(", ", bytes.Select(b => $"0x{b:X2}")) + " }";
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
