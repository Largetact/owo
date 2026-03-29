using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using LabFusion.Network;
using LabFusion.Data;
using LabFusion.Preferences.Server;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Automatically hosts a friends-only lobby once Fusion is logged in.
    /// Retries each frame until Fusion is ready, then hosts once.
    /// </summary>
    public static class AutoHostController
    {
        private static bool _enabled = false;
        private static bool _hasHostedThisSession = false;
        private static bool _levelLoadedOnce = false;
        private static int _retryCount = 0;
        private static float _lastAttemptTime = -999f;
        private const int MAX_RETRIES = 10;
        private const float RETRY_INTERVAL = 3f;  // seconds between attempts

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static void Initialize() { }

        /// <summary>
        /// Called from OnLevelLoaded hook. Marks that a level has loaded,
        /// so Update() can begin attempting to auto-host.
        /// </summary>
        public static void OnFirstLevelLoaded()
        {
            if (_levelLoadedOnce) return;
            _levelLoadedOnce = true;

            // Try immediately in case Fusion is already ready
            TryAutoHost();
        }

        /// <summary>
        /// Called each frame. If enabled and a level has loaded but we haven't
        /// hosted yet, keep retrying until Fusion is logged in and Layer is ready.
        /// </summary>
        public static void Update()
        {
            if (_hasHostedThisSession || !_enabled || !_levelLoadedOnce) return;
            if (_retryCount >= MAX_RETRIES) return;

            // Throttle: only attempt every RETRY_INTERVAL seconds
            float now = UnityEngine.Time.time;
            if (now - _lastAttemptTime < RETRY_INTERVAL) return;
            _lastAttemptTime = now;

            TryAutoHost();
        }

        private static void TryAutoHost()
        {
            if (_hasHostedThisSession) return;
            if (!_enabled) return;

            // Wait until Fusion is actually logged in AND Layer is ready
            if (!NetworkLayerManager.LoggedIn || !NetworkLayerManager.HasLayer) return;

            // Wait until the player rig and avatar are initialized.
            // If we start the server before RigData is ready, the host has no
            // avatar representation — other players can't see or hear the host.
            if (!RigData.HasPlayer) return;

            _hasHostedThisSession = true;

            try
            {
                if (NetworkInfo.HasServer)
                {
                    Main.MelonLog.Msg("[AutoHost] Already in a server, skipping auto-host");
                    return;
                }

                // Set privacy to FRIENDS_ONLY via SavedServerSettings (proper Fusion API)
                try
                {
                    SavedServerSettings.Privacy.Value = ServerPrivacy.FRIENDS_ONLY;
                    Main.MelonLog.Msg("[AutoHost] Set privacy to FRIENDS_ONLY via SavedServerSettings");
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[AutoHost] SavedServerSettings failed, trying MelonPreferences: {ex.Message}");
                    SetPrivacyFallback();
                }

                // Start the server via Layer directly (not NetworkHelper which has null-conditional)
                NetworkLayerManager.Layer.StartServer();

                Main.MelonLog.Msg("[AutoHost] Auto-hosted friends-only lobby");
                NotificationHelper.Send(
                    BoneLib.Notifications.NotificationType.Success,
                    "Auto-hosted friends-only lobby",
                    "DOOBER UTILS", 3f, true);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[AutoHost] Failed to auto-host: {ex.Message}");
                _retryCount++;
                // Allow retry on next interval (up to MAX_RETRIES)
                _hasHostedThisSession = false;
                if (_retryCount >= MAX_RETRIES)
                    Main.MelonLog.Warning("[AutoHost] Max retries reached, giving up");
            }
        }

        private static void SetPrivacyFallback()
        {
            try
            {
                var category = MelonPreferences.GetCategory("BONELAB Fusion");
                if (category == null) return;

                var entry = category.Entries.FirstOrDefault(e => e.DisplayName == "Server Privacy");
                if (entry == null) return;

                entry.BoxedValue = ServerPrivacy.FRIENDS_ONLY;
                category.SaveToFile(true);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[AutoHost] Fallback privacy set failed: {ex.Message}");
            }
        }
    }
}
