using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Anti-Gravity Change (Earth Loop) — prevents other players from changing world gravity.
    /// Every frame, forces Physics.gravity back to (0, -9.81, 0).
    /// Ported from FusionProtector.
    /// </summary>
    public static class AntiGravityChangeController
    {
        private static bool _enabled = false;
        private static readonly Vector3 EarthGravity = new Vector3(0f, -9.81f, 0f);

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value) Physics.gravity = EarthGravity;
                Main.MelonLog.Msg($"Anti-Gravity Change (Earth Loop): {(value ? "ON" : "OFF")}");
            }
        }

        public static void Update()
        {
            if (_enabled)
                Physics.gravity = EarthGravity;
        }
    }
}
