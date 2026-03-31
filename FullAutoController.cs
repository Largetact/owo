using MelonLoader;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Controller that delegates to FullAutoGunSystem for Full Auto functionality
    /// </summary>
    public static class FullAutoController
    {
        private static bool fullAutoEnabled = false;
        private static float _fireRateMultiplier = 1f;

        public static bool IsFullAutoEnabled
        {
            get => fullAutoEnabled;
            set
            {
                if (value != fullAutoEnabled)
                {
                    fullAutoEnabled = value;
                    FullAutoGunSystem.SetEnabled(value);
                }
            }
        }

        public static float FireRateMultiplier
        {
            get => _fireRateMultiplier;
            set
            {
                _fireRateMultiplier = Mathf.Clamp(value, 1f, 1000f);
                FullAutoGunSystem.FireRateMultiplier = _fireRateMultiplier;
            }
        }

        // Legacy accessor for settings compatibility
        public static float FireRate
        {
            get => _fireRateMultiplier;
            set => FireRateMultiplier = value;
        }

        public static void Initialize()
        {
            FullAutoGunSystem.Initialize();
            Main.MelonLog.Msg("Full Auto controller initialized");
        }

        public static void Update()
        {
            // Delegate to the gun system
            FullAutoGunSystem.Update();
        }
    }
}
