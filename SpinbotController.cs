using MelonLoader;
using HarmonyLib;
using UnityEngine;
using BoneLib;
using System;

namespace BonelabUtilityMod
{
    public enum SpinDirection
    {
        RIGHT,
        LEFT
    }

    /// <summary>
    /// Spinbot — Harmony patches LabFusion's RigPose.ReadSkeleton() to inject
    /// spin rotation into the outgoing TrackedPlayspace quaternion.
    /// Only the network pose data is modified; the local player is never touched.
    /// Remote players see you spinning; you see nothing.
    /// </summary>
    public static class SpinbotController
    {
        private static bool _enabled = false;
        private static float _speed = 720f;
        private static SpinDirection _direction = SpinDirection.RIGHT;

        // Accumulated spin angle (degrees, ever-increasing while enabled)
        private static float _totalSpinAngle = 0f;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (!value) _totalSpinAngle = 0f;
                _enabled = value;
                Main.MelonLog.Msg($"Spinbot {(value ? "ON" : "OFF")} ({_speed}°/s {_direction})");
            }
        }

        public static float Speed
        {
            get => _speed;
            set => _speed = Mathf.Clamp(value, 10f, 7200f);
        }

        public static SpinDirection Direction
        {
            get => _direction;
            set => _direction = value;
        }

        /// <summary>
        /// Called from BonelabUtility.OnUpdate to advance the spin angle.
        /// No transforms are modified — only the angle accumulator.
        /// </summary>
        public static void Update()
        {
            if (!_enabled) return;
            float sign = _direction == SpinDirection.RIGHT ? 1f : -1f;
            _totalSpinAngle += sign * _speed * Time.deltaTime;
        }

        /// <summary>
        /// Returns the current spin rotation to inject into the network pose.
        /// Called by the Harmony postfix on RigPose.ReadSkeleton.
        /// </summary>
        public static Quaternion GetSpinRotation()
        {
            if (!_enabled) return Quaternion.identity;
            return Quaternion.AngleAxis(_totalSpinAngle, Vector3.up);
        }

        public static void OnLevelUnloaded()
        {
            _totalSpinAngle = 0f;
        }
    }

    /// <summary>
    /// Harmony postfix on LabFusion's RigPose.ReadSkeleton().
    /// After Fusion reads the real TrackedPlayspace rotation, we multiply
    /// in the spinbot rotation. The modified quaternion gets serialized
    /// and sent to remote clients. Local player is never affected.
    /// </summary>
    [HarmonyPatch(typeof(LabFusion.Entities.RigPose), nameof(LabFusion.Entities.RigPose.ReadSkeleton))]
    public static class SpinbotReadSkeletonPatch
    {
        public static void Postfix(object __instance)
        {
            if (!SpinbotController.Enabled) return;

            try
            {
                var pose = __instance as LabFusion.Entities.RigPose;
                if (pose == null) return;

                // Multiply spin into the playspace rotation
                Quaternion spun = SpinbotController.GetSpinRotation() * pose.TrackedPlayspaceExpanded;
                pose.TrackedPlayspaceExpanded = spun;
                pose.TrackedPlayspace = LabFusion.Data.SerializedSmallQuaternion.Compress(spun);
            }
            catch { }
        }
    }
}
