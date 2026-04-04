using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppInterop.Runtime.InteropTypes;
using HarmonyLib;
using System;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Ragdolls the player from gun recoil after a configurable delay.
    /// High-recoil weapons knock the player back when they fire.
    /// </summary>
    public static class RecoilRagdollController
    {
        // ───── Settings ─────
        private static bool _enabled = false;
        private static float _delay = 0.05f;          // seconds after firing before ragdoll triggers
        private static float _cooldown = 1.0f;        // min seconds between recoil ragdolls
        private static bool _dropGun = false;          // drop the gun when ragdolled
        private static float _forceMultiplier = 1.0f;  // scales the knockback impulse

        // ───── Internal State ─────
        private static float _pendingRagdollTime = -1f;
        private static float _nextAllowedTime = 0f;
        private static Vector3 _pendingForceDir = Vector3.zero;

        // ───── Properties ─────
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value) _pendingRagdollTime = -1f;
            }
        }
        public static float Delay { get => _delay; set => _delay = Mathf.Clamp(value, 0f, 2f); }
        public static float Cooldown { get => _cooldown; set => _cooldown = Mathf.Clamp(value, 0.1f, 10f); }
        public static bool DropGun { get => _dropGun; set => _dropGun = value; }
        public static float ForceMultiplier { get => _forceMultiplier; set => _forceMultiplier = Mathf.Clamp(value, 0f, 10f); }

        public static void OnLevelUnloaded()
        {
            _pendingRagdollTime = -1f;
            _nextAllowedTime = 0f;
        }

        /// <summary>Called from main Update loop every frame.</summary>
        public static void Update()
        {
            if (!_enabled) return;
            if (_pendingRagdollTime < 0f) return;

            if (Time.time >= _pendingRagdollTime)
            {
                _pendingRagdollTime = -1f;
                _nextAllowedTime = Time.time + _cooldown;
                try
                {
                    var physRig = Player.PhysicsRig;
                    if (physRig == null) return;

                    // Trigger ragdoll via RagdollController if it's enabled, otherwise do it directly
                    if (RagdollController.Enabled)
                    {
                        RagdollController.ManualRagdoll(physRig);
                    }
                    else
                    {
                        // Minimal ragdoll: disable ball loco + physical legs
                        physRig.RagdollRig();
                        // Auto-recover after a short time
                    }

                    // Apply knockback force opposite to firing direction
                    if (_forceMultiplier > 0f && _pendingForceDir.sqrMagnitude > 0.01f)
                    {
                        var pelvisRb = physRig.torso?.rbPelvis;
                        if (pelvisRb != null)
                        {
                            pelvisRb.AddForce(_pendingForceDir * _forceMultiplier * 8f, ForceMode.Impulse);
                        }
                    }

                    if (_dropGun)
                    {
                        TryDrop(Player.LeftHand);
                        TryDrop(Player.RightHand);
                    }
                }
                catch { }
            }
        }

        /// <summary>Called from Gun.Fire Harmony patch to queue a recoil ragdoll.</summary>
        public static void OnGunFired(Gun gun)
        {
            if (!_enabled) return;
            if (Time.time < _nextAllowedTime) return;
            if (_pendingRagdollTime >= 0f) return; // already queued

            try
            {
                // Compute knockback direction: opposite to where the gun barrel is pointing
                var muzzle = gun.firePointTransform;
                if (muzzle != null)
                {
                    _pendingForceDir = -muzzle.forward;
                }
                else
                {
                    _pendingForceDir = Vector3.zero;
                }
            }
            catch
            {
                _pendingForceDir = Vector3.zero;
            }

            _pendingRagdollTime = Time.time + _delay;
        }

        private static void TryDrop(Hand hand)
        {
            try
            {
                if (hand == null || !hand.HasAttachedObject()) return;
                var grip = ((Il2CppObjectBase)hand.AttachedReceiver).TryCast<Grip>();
                if (grip != null)
                    grip.ForceDetach(true);
            }
            catch { }
        }
    }
}
