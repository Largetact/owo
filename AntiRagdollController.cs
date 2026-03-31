using BoneLib;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Anti-Ragdoll — prevents other players from forcefully ragdolling you via network messages.
    /// When enabled, auto-unragdolls the player if ragdolled without user input.
    /// Safe and Fusion-friendly — purely defensive.
    /// </summary>
    public static class AntiRagdollController
    {
        private static bool _enabled = false;
        private static float _lastRagdollBlock = 0f;
        private const float NOTIFICATION_COOLDOWN = 3f;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Anti-Ragdoll: {(value ? "ON" : "OFF")}");
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Anti-Ragdoll controller initialized");
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null || rigManager.physicsRig == null) return;

                // If our own ragdoll system is enabled, don't interfere (user intentionally ragdolling)
                if (RagdollController.Enabled) return;

                // Check if the physics rig is in ragdoll state not initiated by us
                // The rig's "turnedOn" property: false = ragdolled, true = normal
                bool rigOn = true;
                try
                {
                    var turnedOnField = rigManager.physicsRig.GetType().GetField("turningOn",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (turnedOnField != null)
                        rigOn = (bool)turnedOnField.GetValue(rigManager.physicsRig);
                }
                catch { }

                if (!rigOn)
                {
                    // We're ragdolled but NOT by our own system — force un-ragdoll
                    try
                    {
                        RagdollController.UnragdollPlayer(rigManager.physicsRig);
                    }
                    catch { }

                    if (Time.time - _lastRagdollBlock > NOTIFICATION_COOLDOWN)
                    {
                        _lastRagdollBlock = Time.time;
                        NotificationHelper.Send(
                            BoneLib.Notifications.NotificationType.Warning,
                            "Anti-Ragdoll blocked forced ragdoll"
                        );
                    }
                }
            }
            catch { }
        }
    }
}
