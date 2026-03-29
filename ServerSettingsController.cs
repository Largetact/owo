using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Server Settings Controller — based on FusionProtector's EditFusionPreferences.
    /// Directly modifies LabFusion's "BONELAB Fusion" MelonPreferences category
    /// and pushes lobby updates. No permission check at all.
    /// 
    /// When you ARE the host: Changes apply to the server and sync to all players.
    /// When you are a client: Changes apply locally only (server prefs are host-side).
    /// </summary>
    public static class ServerSettingsController
    {
        public static void Initialize()
        {
            Main.MelonLog.Msg("ServerSettingsController initialized (no permission check)");
        }

        // ── Core: EditFusionPreferences (from FusionProtector) ────

        /// <summary>
        /// Directly modify a LabFusion server preference by its display name.
        /// Same approach as FusionProtector's EditFusionPreferences.
        /// No permission check — works for any player when they're the host.
        /// </summary>
        public static void EditFusionPreference(string displayName, object value)
        {
            try
            {
                var category = MelonPreferences.GetCategory("BONELAB Fusion");
                if (category == null)
                {
                    Main.MelonLog.Warning("BONELAB Fusion preferences category not found");
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning,
                        "Fusion prefs not found — not in a server?");
                    return;
                }

                var entry = category.Entries.FirstOrDefault(e => e.DisplayName == displayName);
                if (entry == null)
                {
                    Main.MelonLog.Warning($"Fusion preference '{displayName}' not found");
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning,
                        $"Setting '{displayName}' not found");
                    return;
                }

                entry.BoxedValue = value;
                category.SaveToFile(true);

                // Push lobby update so changes propagate (if we're host)
                try { LobbyInfoManager.PushLobbyUpdate(); } catch { }

                Main.MelonLog.Msg($"Set Fusion pref '{displayName}' = {value}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success,
                    $"{displayName}: {value}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"EditFusionPreference error: {ex.Message}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error,
                    $"Failed: {ex.Message}");
            }
        }

        // ── Toggle helpers (mirrors FusionProtector's Owner server settings) ──

        /// <summary>Get the current value of a LobbyInfo bool setting.</summary>
        private static bool GetLobbyBool(Func<LobbyInfo, bool> getter)
        {
            try { return getter(LobbyInfoManager.LobbyInfo); }
            catch { return false; }
        }

        public static void ToggleNameTags()
        {
            EditFusionPreference("Server Nametags Enabled", !GetLobbyBool(l => l.NameTags));
        }

        public static void ToggleVoiceChat()
        {
            EditFusionPreference("Server Voicechat Enabled", !GetLobbyBool(l => l.VoiceChat));
        }

        public static void ToggleMortality()
        {
            EditFusionPreference("Server Mortality", !GetLobbyBool(l => l.Mortality));
        }

        public static void ToggleFriendlyFire()
        {
            EditFusionPreference("Friendly Fire", !GetLobbyBool(l => l.FriendlyFire));
        }

        public static void ToggleKnockout()
        {
            EditFusionPreference("Knockout", !GetLobbyBool(l => l.Knockout));
        }

        public static void TogglePlayerConstraining()
        {
            EditFusionPreference("Server Player Constraints Enabled", !GetLobbyBool(l => l.PlayerConstraining));
        }

        // ── Permission level settings ──

        public static void SetDevTools(int level)
        {
            EditFusionPreference("Dev Tools Allowed", (PermissionLevel)(sbyte)level);
        }

        public static void SetConstrainer(int level)
        {
            EditFusionPreference("Constrainer Allowed", (PermissionLevel)(sbyte)level);
        }

        public static void SetCustomAvatars(int level)
        {
            EditFusionPreference("Custom Avatars Allowed", (PermissionLevel)(sbyte)level);
        }

        public static void SetTeleportation(int level)
        {
            EditFusionPreference("Teleportation", (PermissionLevel)(sbyte)level);
        }
    }
}
