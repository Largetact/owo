using BoneLib;
using HarmonyLib;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Ghost Mode — hides your avatar from other players on the network.
    /// Uses a Harmony postfix on LabFusion's RigPose.ReadSkeleton() to offset
    /// the network position deep underground so other players can't see you.
    /// Also hides local renderers so you can see through yourself.
    /// </summary>
    public static class GhostModeController
    {
        private static bool _enabled = false;

        // How far underground to offset the network rep (meters)
        private const float GHOST_OFFSET_Y = -9999f;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Ghost Mode: {(value ? "ON (invisible to all)" : "OFF")}");
                ApplyLocalGhostMode(value);
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Ghost Mode controller initialized");
        }

        /// <summary>
        /// Toggle local renderers so you can see through yourself.
        /// </summary>
        private static void ApplyLocalGhostMode(bool hide)
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var skinned = rigManager.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinned != null)
                    foreach (var smr in skinned)
                        if (smr != null) smr.enabled = !hide;

                var meshRenderers = rigManager.GetComponentsInChildren<MeshRenderer>(true);
                if (meshRenderers != null)
                    foreach (var mr in meshRenderers)
                        if (mr != null) mr.enabled = !hide;
            }
            catch { }
        }

        public static void OnLevelLoaded()
        {
            if (_enabled)
                ApplyLocalGhostMode(true);
        }

        private static float _lastApply = 0f;
        private const float REAPPLY_INTERVAL = 1f;

        public static void Update()
        {
            if (!_enabled) return;
            if (Time.time - _lastApply < REAPPLY_INTERVAL) return;
            _lastApply = Time.time;
            ApplyLocalGhostMode(true);
        }
    }

    /// <summary>
    /// Harmony postfix on LabFusion's RigPose.ReadSkeleton().
    /// After Fusion reads the skeleton pose, if ghost mode is on,
    /// offset the pelvis pose position deep underground so other
    /// players' representation of you is invisible.
    /// </summary>
    [HarmonyPatch(typeof(LabFusion.Entities.RigPose), nameof(LabFusion.Entities.RigPose.ReadSkeleton))]
    public static class GhostModeReadSkeletonPatch
    {
        public static void Postfix(object __instance)
        {
            if (!GhostModeController.Enabled) return;

            try
            {
                var pose = __instance as LabFusion.Entities.RigPose;
                if (pose == null) return;

                // RigPose stores position in PelvisPose.Position (Vector3)
                // and PelvisPose.Velocity. We offset the position deep underground
                // so the network representation is invisible to other players.
                var pelvisPose = pose.PelvisPose;
                if (pelvisPose == null) return;

                // Use reflection since BodyPose fields may not be directly accessible
                var pelvisType = pelvisPose.GetType();

                var posField = pelvisType.GetField("Position")
                    ?? pelvisType.GetField("position");
                if (posField != null)
                {
                    Vector3 pos = (Vector3)posField.GetValue(pelvisPose);
                    pos.y -= 9999f;
                    posField.SetValue(pelvisPose, pos);
                }
                else
                {
                    var posProp = pelvisType.GetProperty("Position")
                        ?? pelvisType.GetProperty("position");
                    if (posProp != null && posProp.CanWrite)
                    {
                        Vector3 pos = (Vector3)posProp.GetValue(pelvisPose);
                        pos.y -= 9999f;
                        posProp.SetValue(pelvisPose, pos);
                    }
                }

                // Zero out velocity so prediction doesn't snap back
                var velField = pelvisType.GetField("Velocity")
                    ?? pelvisType.GetField("velocity");
                if (velField != null)
                {
                    velField.SetValue(pelvisPose, Vector3.zero);
                }
            }
            catch { }
        }
    }
}
