using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Collections;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Flight controller: hold both grip + trigger on BOTH controllers to fly.
    /// Speed scales with hand distance from body (further = faster, closer = slower).
    /// Direction is the average of both hands' forward vectors.
    /// Optional acceleration mode: speed ramps up exponentially while flying.
    /// </summary>
    public static class FlightController
    {
        private static bool _enabled = false;
        private static float _speedMultiplier = 1f;
        private static bool _accelerationEnabled = false;
        private static float _accelerationRate = 1.5f;
        private static bool _momentumEnabled = false;
        private static bool _lockOnEnabled = false;
        private static TargetFilter _lockOnFilter = TargetFilter.NEAREST;
        private static bool _lookAtTarget = false;
        private static bool _lookAtHead = false;

        // Cached rig state for gravity management
        private static bool _isFlying = false;
        private static Rigidbody[] _cachedRigidbodies = null;
        private static float[] _originalDrags = null;
        private static float _currentAccelSpeed = 0f;

        // ═══════════════════════════════════════════════════
        // EFFECT SYSTEM — Spawns effects at torso during flight
        // ═══════════════════════════════════════════════════
        private const string SmashBoneBarcode = "Lakatrazz.FusionContent.Spawnable.DeathExplosion";

        // Custom effect
        private static bool _effectEnabled = false;
        private static string _effectBarcode = "";

        // SmashBone effect
        private static bool _smashBoneEnabled = false;
        private static int _smashBoneCount = 1;
        private static bool _smashBoneFlip = false;

        // Cosmetic effect
        private static bool _cosmeticEnabled = false;
        private static string _cosmeticBarcode = "";
        private static int _cosmeticCount = 1;
        private static bool _cosmeticFlip = false;

        // Matrix spawn (count + gap for effects)
        private static int _effectMatrixCount = 1;
        private static float _effectMatrixSpacing = 0.5f;
        private static MatrixMode _effectMatrixMode = MatrixMode.SQUARE;

        // Effect timing
        private static float _effectSpawnDelay = 0f;
        private static float _effectSpawnInterval = 0f;
        private static float _lastEffectSpawnTime = 0f;

        // Transform offset
        private static Vector3 _effectOffset = Vector3.zero;

        // Orientation for effects
        private static bool _effectHandOriented = false;
        private static bool _effectUseLeftHand = false;

        // Tuning constants
        private const float BASE_SPEED = 25f;          // base flight speed at ~0.5m hand distance
        private const float MIN_HAND_DISTANCE = 0.05f; // hands very close to body → near-zero speed
        private const float MAX_HAND_DISTANCE = 1.2f;  // hands fully extended → max speed
        private const float NEUTRAL_DISTANCE = 0.4f;   // hand distance where speed stays constant (no accel/decel)
        private const float HOVER_DRAG = 5f;           // drag applied during flight to feel smooth
        private const float INPUT_THRESHOLD = 0.7f;    // analog threshold for grip/trigger
        private const float MAX_ACCEL_SPEED = 500f;    // cap so speed can't accumulate forever

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                bool wasEnabled = _enabled;
                _enabled = value;
                if (!value && wasEnabled)
                    StopFlying();
                Main.MelonLog.Msg($"Flight {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static float SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                _speedMultiplier = Mathf.Max(0.5f, value);
            }
        }

        public static bool AccelerationEnabled
        {
            get => _accelerationEnabled;
            set
            {
                _accelerationEnabled = value;
                Main.MelonLog.Msg($"Flight Acceleration {(value ? "ON" : "OFF")}");
            }
        }

        public static float AccelerationRate
        {
            get => _accelerationRate;
            set
            {
                _accelerationRate = Mathf.Clamp(value, 0.1f, 10f);
            }
        }

        public static bool MomentumEnabled
        {
            get => _momentumEnabled;
            set
            {
                _momentumEnabled = value;
                Main.MelonLog.Msg($"Flight Momentum {(value ? "ON" : "OFF")}");
            }
        }

        public static bool LockOnEnabled
        {
            get => _lockOnEnabled;
            set
            {
                _lockOnEnabled = value;
                Main.MelonLog.Msg($"Flight Lock-On: {(value ? "ON" : "OFF")}");
            }
        }

        public static TargetFilter LockOnFilter
        {
            get => _lockOnFilter;
            set
            {
                _lockOnFilter = value;
                Main.MelonLog.Msg($"Flight Lock-On Filter: {value}");
            }
        }

        public static bool LookAtTarget
        {
            get => _lookAtTarget;
            set
            {
                _lookAtTarget = value;
                Main.MelonLog.Msg($"Flight Look-At Target: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool LookAtHead
        {
            get => _lookAtHead;
            set
            {
                _lookAtHead = value;
                Main.MelonLog.Msg($"Flight Look-At: {(value ? "Head" : "Torso")}");
            }
        }

        // ═══════════════════════════════════════════════════
        // PROPERTIES — Effect system
        // ═══════════════════════════════════════════════════
        public static bool EffectEnabled { get => _effectEnabled; set => _effectEnabled = value; }
        public static string EffectBarcode { get => _effectBarcode; set => _effectBarcode = value ?? ""; }
        public static bool SmashBoneEnabled { get => _smashBoneEnabled; set => _smashBoneEnabled = value; }
        public static int SmashBoneCount { get => _smashBoneCount; set => _smashBoneCount = Mathf.Clamp(value, 1, 20); }
        public static bool SmashBoneFlip { get => _smashBoneFlip; set => _smashBoneFlip = value; }
        public static bool CosmeticEnabled { get => _cosmeticEnabled; set => _cosmeticEnabled = value; }
        public static string CosmeticBarcode { get => _cosmeticBarcode; set => _cosmeticBarcode = value ?? ""; }
        public static int CosmeticCount { get => _cosmeticCount; set => _cosmeticCount = Mathf.Clamp(value, 1, 20); }
        public static bool CosmeticFlip { get => _cosmeticFlip; set => _cosmeticFlip = value; }
        public static int EffectMatrixCount { get => _effectMatrixCount; set => _effectMatrixCount = Mathf.Clamp(value, 1, 25); }
        public static float EffectMatrixSpacing { get => _effectMatrixSpacing; set => _effectMatrixSpacing = Mathf.Clamp(value, 0.1f, 10f); }
        public static MatrixMode EffectMatrixMode { get => _effectMatrixMode; set => _effectMatrixMode = value; }
        public static float EffectSpawnDelay { get => _effectSpawnDelay; set => _effectSpawnDelay = Mathf.Clamp(value, 0f, 2f); }
        public static float EffectSpawnInterval { get => _effectSpawnInterval; set => _effectSpawnInterval = Mathf.Clamp(value, 0f, 5f); }
        public static float EffectOffsetX { get => _effectOffset.x; set => _effectOffset.x = Mathf.Clamp(value, -2f, 2f); }
        public static float EffectOffsetY { get => _effectOffset.y; set => _effectOffset.y = Mathf.Clamp(value, -2f, 2f); }
        public static float EffectOffsetZ { get => _effectOffset.z; set => _effectOffset.z = Mathf.Clamp(value, -2f, 2f); }
        public static bool EffectHandOriented { get => _effectHandOriented; set => _effectHandOriented = value; }
        public static bool EffectUseLeftHand { get => _effectUseLeftHand; set => _effectUseLeftHand = value; }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Flight controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            _cachedRigidbodies = null;
            _originalDrags = null;
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                UpdateFlight();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Flight error: {ex.Message}");
            }
        }

        private static void UpdateFlight()
        {
            // Read inputs from BOTH controllers
            bool leftGrip = false, leftTrigger = false;
            bool rightGrip = false, rightTrigger = false;

            try
            {
                leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger") > INPUT_THRESHOLD;
                leftTrigger = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > INPUT_THRESHOLD;
                rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger") > INPUT_THRESHOLD;
                rightTrigger = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > INPUT_THRESHOLD;
            }
            catch { }

            bool bothHandsActive = leftGrip && leftTrigger && rightGrip && rightTrigger;

            if (bothHandsActive)
            {
                PerformFlight();
            }
            else if (_isFlying)
            {
                StopFlying();
            }
        }

        private static void PerformFlight()
        {
            var rigManager = Player.RigManager;
            if (rigManager == null) return;

            var head = Player.Head;
            var leftHand = Player.LeftHand;
            var rightHand = Player.RightHand;
            if (head == null || leftHand == null || rightHand == null) return;

            // Calculate hand distances from head (proxy for body center)
            Vector3 headPos = head.position;
            float leftDist = Vector3.Distance(leftHand.transform.position, headPos);
            float rightDist = Vector3.Distance(rightHand.transform.position, headPos);
            float avgDist = (leftDist + rightDist) * 0.5f;

            // Normalize distance to a 0-1 speed factor
            float speedFactor = Mathf.InverseLerp(MIN_HAND_DISTANCE, MAX_HAND_DISTANCE, avgDist);
            float speed;

            if (_accelerationEnabled)
            {
                // Hand distance controls acceleration/deceleration:
                //   Hands far from body (> NEUTRAL_DISTANCE) → accelerate (linear ramp)
                //   Hands close to body (< NEUTRAL_DISTANCE) → decelerate (exponential decay)
                //   At NEUTRAL_DISTANCE → maintain current speed
                if (avgDist > NEUTRAL_DISTANCE)
                {
                    // Accelerate: 0–1 mapped from neutral to max
                    float accelInput = (avgDist - NEUTRAL_DISTANCE) / (MAX_HAND_DISTANCE - NEUTRAL_DISTANCE);
                    accelInput = Mathf.Clamp01(accelInput);
                    _currentAccelSpeed += accelInput * _accelerationRate * BASE_SPEED * Time.deltaTime;
                    _currentAccelSpeed = Mathf.Min(_currentAccelSpeed, MAX_ACCEL_SPEED);
                }
                else
                {
                    // Decelerate using exponential decay — feels proportional at any speed.
                    // Closer hands = stronger decay. At max decel (hands at body),
                    // speed decays by ~(rate * 3) per second exponentially.
                    float decelInput = (NEUTRAL_DISTANCE - avgDist) / (NEUTRAL_DISTANCE - MIN_HAND_DISTANCE);
                    decelInput = Mathf.Clamp01(decelInput);
                    float decayRate = decelInput * _accelerationRate * 3f;
                    _currentAccelSpeed *= Mathf.Exp(-decayRate * Time.deltaTime);
                    // Snap to zero when speed is negligible
                    if (_currentAccelSpeed < 0.1f)
                        _currentAccelSpeed = 0f;
                }
                speed = _currentAccelSpeed * _speedMultiplier;
            }
            else
            {
                speed = speedFactor * BASE_SPEED * _speedMultiplier;
            }

            // Direction: average of both hands' forward (or lock-on target)
            Vector3 flyDirection;
            if (_lockOnEnabled)
            {
                var dir = PlayerTargeting.GetDirectionToTarget(_lockOnFilter, headPos);
                flyDirection = dir ?? ((leftHand.transform.forward + rightHand.transform.forward) * 0.5f).normalized;
            }
            else
            {
                Vector3 leftFwd = leftHand.transform.forward;
                Vector3 rightFwd = rightHand.transform.forward;
                flyDirection = ((leftFwd + rightFwd) * 0.5f).normalized;
            }

            if (flyDirection.sqrMagnitude < 0.001f)
                flyDirection = head.forward;

            // Apply velocity to rig rigidbodies
            try
            {
                var rigGo = (rigManager as UnityEngine.Component)?.gameObject;
                if (rigGo == null) return;

                // Cache rigidbodies on first flight frame
                if (!_isFlying)
                {
                    _cachedRigidbodies = rigGo.GetComponentsInChildren<Rigidbody>();
                    _originalDrags = new float[_cachedRigidbodies.Length];
                    for (int i = 0; i < _cachedRigidbodies.Length; i++)
                    {
                        if (_cachedRigidbodies[i] != null)
                        {
                            _originalDrags[i] = _cachedRigidbodies[i].drag;
                        }
                    }
                    _isFlying = true;
                }

                Vector3 targetVelocity = flyDirection * speed;

                foreach (var rb in _cachedRigidbodies)
                {
                    if (rb != null && rb.mass > 1f)
                    {
                        // Counteract gravity + set flight velocity
                        rb.useGravity = false;
                        rb.drag = HOVER_DRAG;
                        rb.velocity = targetVelocity;
                    }
                }
            }
            catch { }

            // Rotate rig to face the lock-on target
            if (_lockOnEnabled && _lookAtTarget)
                RotateRigTowardTarget(rigManager);

            // Spawn effects at torso during flight
            SpawnFlightEffects(flyDirection);
        }

        private static void StopFlying()
        {
            if (!_isFlying) return;

            try
            {
                if (_cachedRigidbodies != null)
                {
                    for (int i = 0; i < _cachedRigidbodies.Length; i++)
                    {
                        if (_cachedRigidbodies[i] != null && _cachedRigidbodies[i].mass > 1f)
                        {
                            _cachedRigidbodies[i].useGravity = true;
                            _cachedRigidbodies[i].drag = (_originalDrags != null && i < _originalDrags.Length)
                                ? _originalDrags[i] : 0f;
                        }
                    }
                }
            }
            catch { }

            _cachedRigidbodies = null;
            _originalDrags = null;
            _isFlying = false;
            // Preserve accumulated speed if momentum is on, otherwise reset
            if (!_momentumEnabled)
                _currentAccelSpeed = 0f;
        }

        private static void RotateRigTowardTarget(RigManager rigManager)
        {
            try
            {
                var head = Player.Head;
                if (head == null) return;

                var dir = PlayerTargeting.GetDirectionToTarget(_lockOnFilter, head.position, _lookAtHead);
                if (dir == null) return;

                Vector3 flatDir = new Vector3(dir.Value.x, 0f, dir.Value.z);
                if (flatDir.sqrMagnitude < 0.001f) return;

                Vector3 headFlat = new Vector3(head.forward.x, 0f, head.forward.z);
                if (headFlat.sqrMagnitude < 0.001f) return;

                float yawAngle = Vector3.SignedAngle(headFlat.normalized, flatDir.normalized, Vector3.up);

                var rigTransform = (rigManager as UnityEngine.Component)?.transform;
                if (rigTransform != null)
                    rigTransform.RotateAround(head.position, Vector3.up, yawAngle);

                if (_lookAtHead && rigTransform != null)
                {
                    Vector3 currentFwd = head.forward;
                    float currentPitch = Mathf.Asin(Mathf.Clamp(currentFwd.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float targetPitch = Mathf.Asin(Mathf.Clamp(dir.Value.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float pitchDelta = targetPitch - currentPitch;
                    if (Mathf.Abs(pitchDelta) > 0.5f)
                    {
                        Vector3 rightAxis = Vector3.Cross(Vector3.up, flatDir.normalized).normalized;
                        if (rightAxis.sqrMagnitude > 0.001f)
                            rigTransform.RotateAround(head.position, rightAxis, pitchDelta);
                    }
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        // EFFECT SPAWNING — Spawns at torso during flight
        // ═══════════════════════════════════════════════════
        private static void SpawnFlightEffects(Vector3 flyDirection)
        {
            bool anyEffect = _effectEnabled || _smashBoneEnabled || _cosmeticEnabled;
            if (!anyEffect) return;

            // Check spawn interval
            if (_effectSpawnInterval > 0f && Time.time - _lastEffectSpawnTime < _effectSpawnInterval)
                return;
            _lastEffectSpawnTime = Time.time;

            try
            {
                var head = Player.Head;
                if (head == null) return;

                // Torso is roughly below head
                Vector3 torsoPos = head.position + Vector3.down * 0.35f;

                // Apply transform offset in orientation-local space
                Quaternion orientationRot = GetEffectOrientationRotation();
                torsoPos += orientationRot * _effectOffset;

                // Calculate rotation based on effect orientation mode
                Quaternion spawnRot = orientationRot;

                // SmashBone rotation
                Vector3 smashDir = _smashBoneFlip ? -flyDirection : flyDirection;
                Quaternion smashRot = flyDirection.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(smashDir)
                    : Quaternion.identity;

                // Cosmetic rotation
                Quaternion cosRot = _cosmeticFlip ? Quaternion.LookRotation(flyDirection) : spawnRot;

                // Spawn custom effect
                if (_effectEnabled && !string.IsNullOrEmpty(_effectBarcode))
                {
                    SpawnWithMatrix(_effectBarcode, 1, torsoPos, spawnRot, _effectSpawnDelay, flyDirection);
                }

                // Spawn SmashBone
                if (_smashBoneEnabled)
                {
                    SpawnWithMatrix(SmashBoneBarcode, _smashBoneCount, torsoPos, smashRot, _effectSpawnDelay, flyDirection);
                }

                // Spawn Cosmetic
                if (_cosmeticEnabled && !string.IsNullOrEmpty(_cosmeticBarcode))
                {
                    SpawnWithMatrix(_cosmeticBarcode, _cosmeticCount, torsoPos, cosRot, _effectSpawnDelay, flyDirection);
                }
            }
            catch { }
        }

        private static Quaternion GetEffectOrientationRotation()
        {
            if (_effectHandOriented)
            {
                var hand = _effectUseLeftHand ? Player.LeftHand : Player.RightHand;
                if (hand != null)
                    return hand.transform.rotation;
            }

            var head = Player.Head;
            if (head != null)
                return Quaternion.LookRotation(head.forward, Vector3.up);

            return Quaternion.identity;
        }

        private static void SpawnMultiple(string barcode, int count, Vector3 position, Quaternion rotation, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                if (delay > 0f)
                    MelonCoroutines.Start(DelayedSpawn(barcode, position, rotation, delay));
                else
                    ExplosivePunchController.SpawnEffect(barcode, position, rotation);
            }
        }

        private static IEnumerator DelayedSpawn(string barcode, Vector3 position, Quaternion rotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            ExplosivePunchController.SpawnEffect(barcode, position, rotation);
        }

        private static void SpawnWithMatrix(string barcode, int count, Vector3 position, Quaternion rotation, float delay, Vector3 direction)
        {
            if (_effectMatrixMode == MatrixMode.DISABLED || _effectMatrixCount <= 1)
            {
                SpawnMultiple(barcode, count, position, rotation, delay);
                return;
            }
            Vector3 forward = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            var offsets = CalculateMatrixOffsets(_effectMatrixCount, _effectMatrixSpacing, right, up, _effectMatrixMode);
            foreach (var offset in offsets)
                SpawnMultiple(barcode, count, position + offset, rotation, delay);
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
