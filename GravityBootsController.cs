using System;
using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Gravity Boots: walk on walls and ceilings by redirecting the player's
    /// gravity toward the nearest surface. Uses raycasts from the player's feet
    /// to detect surfaces and smoothly rotates the player's "up" vector to match
    /// the surface normal, then applies custom gravity forces.
    /// </summary>
    public static class GravityBootsController
    {
        private static bool _enabled = false;
        private static bool _active = false;

        // Tuning
        private static float _gravityStrength = 9.81f;
        private static float _surfaceDetectRange = 3f;
        private static float _rotationSpeed = 5f;
        private static float _stickForce = 20f;

        // Cached state
        private static Rigidbody[] _cachedRigidbodies = null;
        private static Vector3 _targetUp = Vector3.up;
        private static Vector3 _currentUp = Vector3.up;

        // Layer mask: everything except player layer
        private static int _raycastMask = ~0;

        // Reusable direction array (avoid per-frame allocation)
        private static readonly Vector3[] _rayDirections = new Vector3[6];

        // ── Properties for BoneMenu ──
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!_enabled && _active)
                    Deactivate();
            }
        }

        public static float GravityStrength
        {
            get => _gravityStrength;
            set => _gravityStrength = Mathf.Clamp(value, 1f, 30f);
        }

        public static float SurfaceDetectRange
        {
            get => _surfaceDetectRange;
            set => _surfaceDetectRange = Mathf.Clamp(value, 1f, 10f);
        }

        public static float RotationSpeed
        {
            get => _rotationSpeed;
            set => _rotationSpeed = Mathf.Clamp(value, 1f, 20f);
        }

        public static float StickForce
        {
            get => _stickForce;
            set => _stickForce = Mathf.Clamp(value, 5f, 50f);
        }

        public static void OnLevelUnloaded()
        {
            _cachedRigidbodies = null;
            _targetUp = Vector3.up;
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var physRig = rigManager.physicsRig;
                if (physRig == null) return;

                var remapRig = rigManager.remapHeptaRig;
                if (remapRig == null) return;

                if (!_active)
                    Activate(physRig);

                // ── Surface detection via multi-directional raycasts ──
                Vector3 feetPos = physRig.feet != null
                    ? physRig.feet.transform.position
                    : physRig.rbFeet != null
                        ? physRig.rbFeet.transform.position
                        : rigManager.transform.position;

                Vector3 currentUp = _currentUp;

                // Cast in 6 directions relative to current orientation to find nearest surface
                Vector3 bestNormal = Vector3.zero;
                float bestDist = float.MaxValue;
                RaycastHit hit;

                // Reuse static array to avoid per-frame allocation
                _rayDirections[0] = -currentUp;
                _rayDirections[1] = currentUp;
                _rayDirections[2] = Vector3.Cross(currentUp, Vector3.forward).normalized;
                _rayDirections[3] = -_rayDirections[2];
                _rayDirections[4] = Vector3.Cross(Vector3.right, currentUp).normalized;
                _rayDirections[5] = -_rayDirections[4];

                foreach (var dir in _rayDirections)
                {
                    if (dir.sqrMagnitude < 0.01f) continue;
                    if (Physics.Raycast(feetPos, dir, out hit, _surfaceDetectRange, _raycastMask))
                    {
                        if (hit.distance < bestDist)
                        {
                            bestDist = hit.distance;
                            bestNormal = hit.normal;
                        }
                    }
                }

                // If no surface found, fall back to world up (normal gravity)
                if (bestNormal.sqrMagnitude < 0.01f)
                    _targetUp = Vector3.up;
                else
                    _targetUp = bestNormal;

                // ── Smooth rotation of player's up vector ──
                _currentUp = Vector3.Slerp(_currentUp, _targetUp, Time.deltaTime * _rotationSpeed);

                // Apply to the rig's orientation system
                remapRig._playerUp = _currentUp;

                // ── Apply custom gravity force to all rigidbodies ──
                Vector3 gravityForce = -_currentUp * _gravityStrength;

                if (_cachedRigidbodies != null)
                {
                    foreach (var rb in _cachedRigidbodies)
                    {
                        if (rb == null) continue;
                        // Apply gravity as acceleration (mass-independent)
                        rb.AddForce(gravityForce, ForceMode.Acceleration);

                        // Extra "sticking" force when near a surface to prevent sliding off
                        if (bestDist < 1.5f && bestNormal.sqrMagnitude > 0.01f)
                        {
                            Vector3 stickDir = -bestNormal;
                            rb.AddForce(stickDir * _stickForce * (1f - bestDist / 1.5f), ForceMode.Acceleration);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"GravityBoots error: {ex.Message}");
            }
        }

        private static void Activate(PhysicsRig physRig)
        {
            try
            {
                // Cache all rigidbodies and disable Unity's built-in gravity
                var rigGo = ((Component)physRig).gameObject;
                _cachedRigidbodies = rigGo.GetComponentsInChildren<Rigidbody>();

                foreach (var rb in _cachedRigidbodies)
                {
                    if (rb != null)
                        rb.useGravity = false;
                }

                _currentUp = Vector3.up;
                _targetUp = Vector3.up;
                _active = true;

                Main.MelonLog.Msg("Gravity Boots activated");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"GravityBoots activate error: {ex.Message}");
            }
        }

        private static void Deactivate()
        {
            try
            {
                // Restore Unity gravity and reset player orientation
                if (_cachedRigidbodies != null)
                {
                    foreach (var rb in _cachedRigidbodies)
                    {
                        if (rb != null)
                            rb.useGravity = true;
                    }
                }

                // Reset player up to world up
                try
                {
                    var rigManager = Player.RigManager;
                    if (rigManager?.remapHeptaRig != null)
                        rigManager.remapHeptaRig._playerUp = Vector3.up;
                }
                catch { }

                _cachedRigidbodies = null;
                _currentUp = Vector3.up;
                _targetUp = Vector3.up;
                _active = false;

                Main.MelonLog.Msg("Gravity Boots deactivated");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"GravityBoots deactivate error: {ex.Message}");
            }
        }

        public static void Toggle()
        {
            Enabled = !Enabled;
            NotificationHelper.Send(
                _enabled ? NotificationType.Success : NotificationType.Warning,
                $"Gravity Boots {(_enabled ? "ON" : "OFF")}"
            );
        }
    }
}
