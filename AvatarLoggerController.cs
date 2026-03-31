using System;
using System.Collections.Generic;
using BoneLib.Notifications;
using MelonLoader;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Logs when players change their avatar.
    /// Network-friendly: reads data only, does not send any packets.
    /// </summary>
    public static class AvatarLoggerController
    {
        public static bool Enabled { get; set; }
        public static bool ShowNotifications { get; set; } = true;

        // playerId → last known barcode
        private static readonly Dictionary<string, string> _lastKnownAvatar = new();
        private static float _pollTimer;
        private const float PollInterval = 2f;

        public static void Initialize()
        {
            Main.MelonLog.Msg("Avatar Logger controller initialized");
        }

        public static void Update()
        {
            if (!Enabled) return;

            _pollTimer += UnityEngine.Time.deltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            PollAvatars();
        }

        public static void OnLevelLoaded()
        {
            _lastKnownAvatar.Clear();
        }

        private static void PollAvatars()
        {
            try
            {
                // Attempt to get players from LabFusion if available
                Type playerRepType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { playerRepType = asm.GetType("LabFusion.Representation.PlayerRepManager"); } catch { }
                    if (playerRepType != null) break;
                }

                if (playerRepType == null) return;

                var repsProp = playerRepType.GetProperty("PlayerReps",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?? playerRepType.GetProperty("playerReps",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);

                if (repsProp == null) return;

                var reps = repsProp.GetValue(null);
                if (reps == null) return;

                var enumerable = reps as System.Collections.IEnumerable;
                if (enumerable == null) return;

                foreach (var repObj in enumerable)
                {
                    try
                    {
                        // Each PlayerRep has a PlayerId and avatar barcode
                        var idProp = repObj.GetType().GetProperty("PlayerId")
                            ?? repObj.GetType().GetProperty("SmallId");
                        var nameProp = repObj.GetType().GetProperty("Username");

                        string playerId = idProp?.GetValue(repObj)?.ToString() ?? "?";
                        string playerName = nameProp?.GetValue(repObj)?.ToString() ?? "Unknown";

                        // Try to get current avatar barcode
                        string barcode = TryGetBarcode(repObj);
                        if (string.IsNullOrEmpty(barcode)) continue;

                        if (_lastKnownAvatar.TryGetValue(playerId, out var prev))
                        {
                            if (prev != barcode)
                            {
                                string friendlyName = ResolveFriendlyName(barcode);
                                string msg = $"{playerName} changed avatar → {friendlyName}";
                                MelonLogger.Msg($"[AvatarLog] {msg}");

                                if (ShowNotifications)
                                    NotificationHelper.Send(NotificationType.Information, msg);

                                _lastKnownAvatar[playerId] = barcode;
                            }
                        }
                        else
                        {
                            _lastKnownAvatar[playerId] = barcode;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string TryGetBarcode(object playerRep)
        {
            try
            {
                // Try direct property
                var avStatProp = playerRep.GetType().GetProperty("AvatarBarcode")
                    ?? playerRep.GetType().GetProperty("avatarBarcode");
                if (avStatProp != null) return avStatProp.GetValue(playerRep)?.ToString();

                // Try RigRefs -> AvatarStats -> barcode
                var rigRefsProp = playerRep.GetType().GetProperty("RigRefs");
                if (rigRefsProp != null)
                {
                    var rigRefs = rigRefsProp.GetValue(playerRep);
                    if (rigRefs != null)
                    {
                        var rmProp = rigRefs.GetType().GetProperty("RigManager");
                        if (rmProp != null)
                        {
                            var rm = rmProp.GetValue(rigRefs);
                            if (rm != null)
                            {
                                var avStatP = rm.GetType().GetProperty("AvatarCrate");
                                if (avStatP != null)
                                {
                                    var crate = avStatP.GetValue(rm);
                                    if (crate != null)
                                    {
                                        var bcProp = crate.GetType().GetProperty("Barcode");
                                        if (bcProp != null)
                                            return bcProp.GetValue(crate)?.ToString();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string ResolveFriendlyName(string barcode)
        {
            try
            {
                Type warehouseType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { warehouseType = asm.GetType("Il2CppSLZ.Marrow.Warehouse.AssetWarehouse"); } catch { }
                    if (warehouseType != null) break;
                }
                if (warehouseType == null) return barcode;

                var inst = warehouseType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) return barcode;

                // TryGetCrate(Barcode) → Crate → Title
                var tryGetCrate = inst.GetType().GetMethod("GetCrate",
                    new[] { typeof(string) });
                if (tryGetCrate == null) return barcode;

                var crate = tryGetCrate.Invoke(inst, new object[] { barcode });
                if (crate == null) return barcode;

                var titleProp = crate.GetType().GetProperty("Title");
                if (titleProp != null)
                    return titleProp.GetValue(crate)?.ToString() ?? barcode;
            }
            catch { }
            return barcode;
        }
    }
}
