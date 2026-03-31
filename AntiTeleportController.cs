using BoneLib;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Anti-Teleport — detects and reverses forced teleportation by other players.
    /// Tracks the player's position and reverts if a sudden large displacement occurs
    /// without user-initiated movement (dash, flight, etc.).
    /// Safe and Fusion-friendly — purely defensive.
    /// </summary>
    public static class AntiTeleportController
    {
        private static bool _enabled = false;
        private static Vector3 _lastPosition = Vector3.zero;
        private static bool _hasPosition = false;
        private static float _maxAllowedDelta = 30f; // meters per frame — anything above is suspicious

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                _hasPosition = false;
                Main.MelonLog.Msg($"Anti-Teleport: {(value ? "ON" : "OFF")}");
            }
        }

        /// <summary>
        /// Maximum distance allowed per frame before it's considered a forced teleport.
        /// Default 30m is generous enough to not interfere with dash/flight.
        /// </summary>
        public static float Threshold
        {
            get => _maxAllowedDelta;
            set => _maxAllowedDelta = Mathf.Max(5f, value);
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Anti-Teleport controller initialized");
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null || rigManager.physicsRig == null)
                {
                    _hasPosition = false;
                    return;
                }

                // Skip during user-initiated teleports (dash/flight enabled = fast movement expected)
                if (DashController.IsDashEnabled || FlightController.Enabled)
                {
                    _lastPosition = Player.Head.position;
                    return;
                }

                Vector3 currentPos = Player.Head.position;

                if (!_hasPosition)
                {
                    _lastPosition = currentPos;
                    _hasPosition = true;
                    return;
                }

                float delta = Vector3.Distance(currentPos, _lastPosition);

                if (delta > _maxAllowedDelta)
                {
                    // Forced teleport detected — revert
                    try
                    {
                        var head = Player.Head;
                        Vector3 fwd = head != null ? head.forward : Vector3.forward;
                        rigManager.Teleport(_lastPosition, fwd);
                    }
                    catch { }
                    NotificationHelper.Send(
                        BoneLib.Notifications.NotificationType.Warning,
                        $"Anti-Teleport blocked ({delta:F0}m jump)"
                    );
                    Main.MelonLog.Msg($"[Anti-Teleport] Blocked forced teleport: {delta:F1}m displacement");
                }
                else
                {
                    _lastPosition = currentPos;
                }
            }
            catch { }
        }

        /// <summary>
        /// Call this when the player intentionally teleports (waypoints, menu TP, etc.)
        /// so Anti-Teleport doesn't flag it.
        /// </summary>
        public static void NotifyIntentionalTeleport()
        {
            _hasPosition = false;
        }

        public static void OnLevelUnloaded()
        {
            _hasPosition = false;
        }
    }
}
