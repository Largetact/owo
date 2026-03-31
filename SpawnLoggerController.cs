using System;
using System.Reflection;
using HarmonyLib;
using Il2CppSLZ.Marrow;
using MelonLoader;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Spawn Logger — logs what items are spawned and by whom via Harmony patch.
    /// Hooks into the spawn response data to monitor network spawns.
    /// Safe and Fusion-friendly — read-only logging, no blocking.
    /// </summary>
    public static class SpawnLoggerController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Spawn Logger: {(value ? "ON" : "OFF")}");
            }
        }

        /// <summary>
        /// Whether to show in-game notifications for spawns (can be noisy).
        /// </summary>
        public static bool ShowNotifications { get; set; } = false;

        public static void Initialize()
        {
            Main.MelonLog.Msg("Spawn Logger controller initialized");
        }

        /// <summary>
        /// Called by external hooks when a network spawn is detected.
        /// </summary>
        public static void LogSpawn(string barcode, string ownerName, byte ownerSmallId)
        {
            if (!_enabled) return;

            string displayName = ResolveName(barcode);
            string msg = $"[SpawnLog] {ownerName} (ID:{ownerSmallId}) spawned: {displayName}";
            Main.MelonLog.Msg(msg);

            if (ShowNotifications)
            {
                NotificationHelper.Send(
                    BoneLib.Notifications.NotificationType.Information,
                    $"{ownerName} spawned {displayName}",
                    "Spawn Log", 2f
                );
            }
        }

        /// <summary>
        /// Log a local spawn event.
        /// </summary>
        public static void LogLocalSpawn(string barcode)
        {
            if (!_enabled) return;
            string displayName = ResolveName(barcode);
            Main.MelonLog.Msg($"[SpawnLog] YOU spawned: {displayName}");
        }

        private static string ResolveName(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return "(unknown)";

            // Try to resolve a friendly name via the asset warehouse
            try
            {
                Type warehouseType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { warehouseType = asm.GetType("Il2CppSLZ.Marrow.Warehouse.AssetWarehouse"); } catch { }
                    if (warehouseType != null) break;
                }

                if (warehouseType != null)
                {
                    var instanceProp = warehouseType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        // Try GetCrate
                        var getCrate = warehouseType.GetMethod("GetCrate",
                            new[] { typeof(string) });
                        if (getCrate != null)
                        {
                            var crate = getCrate.Invoke(instance, new object[] { barcode });
                            if (crate != null)
                            {
                                var titleProp = crate.GetType().GetProperty("Title");
                                string title = titleProp?.GetValue(crate)?.ToString();
                                if (!string.IsNullOrEmpty(title))
                                    return title;
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback: last part of barcode
            if (barcode.Contains("-") && barcode.Length > 8)
                return barcode.Substring(barcode.Length - 8);
            return barcode;
        }
    }
}
