using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppInterop.Runtime.InteropTypes;
using System;

namespace BonelabUtilityMod
{
    public enum SpinDirection
    {
        RIGHT,
        LEFT
    }

    /// <summary>
    /// Spinbot — spins the player’s playspace yaw (vrRoot) so Fusion syncs
    /// the spin to other players.
    ///
    /// Approach:  Apply the accumulated spin to vrRoot in Update() (so Fusion
    /// reads the spun rotation during its own Update tick), then remove it
    /// in LateUpdate() (which runs after ALL Updates but BEFORE rendering).
    /// The local camera renders with an unspun vrRoot, so the player sees
    /// nothing.  Works with both real VR and FlatPlayer’s MockHMD because
    /// we never touch the camera or XRHmd — only vrRoot.
    /// </summary>
    public static class SpinbotController
    {
        private static bool _enabled = false;
        private static float _speed = 720f;
        private static SpinDirection _direction = SpinDirection.RIGHT;

        // Accumulated spin angle (ever-increasing while enabled).
        // This is the total degrees of spin that remote players see.
        private static float _totalSpinAngle = 0f;

        // Whether the spin is currently applied to vrRoot (applied in Update,
        // removed in LateUpdate).
        private static bool _spinApplied = false;

        private static Transform _vrRoot = null;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                // If disabling while spin is on the transform, undo it now
                if (_enabled && !value)
                {
                    if (_spinApplied && _vrRoot != null)
                    {
                        try { _vrRoot.Rotate(Vector3.up, -_totalSpinAngle, Space.World); } catch { }
                        _spinApplied = false;
                    }
                    _totalSpinAngle = 0f;
                }
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
        /// Called from BonelabUtility.OnUpdate.
        /// Applies the full accumulated spin to vrRoot so that Fusion’s
        /// Update reads spun TrackedPlayspace rotation and syncs it.
        /// </summary>
        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                if (_vrRoot == null)
                {
                    var rig = Player.RigManager;
                    if (rig == null) return;
                    var ocr = ((Il2CppObjectBase)rig.ControllerRig).TryCast<OpenControllerRig>();
                    if (ocr == null || ocr.vrRoot == null) return;
                    _vrRoot = ((Component)ocr.vrRoot).transform;
                }

                float sign = _direction == SpinDirection.RIGHT ? 1f : -1f;
                float frameAngle = sign * _speed * Time.deltaTime;
                _totalSpinAngle += frameAngle;

                // Apply the full accumulated spin as a world-space Y rotation.
                // Since LateUpdate removed it last frame, vrRoot is currently
                // “clean” (no spin).  We add the full total so Fusion reads
                // the correct absolute rotation.
                _vrRoot.Rotate(Vector3.up, _totalSpinAngle, Space.World);
                _spinApplied = true;
            }
            catch { }
        }

        /// <summary>
        /// Called from BonelabUtility.OnLateUpdate.
        /// Runs AFTER all Updates (including Fusion’s) but BEFORE rendering.
        /// Removes the spin from vrRoot so the local camera renders normally.
        /// Because both the spin and the undo are pure Y-axis world rotations,
        /// any smooth-turn or other rotation applied between Update and
        /// LateUpdate is preserved (Y-axis quaternions commute).
        /// </summary>
        public static void LateUpdate()
        {
            if (!_spinApplied) return;

            try
            {
                if (_vrRoot != null)
                    _vrRoot.Rotate(Vector3.up, -_totalSpinAngle, Space.World);
            }
            catch { }

            _spinApplied = false;
        }

        public static void OnLevelUnloaded()
        {
            _totalSpinAngle = 0f;
            _vrRoot = null;
            _spinApplied = false;
        }
    }
}
