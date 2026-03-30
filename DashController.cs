using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    public static class DashController
    {
        // ═══════════════════════════════════════════════════
        // DASH SETTINGS
        // ═══════════════════════════════════════════════════
        private static bool dashEnabled = false;
        private static float dashForce = 15f;
        private static bool dashInstantaneous = true;
        private static bool dashContinuous = false;
        private static bool isHandOriented = false;
        private static bool useLeftHand = false;
        private static bool _lockOnEnabled = false;
        private static TargetFilter _lockOnFilter = TargetFilter.NEAREST;
        private static bool _lookAtTarget = false;
        private static bool _lookAtHead = false; // false = torso (yaw only), true = head (yaw+pitch)
        private static bool _killVelocityOnLand = false;
        private static bool _waitingForLanding = false;  // true = dashed and went airborne, waiting to land
        private static bool _wasAirborneAfterDash = false; // true once we detect airborne after a dash
        private static PhysGrounder _grounder = null;
        private static bool prevRightStickPressed = false;

        // ═══════════════════════════════════════════════════
        // EFFECT SYSTEM — Spawns effects at torso on dash
        // ═══════════════════════════════════════════════════
        private const string SmashBoneBarcode = "Lakatrazz.FusionContent.Spawnable.DeathExplosion";

        // Custom effect (from search menu)
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

        public static bool IsDashEnabled
        {
            get => dashEnabled;
            set
            {
                dashEnabled = value;
                Main.MelonLog.Msg($"Dash {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static float DashForce
        {
            get => dashForce;
            set
            {
                dashForce = Mathf.Clamp(value, 0f, 500f);
            }
        }

        public static bool IsDashInstantaneous
        {
            get => dashInstantaneous;
            set
            {
                dashInstantaneous = value;
                Main.MelonLog.Msg($"Dash Mode: {(value ? "Instantaneous" : "Additive")}");
            }
        }

        public static bool IsDashContinuous
        {
            get => dashContinuous;
            set
            {
                dashContinuous = value;
                Main.MelonLog.Msg($"Dash Continuous: {(value ? "ON (hold)" : "OFF (click)")}");
            }
        }

        public static bool IsHandOriented
        {
            get => isHandOriented;
            set
            {
                isHandOriented = value;
                Main.MelonLog.Msg($"Dash Hand Oriented: {(value ? "ON" : "OFF (head)")}");
            }
        }

        public static bool UseLeftHand
        {
            get => useLeftHand;
            set
            {
                useLeftHand = value;
                Main.MelonLog.Msg($"Dash Hand: {(value ? "Left" : "Right")}");
            }
        }

        public static bool KillVelocityOnLand
        {
            get => _killVelocityOnLand;
            set
            {
                _killVelocityOnLand = value;
                Main.MelonLog.Msg($"Dash Kill Velocity On Land: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool LockOnEnabled
        {
            get => _lockOnEnabled;
            set
            {
                _lockOnEnabled = value;
                Main.MelonLog.Msg($"Dash Lock-On: {(value ? "ON" : "OFF")}");
            }
        }

        public static TargetFilter LockOnFilter
        {
            get => _lockOnFilter;
            set
            {
                _lockOnFilter = value;
                Main.MelonLog.Msg($"Dash Lock-On Filter: {value}");
            }
        }

        public static bool LookAtTarget
        {
            get => _lookAtTarget;
            set
            {
                _lookAtTarget = value;
                Main.MelonLog.Msg($"Dash Look-At Target: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool LookAtHead
        {
            get => _lookAtHead;
            set
            {
                _lookAtHead = value;
                Main.MelonLog.Msg($"Dash Look-At: {(value ? "Head" : "Torso")}");
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

        public static void Initialize()
        {
            Main.MelonLog.Msg("Dash controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            _grounder = null;
        }

        public static void Update()
        {
            if (dashEnabled)
            {
                UpdateDash();
            }

            if (_killVelocityOnLand && _waitingForLanding)
            {
                CheckLandingAndKillVelocity();
            }
        }

        private static void UpdateDash()
        {
            try
            {
                // Check for thumbstick click
                // Left thumbstick = JoystickButton8, Right thumbstick = JoystickButton9
                bool rightStickPressed = false;
                try
                {
                    if (useLeftHand)
                        rightStickPressed = Input.GetKey(KeyCode.JoystickButton8);
                    else
                        rightStickPressed = Input.GetKey(KeyCode.JoystickButton9);
                }
                catch { }

                // Keyboard fallback: Shift key
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    rightStickPressed = true;
                }

                if (dashContinuous)
                {
                    // Continuous mode: dash every frame while holding
                    if (rightStickPressed)
                    {
                        PerformDash();
                        if (_killVelocityOnLand) { _waitingForLanding = true; _wasAirborneAfterDash = false; }
                    }
                }
                else
                {
                    // Click mode: only trigger on press (not hold)
                    if (rightStickPressed && !prevRightStickPressed)
                    {
                        PerformDash();
                        if (_killVelocityOnLand) { _waitingForLanding = true; _wasAirborneAfterDash = false; }
                    }
                }

                prevRightStickPressed = rightStickPressed;
            }
            catch { }
        }

        private static bool IsGrounded()
        {
            try
            {
                // Use the game's built-in PhysGrounder component on the feet GameObject.
                // PhysGrounder uses OnCollisionEnter/OnCollisionStay — real collider contact, not raycasts.
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

        /// <summary>
        /// After a dash, wait until the player goes airborne, then kill velocity
        /// the moment they touch ground again. This avoids killing velocity
        /// when dashing along the ground or mid-air.
        /// </summary>
        private static void CheckLandingAndKillVelocity()
        {
            try
            {
                bool grounded = IsGrounded();

                if (!_wasAirborneAfterDash)
                {
                    // Phase 1: waiting to leave the ground
                    if (!grounded)
                    {
                        _wasAirborneAfterDash = true;
                    }
                    return;
                }

                // Phase 2: was airborne, waiting to touch ground
                if (!grounded) return;

                // Landed! Kill all velocity
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var rigGo = (rigManager as UnityEngine.Component)?.gameObject;
                if (rigGo != null)
                {
                    var allRbs = rigGo.GetComponentsInChildren<Rigidbody>();
                    foreach (var rb in allRbs)
                    {
                        if (rb != null && rb.mass > 1f)
                        {
                            rb.velocity = Vector3.zero;
                        }
                    }
                }

                _waitingForLanding = false;
                _wasAirborneAfterDash = false;
            }
            catch { }
        }

        private static void PerformDash()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null)
                    return;

                // Get dash direction
                Vector3 dashDirection;
                if (_lockOnEnabled)
                {
                    // Lock-on: aim toward the targeted player
                    var head = Player.Head;
                    if (head == null) return;
                    var dir = PlayerTargeting.GetDirectionToTarget(_lockOnFilter, head.position);
                    dashDirection = dir ?? head.forward.normalized;
                }
                else if (isHandOriented)
                {
                    // Hand-oriented: use the selected hand's forward direction
                    var hand = useLeftHand ? Player.LeftHand : Player.RightHand;
                    if (hand == null) return;
                    dashDirection = hand.transform.forward.normalized;
                }
                else
                {
                    // Default: use the main camera's forward direction.
                    // Camera.main is on the OpenControllerRig and always reflects
                    // where the player is actually looking — immune to physics
                    // head drift from ragdoll (unlike Player.Head which is physical).
                    var cam = Camera.main;
                    if (cam == null) return;
                    dashDirection = cam.transform.forward.normalized;
                }

                // Find rigidbodies to apply force to
                // Get all rigidbodies from the rig and apply velocity to the heaviest ones
                try
                {
                    var rigGo = (rigManager as UnityEngine.Component)?.gameObject;
                    if (rigGo != null)
                    {
                        var allRbs = rigGo.GetComponentsInChildren<Rigidbody>();

                        // Apply to rigidbodies with higher mass (main body parts)
                        int appliedCount = 0;
                        foreach (var rb in allRbs)
                        {
                            if (rb != null && rb.mass > 1f)
                            {
                                if (dashInstantaneous)
                                {
                                    // Set velocity directly (cancels existing momentum for responsive direction changes)
                                    rb.velocity = dashDirection * dashForce;
                                }
                                else
                                {
                                    // Add to existing velocity (old behavior)
                                    rb.velocity += dashDirection * dashForce;
                                }
                                appliedCount++;
                            }
                        }

                        if (appliedCount > 0)
                        {
                            Main.MelonLog.Msg($"Dash! Force: {dashForce}, Applied to {appliedCount} rigidbodies");
                        }
                    }
                }
                catch { }

                // Rotate rig to face the lock-on target
                if (_lockOnEnabled && _lookAtTarget)
                    RotateRigTowardTarget(rigManager);

                // Spawn effects at torso
                SpawnDashEffects(rigManager, dashDirection);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Dash error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        // EFFECT SPAWNING — Spawns at torso position
        // ═══════════════════════════════════════════════════
        private static void SpawnDashEffects(RigManager rigManager, Vector3 direction)
        {
            bool anyEffect = _effectEnabled || _smashBoneEnabled || _cosmeticEnabled;
            if (!anyEffect) return;

            // Check spawn interval
            if (_effectSpawnInterval > 0f && Time.time - _lastEffectSpawnTime < _effectSpawnInterval)
                return;
            _lastEffectSpawnTime = Time.time;

            try
            {
                // Get torso position
                var head = Player.Head;
                if (head == null) return;

                // Torso is roughly below head
                Vector3 torsoPos = head.position + Vector3.down * 0.35f;

                // Apply transform offset (in local space of the orientation)
                Quaternion orientationRot = GetOrientationRotation();
                torsoPos += orientationRot * _effectOffset;

                // Calculate rotation based on orientation mode
                Quaternion spawnRot = orientationRot;

                // SmashBone rotation
                Vector3 smashDir = _smashBoneFlip ? -direction : direction;
                Quaternion smashRot = direction.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(smashDir)
                    : Quaternion.identity;

                // Cosmetic rotation
                Quaternion cosRot = _cosmeticFlip ? Quaternion.LookRotation(direction) : spawnRot;

                // Spawn custom effect
                if (_effectEnabled && !string.IsNullOrEmpty(_effectBarcode))
                {
                    SpawnWithMatrix(_effectBarcode, 1, torsoPos, spawnRot, _effectSpawnDelay, direction);
                }

                // Spawn SmashBone
                if (_smashBoneEnabled)
                {
                    SpawnWithMatrix(SmashBoneBarcode, _smashBoneCount, torsoPos, smashRot, _effectSpawnDelay, direction);
                }

                // Spawn Cosmetic
                if (_cosmeticEnabled && !string.IsNullOrEmpty(_cosmeticBarcode))
                {
                    SpawnWithMatrix(_cosmeticBarcode, _cosmeticCount, torsoPos, cosRot, _effectSpawnDelay, direction);
                }
            }
            catch { }
        }

        private static Quaternion GetOrientationRotation()
        {
            if (isHandOriented)
            {
                var hand = useLeftHand ? Player.LeftHand : Player.RightHand;
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

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
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

        private static void RotateRigTowardTarget(RigManager rigManager)
        {
            try
            {
                var head = Player.Head;
                if (head == null) return;

                var dir = PlayerTargeting.GetDirectionToTarget(_lockOnFilter, head.position, _lookAtHead);
                if (dir == null) return;

                // Yaw rotation (horizontal)
                Vector3 flatDir = new Vector3(dir.Value.x, 0f, dir.Value.z);
                if (flatDir.sqrMagnitude < 0.001f) return;

                Vector3 headFlat = new Vector3(head.forward.x, 0f, head.forward.z);
                if (headFlat.sqrMagnitude < 0.001f) return;

                float yawAngle = Vector3.SignedAngle(headFlat.normalized, flatDir.normalized, Vector3.up);

                var rigTransform = (rigManager as UnityEngine.Component)?.transform;
                if (rigTransform != null)
                    rigTransform.RotateAround(head.position, Vector3.up, yawAngle);

                // Pitch rotation when targeting head (yaw+pitch)
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
    }
}
