using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using System;
using System.Collections;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Explosive Impact: like Ground Slam but triggers when the player impacts
    /// ANY collider (walls, ceilings, objects, ground) at speed.
    /// Uses velocity change detection on the pelvis rigidbody.
    /// </summary>
    public static class ExplosiveImpactController
    {
        // Explosion barcodes (shared with ExplosivePunch/GroundPound)
        private const string NormalExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionMissile";
        private const string SuperExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionTimedNuke";
        private const string BlackFlashBarcode = "Curiosity.BlackFlash.Spawnable.BlackFlash";
        private const string TinyExplosiveBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionSmallBigDamage";
        private const string BoomBarcode = "Curiosity.BlackFlash.Spawnable.Boom";
        private const string SmashBoneBarcode = "Lakatrazz.FusionContent.Spawnable.DeathExplosion";

        // ═══════════════════════════════════════════
        // SETTINGS
        // ═══════════════════════════════════════════
        private static bool _enabled = false;
        private static float _velocityThreshold = 8f;
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

        // ═══════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════
        private static Vector3 _prevVelocity = Vector3.zero;
        private static bool _hadVelocity = false;
        private static float _lastTriggerTime = 0f;

        // ═══════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Explosive Impact {(value ? "ENABLED" : "DISABLED")}");
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
            Main.MelonLog.Msg("Explosive Impact Controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            _prevVelocity = Vector3.zero;
            _hadVelocity = false;
        }

        public static void Update()
        {
            if (!_enabled) return;

            if (_explosionType == ExplosionType.NONE && !_smashBoneEnabled && !_cosmeticEnabled)
                return;

            try
            {
                DetectImpact();
            }
            catch { }
        }

        private static void DetectImpact()
        {
            var physRig = Player.PhysicsRig;
            if (physRig == null) return;

            var pelvisRb = physRig.torso?.rbPelvis;
            if (pelvisRb == null) return;

            Vector3 currentVel = pelvisRb.velocity;

            if (_hadVelocity)
            {
                // Compute instantaneous velocity change (deceleration on impact)
                float prevSpeed = _prevVelocity.magnitude;
                float currentSpeed = currentVel.magnitude;
                float speedDrop = prevSpeed - currentSpeed;

                // Also check the actual delta-V magnitude for direction changes (wall bounces)
                float deltaV = (currentVel - _prevVelocity).magnitude;
                float impactMagnitude = Mathf.Max(speedDrop, deltaV);

                if (impactMagnitude >= _velocityThreshold && prevSpeed >= _velocityThreshold * 0.5f)
                {
                    if (Time.time - _lastTriggerTime >= _cooldown)
                    {
                        // Confirm we actually hit a non-player collider (not our own rig)
                        Vector3 impactDir = _prevVelocity.normalized;
                        Vector3 origin = pelvisRb.transform.position;

                        if (!ConfirmExternalCollision(origin, impactDir))
                        {
                            // No external collider found — likely self-collision, skip
                            _prevVelocity = currentVel;
                            _hadVelocity = true;
                            return;
                        }

                        _lastTriggerTime = Time.time;

                        Vector3 spawnPos = origin + impactDir * 0.3f;

                        TriggerExplosion(spawnPos, impactDir);
                    }
                }
            }

            _prevVelocity = currentVel;
            _hadVelocity = true;
        }

        /// <summary>
        /// Raycast in the travel direction (and a few nearby directions) to confirm
        /// we hit an external collider rather than our own rig.
        /// </summary>
        private static bool ConfirmExternalCollision(Vector3 origin, Vector3 dir)
        {
            Transform rigRoot = null;
            try
            {
                var rm = Player.RigManager;
                if (rm != null) rigRoot = ((Component)rm).transform;
            }
            catch { }

            // Check main direction + a small spread
            float castDist = 1.5f;
            if (CastHitsExternal(origin, dir, castDist, rigRoot)) return true;
            if (CastHitsExternal(origin, -dir, castDist, rigRoot)) return true;
            if (CastHitsExternal(origin, Vector3.down, castDist, rigRoot)) return true;

            return false;
        }

        private static bool CastHitsExternal(Vector3 origin, Vector3 dir, float dist, Transform rigRoot)
        {
            if (dir.sqrMagnitude < 0.01f) return false;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null) return false;
                // Check if the hit collider belongs to our own rig
                if (rigRoot != null && hit.collider.transform.IsChildOf(rigRoot))
                    return false;
                return true;
            }
            return false;
        }

        private static void TriggerExplosion(Vector3 spawnPos, Vector3 impactDir)
        {
            try
            {
                Quaternion spawnRot = Quaternion.identity;

                // Build matrix layout axes from impact direction
                Vector3 forward = impactDir.sqrMagnitude > 0.01f ? impactDir : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                Vector3 up = Vector3.Cross(forward, right).normalized;

                string explosionBarcode = GetExplosionBarcode();

                Quaternion smashRot = Quaternion.LookRotation(_smashBoneFlip ? Vector3.up : Vector3.down);
                Quaternion cosRot = _cosmeticFlip ? Quaternion.LookRotation(Vector3.up) : Quaternion.identity;

                if (explosionBarcode != null)
                    SpawnWithMatrix(explosionBarcode, 1, spawnPos, spawnRot, right, up);

                if (_smashBoneEnabled)
                    SpawnWithMatrix(SmashBoneBarcode, _smashBoneCount, spawnPos, smashRot, right, up);

                if (_cosmeticEnabled && !string.IsNullOrEmpty(_cosmeticBarcode))
                    SpawnWithMatrix(_cosmeticBarcode, _cosmeticCount, spawnPos, cosRot, right, up);

                Main.MelonLog.Msg($"Explosive Impact! Dir: {impactDir:F1}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Explosive Impact error: {ex.Message}");
            }
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
