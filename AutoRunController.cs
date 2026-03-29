using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Auto-Run — continuously sets traversal state to running.
    /// Based on FusionProtector's ApplyAutoRun / SmashBones pattern.
    /// </summary>
    public static class AutoRunController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static void Initialize() { }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var remapRig = rigManager.remapHeptaRig;
                if (remapRig == null) return;

                // If walking/idle (state 0), set to running (state 1)
                if ((int)remapRig.travState == 0)
                    remapRig.travState = (RemapRig.TraversalState)1;
            }
            catch { }
        }
    }
}
