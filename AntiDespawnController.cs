using MelonLoader;
using HarmonyLib;
using Il2CppSLZ.Marrow.VFX;

namespace BonelabUtilityMod
{
    public static class AntiDespawnController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
    }

    [HarmonyPatch(typeof(SpawnEffects), "CallDespawnEffect")]
    public static class AntiDespawnPatch
    {
        public static bool Prefix()
        {
            if (AntiDespawnController.Enabled)
                return false;
            return true;
        }
    }
}
