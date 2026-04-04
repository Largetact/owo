using System;
using System.IO;
using MelonLoader;
using MelonLoader.Preferences;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(BonelabUtilityUpdater.UpdaterPlugin), "BonelabUtilityUpdater", "1.0.0", "XI")]
[assembly: MelonColor(255, 0, 255, 255)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace BonelabUtilityUpdater
{
    public class UpdaterPlugin : MelonPlugin
    {
        private const string MOD_DLL = "BonelabUtilityMod.dll";

        private MelonPreferences_Category _prefs;
        private MelonPreferences_Entry<bool> _offlineMode;

        public static readonly string ModAssemblyPath = Path.Combine(MelonEnvironment.ModsDirectory, MOD_DLL);
        public static MelonLogger.Instance Logger { get; private set; }

        public override void OnPreInitialization()
        {
            Logger = LoggerInstance;

            _prefs = MelonPreferences.CreateCategory("BonelabUtilityUpdater");
            _offlineMode = _prefs.CreateEntry("OfflineMode", false);
            _prefs.SaveToFile(false);

            if (_offlineMode.Value)
            {
                LoggerInstance.Msg(System.ConsoleColor.Yellow, "Auto-updater is OFFLINE");
                if (!File.Exists(ModAssemblyPath))
                    LoggerInstance.Warning($"{MOD_DLL} not found in Mods folder. Download it manually or switch to ONLINE mode.");
                return;
            }

            LoggerInstance.Msg(System.ConsoleColor.Green, "Auto-updater is ONLINE");

            // ────────────────────────────────────────────────────────────
            //  Config is AES-256-CBC encrypted in AutoUpdater.cs.
            //  To change owner/repo, re-encrypt with a new PowerShell script.
            // ────────────────────────────────────────────────────────────

            AutoUpdater.CheckAndUpdate(LoggerInstance);
        }
    }
}
