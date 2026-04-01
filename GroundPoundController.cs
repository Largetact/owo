using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using System;
using System.Collections;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    public static class GroundPoundController
    {
        // Explosion barcodes (same as ExplosivePunch)
        private const string NormalExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionMissile";
        private const string SuperExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionTimedNuke";
        private const string BlackFlashBarcode = "Curiosity.BlackFlash.Spawnable.BlackFlash";
        private const string TinyExplosiveBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionSmallBigDamage";
        private const string BoomBarcode = "Curiosity.BlackFlash.Spawnable.Boom";
        private const string SmashBoneBarcode = "Lakatrazz.FusionContent.Spawnable.DeathExplosion";

        // ═══════════════════════════════════════════════════
        // SETTINGS
        // ═══════════════════════════════════════════════════
        private static bool _enabled = false;
        private static float _velocityThreshold = 5f;
        private static float _cooldown = 0.5f;
        private static float _spawnDelay = 0f;

        // Explosion type
        private static ExplosionType _explosionType = ExplosionType.Normal;
        private static string _customBarcode = "";

        // SmashBone
        private static bool _smashBoneEnabled = false;
        private static int _smashBoneCount = 1;
        private static bool _smashBoneFlip = false;

        // Cosmetic
        private static bool _cosmeticEnabled = false;
        private static string _cosmeticBarcode = "";
        private static int _cosmeticCount = 1;
        private static bool _cosmeticFlip = false;

        // Matrix
        private static int _matrixCount = 1;
        private static float _matrixSpacing = 0.5f;
        private static MatrixMode _matrixMode = MatrixMode.SQUARE;

        // ═══════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════
        private static PhysGrounder _grounder = null;
        private static bool _wasAirborne = false;
        private static float _peakDownwardSpeed = 0f;
        private static float _lastTriggerTime = 0f;

        // ═══════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                Main.MelonLog.Msg($"Ground Slam {(value ? "ENABLED" : "DISABLED")}");
            }
        }
        public static float VelocityThreshold { get => _velocityThreshold; set => _velocityThreshold = Mathf.Clamp(value, 1f, 50f); }
        public static float Cooldown { get => _cooldown; set => _cooldown = Mathf.Clamp(value, 0.05f, 5f); }
        public static float SpawnDelay { get => _spawnDelay; set => _spawnDelay = Mathf.Clamp(value, 0f, 2f); }
        public static ExplosionType SelectedExplosion { get => _explosionType; set => _explosionType = value; }
        public static string CustomBarcode { get => _customBarcode; set => _customBarcode = value ?? ""; }
        public static bool SmashBoneEnabled { get => _smashBoneEnabled; set => _smashBoneEnabled = value; }
        public static int SmashBoneCount { get => _smashBoneCount; set => _smashBoneCount = Mathf.Clamp(value, 1, 20); }
        public static bool SmashBoneFlip { get => _smashBoneFlip; set => _smashBoneFlip = value; }
        public static bool CosmeticEnabled { get => _cosmeticEnabled; set => _cosmeticEnabled = value; }
        public static string CosmeticBarcode { get => _cosmeticBarcode; set => _cosmeticBarcode = value ?? ""; }
        public static int CosmeticCount { get => _cosmeticCount; set => _cosmeticCount = Mathf.Clamp(value, 1, 20); }
        public static bool CosmeticFlip { get => _cosmeticFlip; set => _cosmeticFlip = value; }
        public static int MatrixCount { get => _matrixCount; set => _matrixCount = Mathf.Clamp(value, 1, 25); }
        public static float MatrixSpacing { get => _matrixSpacing; set => _matrixSpacing = Mathf.Clamp(value, 0.1f, 10f); }
        public static MatrixMode SelectedMatrixMode { get => _matrixMode; set => _matrixMode = value; }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Ground Slam Controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            _grounder = null;
            _wasAirborne = false;
            _peakDownwardSpeed = 0f;
        }

        public static void Update()
        {
            if (!_enabled) return;

            // Need at least one effect to be useful
            if (_explosionType == ExplosionType.NONE && !_smashBoneEnabled && !_cosmeticEnabled)
                return;

            // Safety: both grips + both triggers must be held
            if (!AreBothGripTriggersHeld())
                return;

            try
            {
                DetectGroundSlam();
            }
            catch { }
        }

        private static bool IsGrounded()
        {
            try
            {
                if (_grounder == null)
                {
                    var physRig = Player.PhysicsRig;
                    if (physRig != null && physRig.feet != null)
                        _grounder = physRig.feet.GetComponent<PhysGrounder>();
                }
                if (_grounder != null)
                    return _grounder.isGrounded;
            }
            catch { _grounder = null; }
            return false;
        }

        private static float GetDownwardSpeed()
        {
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return 0f;

                // Use the pelvis rigidbody for a stable velocity reading
                var pelvisRb = physRig.torso?.rbPelvis;
                if (pelvisRb != null)
                {
                    // Downward speed = negative Y velocity
                    float downSpeed = -pelvisRb.velocity.y;
                    return downSpeed > 0f ? downSpeed : 0f;
                }
            }
            catch { }
            return 0f;
        }

        private static bool AreBothGripTriggersHeld()
        {
            try
            {
                bool leftTrigger = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.5f;
                bool leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger") > 0.5f;
                bool rightTrigger = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.5f;
                bool rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger") > 0.5f;
                return leftTrigger && leftGrip && rightTrigger && rightGrip;
            }
            catch { }
            return false;
        }

        private static void DetectGroundSlam()
        {
            bool grounded = IsGrounded();

            if (!grounded)
            {
                // Airborne — track peak downward speed
                _wasAirborne = true;
                float downSpeed = GetDownwardSpeed();
                if (downSpeed > _peakDownwardSpeed)
                    _peakDownwardSpeed = downSpeed;
                return;
            }

            // On ground
            if (_wasAirborne)
            {
                // Just landed — check if we were falling fast enough
                _wasAirborne = false;

                if (_peakDownwardSpeed >= _velocityThreshold)
                {
                    // Check cooldown
                    if (Time.time - _lastTriggerTime >= _cooldown)
                    {
                        _lastTriggerTime = Time.time;
                        TriggerGroundSlam();
                    }
                }

                _peakDownwardSpeed = 0f;
            }
        }

        private static void TriggerGroundSlam()
        {
            try
            {
                // Spawn at feet position
                Vector3 spawnPos = GetFeetPosition();
                Quaternion spawnRot = Quaternion.identity;

                // Direction vectors for matrix layout (horizontal ring around impact point)
                Vector3 forward = Vector3.forward;
                Vector3 right = Vector3.right;
                Vector3 up = Vector3.up;

                // Get explosion barcode
                string explosionBarcode = GetExplosionBarcode();

                // SmashBone rotation (faces up or down)
                Vector3 smashDir = _smashBoneFlip ? Vector3.up : Vector3.down;
                Quaternion smashRot = Quaternion.LookRotation(smashDir);

                // Cosmetic rotation
                Quaternion cosRot = _cosmeticFlip ? Quaternion.LookRotation(Vector3.up) : Quaternion.identity;

                // Spawn explosion
                if (explosionBarcode != null)
                {
                    SpawnWithMatrix(explosionBarcode, 1, spawnPos, spawnRot, right, up);
                }

                // Spawn SmashBone
                if (_smashBoneEnabled)
                {
                    SpawnWithMatrix(SmashBoneBarcode, _smashBoneCount, spawnPos, smashRot, right, up);
                }

                // Spawn Cosmetic
                if (_cosmeticEnabled && !string.IsNullOrEmpty(_cosmeticBarcode))
                {
                    SpawnWithMatrix(_cosmeticBarcode, _cosmeticCount, spawnPos, cosRot, right, up);
                }

                Main.MelonLog.Msg($"Ground Slam! Speed: {_peakDownwardSpeed:F1}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Ground Slam error: {ex.Message}");
            }
        }

        private static Vector3 GetFeetPosition()
        {
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig != null && physRig.feet != null)
                {
                    var feetGo = physRig.feet.gameObject;
                    if (feetGo != null)
                        return feetGo.transform.position;
                }
                // Fallback: head position minus ~1.6m
                var head = Player.Head;
                if (head != null)
                    return head.position + Vector3.down * 1.6f;
            }
            catch { }
            return Vector3.zero;
        }

        private static string GetExplosionBarcode()
        {
            switch (_explosionType)
            {
                case ExplosionType.Custom:
                    return !string.IsNullOrEmpty(_customBarcode) ? _customBarcode : null;
                case ExplosionType.Boom: return BoomBarcode;
                case ExplosionType.BlackFlash: return BlackFlashBarcode;
                case ExplosionType.Tiny: return TinyExplosiveBarcode;
                case ExplosionType.Super: return SuperExplosionBarcode;
                case ExplosionType.Normal: return NormalExplosionBarcode;
                default: return null;
            }
        }

        private static void SpawnWithMatrix(string barcode, int count, Vector3 position, Quaternion rotation, Vector3 right, Vector3 up)
        {
            if (_matrixMode == MatrixMode.DISABLED || _matrixCount <= 1)
            {
                SpawnMultiple(barcode, count, position, rotation);
                return;
            }
            var offsets = CalculateMatrixOffsets(_matrixCount, _matrixSpacing, right, up, _matrixMode);
            foreach (var offset in offsets)
                SpawnMultiple(barcode, count, position + offset, rotation);
        }

        private static void SpawnMultiple(string barcode, int count, Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < count; i++)
            {
                if (_spawnDelay > 0f)
                    MelonCoroutines.Start(DelayedSpawn(barcode, position, rotation, _spawnDelay));
                else
                    ExplosivePunchController.SpawnEffect(barcode, position, rotation);
            }
        }

        private static IEnumerator DelayedSpawn(string barcode, Vector3 position, Quaternion rotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            ExplosivePunchController.SpawnEffect(barcode, position, rotation);
        }

        private static List<Vector3> CalculateMatrixOffsets(int count, float spacing, Vector3 right, Vector3 up, MatrixMode mode)
        {
            var offsets = new List<Vector3>();
            if (count <= 1 || mode == MatrixMode.DISABLED) { offsets.Add(Vector3.zero); return offsets; }

            if (mode == MatrixMode.CIRCLE)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = (2f * Mathf.PI * i) / count;
                    offsets.Add(right * (Mathf.Cos(angle) * spacing) + up * (Mathf.Sin(angle) * spacing));
                }
                return offsets;
            }

            // SQUARE mode
            if (count == 2)
            {
                offsets.Add(-right * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f);
            }
            else if (count == 3)
            {
                offsets.Add(up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else if (count == 4)
            {
                offsets.Add(-right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else
            {
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
                float halfGrid = (gridSize - 1) * spacing * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    int row = i / gridSize;
                    int col = i % gridSize;
                    offsets.Add(right * (col * spacing - halfGrid) + up * (row * spacing - halfGrid));
                }
            }
            return offsets;
        }
    }
}
