using MelonLoader;
using HarmonyLib;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    public static class AntiGrabController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
    }

    [HarmonyPatch(typeof(NativeMessageHandler), "Handle")]
    public static class AntiGrabPatch
    {
        public static bool Prefix(NativeMessageHandler __instance, ReceivedMessage received)
        {
            if (!AntiGrabController.Enabled)
                return true;

            if (__instance is PlayerRepGrabMessage)
            {
                try
                {
                    PlayerRepGrabData data = received.ReadData<PlayerRepGrabData>();
                    var grip = data.GetGrip();
                    if (grip != null)
                    {
                        GameObject gripRoot = ((Component)grip).gameObject.transform.root.gameObject;
                        RigManager localRig = Player.RigManager;
                        if (localRig != null && gripRoot == ((Component)localRig).gameObject)
                        {
                            return false;
                        }
                    }
                }
                catch { }
            }

            return true;
        }
    }
}
