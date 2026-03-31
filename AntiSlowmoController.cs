using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Anti-Slowmo — prevents other players/mods from changing Time.timeScale.
    /// Every frame, forces timeScale back to 1.0f when enabled.
    /// Safe and Fusion-friendly — purely defensive.
    /// </summary>
    public static class AntiSlowmoController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value) Time.timeScale = 1f;
                Main.MelonLog.Msg($"Anti-Slowmo: {(value ? "ON" : "OFF")}");
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Anti-Slowmo controller initialized");
        }

        public static void Update()
        {
            if (!_enabled) return;
            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
