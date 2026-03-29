using System;
using BoneLib;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Default World — loads a custom level on boot instead of Void G114.
    /// Uses Hooking.OnMarrowGameStarted + SceneStreamer.Load (same approach as FusionProtector).
    /// The barcode is saved/loaded via SettingsManager.
    /// </summary>
    public static class DefaultWorldController
    {
        private static bool _enabled = false;
        private static string _barcode = "";
        private static string _levelName = "";
        private static bool _hooked = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static string Barcode
        {
            get => _barcode;
            set => _barcode = value ?? "";
        }

        public static string LevelName
        {
            get => _levelName;
            set => _levelName = value ?? "";
        }

        /// <summary>
        /// Hook into OnMarrowGameStarted so the home world loads at the right time.
        /// Safe to call multiple times — only hooks once.
        /// </summary>
        public static void Initialize()
        {
            if (_hooked) return;
            _hooked = true;

            Hooking.OnMarrowGameStarted += OnMarrowGameStarted;
            Main.MelonLog.Msg("[DefaultWorld] Hooked OnMarrowGameStarted");
        }

        private static void OnMarrowGameStarted()
        {
            if (!_enabled || string.IsNullOrEmpty(_barcode)) return;

            try
            {
                // Validate the barcode is a known level before loading
                var levelRef = new LevelCrateReference(_barcode);
                var crate = levelRef.Crate;
                if (crate == null || crate.Pallet == null || ((Scannable)crate.Pallet).Barcode == null)
                {
                    Main.MelonLog.Warning($"[DefaultWorld] Barcode not found in game: {_barcode}");
                    return;
                }

                Main.MelonLog.Msg($"[DefaultWorld] Loading home world: {_levelName} ({_barcode})");
                SceneStreamer.Load(((ScannableReference)levelRef).Barcode, (Barcode)null);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[DefaultWorld] Failed to load home world: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the current level as the default world.
        /// </summary>
        public static void SetCurrentLevelAsDefault()
        {
            try
            {
                var session = SceneStreamer.Session;
                if (session == null)
                {
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning, "No active scene session");
                    return;
                }

                var level = session.Level;
                if (level == null)
                {
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning, "No level loaded");
                    return;
                }

                var barcode = ((Scannable)level).Barcode;
                if (barcode == null)
                {
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning, "Could not get level barcode");
                    return;
                }

                _barcode = barcode.ID;
                _levelName = level.Title ?? "Unknown";
                _enabled = true;
                SettingsManager.MarkDirty();
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, $"Default world set: {_levelName}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[DefaultWorld] SetCurrentLevelAsDefault failed: {ex.Message}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error, "Failed to set default world");
            }
        }

        /// <summary>
        /// Clear the default world setting.
        /// </summary>
        public static void ClearDefault()
        {
            _barcode = "";
            _levelName = "";
            _enabled = false;
            SettingsManager.MarkDirty();
            NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Default world cleared");
        }
    }
}
