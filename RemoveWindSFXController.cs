using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    public static class RemoveWindSFXController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_enabled)
                    DisableAllExistingWindSFX();
                else
                    EnableAllExistingWindSFX();
            }
        }

        public static void Initialize() { }

        private static void DisableAllExistingWindSFX()
        {
            foreach (var wind in Object.FindObjectsOfType<WindBuffetSFX>())
            {
                wind.enabled = false;
            }
        }

        private static void EnableAllExistingWindSFX()
        {
            foreach (var wind in Object.FindObjectsOfType<WindBuffetSFX>())
            {
                wind.enabled = true;
            }
        }

        [HarmonyPatch(typeof(WindBuffetSFX), "Awake")]
        public static class WindBuffetSFXAwakePatch
        {
            static bool Prefix()
            {
                return !_enabled;
            }
        }

        [HarmonyPatch(typeof(WindBuffetSFX), "LateUpdate")]
        public static class WindBuffetSFXLateUpdatePatch
        {
            static bool Prefix()
            {
                return !_enabled;
            }
        }
    }
}
