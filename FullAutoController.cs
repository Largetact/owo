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
        private static float fireRate = 600f; // Rounds per minute

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

        public static float FireRate
        {
            get => fireRate;
            set
            {
                fireRate = Mathf.Clamp(value, 60f, 2000f);
                FullAutoGunSystem.SetFireRate(fireRate);
            }
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
