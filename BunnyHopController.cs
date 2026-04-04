using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Bonelab;
using System;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Bunny Hop controller with Easy air-strafe:
    /// Hold W + Space, turn camera to redirect. Instant velocity snap, no air friction.
    /// </summary>
    public static class BunnyHopController
    {
        // ───── Settings ─────
        private static bool _enabled = false;
        private static float _hopBoost = 1.5f;        // extra horizontal speed added per hop (m/s)
        private static float _maxSpeed = 50f;          // horizontal speed cap
        private static float _airStrafeForce = 12f;    // Easy: min strafe speed
        private static float _jumpForce = 5.5f;        // upward velocity on hop
        private static bool _autoHop = true;            // hold A to keep hopping
        private static bool _autoJumpToggle = false;    // press jump once to start auto-hop, press again to stop
        private static bool _autoJumpActive = false;    // runtime state: is auto-jump currently toggled on?
        private static float _standableNormal = 0.7f;  // Source sv_standable_normal (0.7 = ~45°)
        private static bool _trimpEnabled = true;        // TF2-style trimp: hop off ramps to convert speed to height
        private static float _trimpMultiplier = 1.0f;    // how aggressively horizontal speed converts to vertical

        // ───── Jump Effect ─────
        private static bool _jumpEffectEnabled = false;
        private static string _jumpEffectBarcode = "";

        // ───── Internal State ─────
        private static PhysGrounder _grounder;
        private static bool _wasGrounded = true;
        private static bool _wasJumpHeld = false;
        private static Rigidbody[] _cachedRbs;
        private static float _lastCacheTime = 0f;
        private static float _preservedSpeed = 0f;     // highest horizontal speed while airborne (no air friction)
        private static bool _onSurfRamp = false;        // true when on a slope steeper than standable normal
        private static Vector3 _surfNormal = Vector3.up; // cached surface normal from last surf ramp check
        private static bool _didJump = false;            // true after a real hop/trimp — gates air strafe to prevent grounder flicker

        // ───── Properties ─────
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"BunnyHop {(value ? "ON" : "OFF")}");
            }
        }

        public static float HopBoost { get => _hopBoost; set => _hopBoost = Mathf.Clamp(value, 0f, 20f); }
        public static float MaxSpeed { get => _maxSpeed; set => _maxSpeed = Mathf.Clamp(value, 5f, 200f); }
        public static float AirStrafeForce { get => _airStrafeForce; set => _airStrafeForce = Mathf.Clamp(value, 0f, 50f); }
        public static float JumpForce { get => _jumpForce; set => _jumpForce = Mathf.Clamp(value, 1f, 20f); }
        public static bool AutoHop { get => _autoHop; set => _autoHop = value; }
        public static bool AutoJumpToggle { get => _autoJumpToggle; set { _autoJumpToggle = value; if (!value) _autoJumpActive = false; } }
        public static float StandableNormal { get => _standableNormal; set => _standableNormal = Mathf.Clamp(value, 0f, 1f); }
        public static bool TrimpEnabled { get => _trimpEnabled; set => _trimpEnabled = value; }
        public static float TrimpMultiplier { get => _trimpMultiplier; set => _trimpMultiplier = Mathf.Clamp(value, 0f, 3f); }
        public static bool JumpEffectEnabled { get => _jumpEffectEnabled; set => _jumpEffectEnabled = value; }
        public static string JumpEffectBarcode { get => _jumpEffectBarcode; set => _jumpEffectBarcode = value ?? ""; }

        public static void OnLevelUnloaded()
        {
            _grounder = null;
            _cachedRbs = null;
            _wasGrounded = true;
            _wasJumpHeld = false;
            _preservedSpeed = 0f;
            _onSurfRamp = false;
            _surfNormal = Vector3.up;
            _didJump = false;
            _autoJumpActive = false;
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                // Don't bhop while ragdolled or in a seat
                if (physRig.torso.shutdown || !physRig.ballLocoEnabled) return;
                if (rigManager.activeSeat != null) return;

                bool rawGrounded = IsGrounded(physRig);
                bool jumpHeld = IsJumpPressed();

                // Auto Jump Toggle: detect rising edge BEFORE ground check
                // so player can toggle off while airborne
                if (_autoJumpToggle && jumpHeld && !_wasJumpHeld)
                    _autoJumpActive = !_autoJumpActive;

                // Cache rigidbodies periodically
                RefreshRbCache(rigManager);

                // Slope detection: check if we're on a surf ramp (steeper than standable normal)
                _onSurfRamp = false;
                if (rawGrounded)
                {
                    _onSurfRamp = CheckSurfRamp(physRig);
                }

                // On a surf ramp = treat as airborne (air strafe works, speed preserved)
                bool grounded = rawGrounded && !_onSurfRamp;

                if (grounded)
                {
                    bool shouldHop = false;

                    // Auto Jump Toggle: state already toggled above (works in air too)
                    if (_autoJumpToggle)
                    {
                        shouldHop = _autoJumpActive;
                    }
                    else if (_autoHop)
                    {
                        shouldHop = jumpHeld;
                    }
                    else
                    {
                        shouldHop = jumpHeld && !_wasJumpHeld;
                    }

                    if (shouldHop)
                    {
                        PerformHop(physRig);
                        _didJump = true;
                    }
                    else
                    {
                        // Actually standing on ground without hopping — reset air state
                        _preservedSpeed = 0f;
                        _didJump = false;
                    }
                }
                else if (_onSurfRamp && rawGrounded)
                {
                    // On a surf ramp — trimp or air strafe
                    if (_trimpEnabled)
                    {
                        bool shouldTrimp = _autoJumpToggle ? _autoJumpActive : (_autoHop ? jumpHeld : (jumpHeld && !_wasJumpHeld));
                        if (shouldTrimp)
                        {
                            PerformTrimpHop(physRig);
                            _didJump = true;
                        }
                        else if (_didJump)
                        {
                            ApplyAirStrafe(physRig, rigManager);
                        }
                    }
                    else if (_didJump)
                    {
                        ApplyAirStrafe(physRig, rigManager);
                    }
                }
                else if (_didJump)
                {
                    // Airborne after a real jump — air strafe + no air friction
                    ApplyAirStrafe(physRig, rigManager);
                }

                _wasGrounded = grounded;
                _wasJumpHeld = jumpHeld;
            }
            catch { }
        }

        private static bool IsGrounded(PhysicsRig physRig)
        {
            try
            {
                if (_grounder == null)
                {
                    if (physRig.feet != null)
                        _grounder = physRig.feet.GetComponent<PhysGrounder>();
                }
                if (_grounder != null)
                    return _grounder.isGrounded;
            }
            catch { _grounder = null; }
            return false;
        }

        /// <summary>
        /// Raycast down from pelvis to check surface normal.
        /// Returns true if the surface is steeper than _standableNormal (surf ramp).
        /// </summary>
        private static bool CheckSurfRamp(PhysicsRig physRig)
        {
            try
            {
                var feet = physRig.feet;
                if (feet == null) return false;
                Vector3 feetPos = feet.transform.position;

                // Use multiple raycasts in a small cross pattern to avoid false positives
                // from collider edges/seams. Take the MOST FLAT (highest normal.y) result
                // so a single stray edge hit doesn't falsely report a surf ramp.
                // Ignore the player layer (layer 8 in BONELAB) to avoid hitting own colliders.
                int layerMask = ~(1 << 8);
                float bestNormalY = 0f;
                Vector3 bestNormal = Vector3.up;
                bool anyHit = false;
                float offset = 0.08f;

                Vector3[] origins = new Vector3[]
                {
                    feetPos + Vector3.up * 0.1f,
                    feetPos + Vector3.up * 0.1f + Vector3.forward * offset,
                    feetPos + Vector3.up * 0.1f - Vector3.forward * offset,
                    feetPos + Vector3.up * 0.1f + Vector3.right * offset,
                    feetPos + Vector3.up * 0.1f - Vector3.right * offset,
                };

                for (int i = 0; i < origins.Length; i++)
                {
                    if (Physics.Raycast(origins[i], Vector3.down, out RaycastHit hit, 0.5f, layerMask))
                    {
                        anyHit = true;
                        if (hit.normal.y > bestNormalY)
                        {
                            bestNormalY = hit.normal.y;
                            bestNormal = hit.normal;
                        }
                    }
                }

                if (anyHit)
                {
                    _surfNormal = bestNormal;
                    return bestNormalY < _standableNormal;
                }
            }
            catch { }
            return false;
        }

        private static bool IsJumpPressed()
        {
            try
            {
                // A button on right controller (JoystickButton0)
                // Also support keyboard Space
                return Input.GetKey(KeyCode.JoystickButton0) || Input.GetKey(KeyCode.Space);
            }
            catch { return false; }
        }

        private static Vector2 GetMoveInput()
        {
            try
            {
                // Left controller thumbstick for movement direction
                var leftController = Player.LeftController;
                if (leftController != null)
                {
                    return leftController.GetThumbStickAxis();
                }
            }
            catch { }
            return Vector2.zero;
        }

        private static void RefreshRbCache(RigManager rigManager)
        {
            if (Time.time - _lastCacheTime < 2f && _cachedRbs != null) return;

            try
            {
                var rigGo = ((Component)rigManager).gameObject;
                if (rigGo != null)
                    _cachedRbs = rigGo.GetComponentsInChildren<Rigidbody>();
                _lastCacheTime = Time.time;
            }
            catch { _cachedRbs = null; }
        }

        /// <summary>
        /// Perform a bunny hop.
        /// EASY mode: hop direction = stick direction relative to camera (side hop, back hop, etc).
        /// SOURCE mode: preserve current horizontal velocity + boost.
        /// </summary>
        private static void PerformHop(PhysicsRig physRig)
        {
            if (_cachedRbs == null || _cachedRbs.Length == 0) return;

            try
            {
                Vector3 currentVel = Vector3.zero;
                var pelvisRb = physRig.torso?.rbPelvis;
                if (pelvisRb != null)
                    currentVel = pelvisRb.velocity;

                Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                float currentHSpeed = horizontalVel.magnitude;

                // Use preserved speed from last airborne phase so ground friction doesn't kill chain hops
                float baseSpeed = Mathf.Max(currentHSpeed, _preservedSpeed);

                Vector3 hopDir = GetCameraRelativeWishDir();

                float hopSpeed = Mathf.Max(baseSpeed, _airStrafeForce);
                hopSpeed += _hopBoost;
                hopSpeed = Mathf.Min(hopSpeed, _maxSpeed);

                horizontalVel = hopDir * hopSpeed;

                Vector3 hopVelocity = new Vector3(horizontalVel.x, _jumpForce, horizontalVel.z);

                // Update preserved speed so chain hops don't lose speed to ground friction
                _preservedSpeed = new Vector3(horizontalVel.x, 0f, horizontalVel.z).magnitude;

                foreach (var rb in _cachedRbs)
                {
                    if (rb != null && rb.mass > 1f)
                    {
                        rb.velocity = hopVelocity;
                    }
                }

                // Spawn jump effect at feet position
                if (_jumpEffectEnabled && !string.IsNullOrEmpty(_jumpEffectBarcode))
                {
                    try
                    {
                        var feet = physRig.feet;
                        if (feet != null)
                            ExplosivePunchController.SpawnEffect(_jumpEffectBarcode, feet.transform.position, Quaternion.identity);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// TF2-style trimp: hop off a surf ramp to convert horizontal speed into vertical launch.
        /// Uses the cached surface normal to compute slope angle.
        /// vertical_boost = horizontal_speed × sin(slope_angle) × multiplier
        /// remaining_horizontal = horizontal_speed × cos(slope_angle)
        /// </summary>
        private static void PerformTrimpHop(PhysicsRig physRig)
        {
            if (_cachedRbs == null || _cachedRbs.Length == 0) return;

            try
            {
                Vector3 currentVel = Vector3.zero;
                var pelvisRb = physRig.torso?.rbPelvis;
                if (pelvisRb != null)
                    currentVel = pelvisRb.velocity;

                Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
                float hSpeed = horizontalVel.magnitude;

                if (hSpeed < 1f)
                {
                    // Too slow to trimp — do a normal hop instead
                    PerformHop(physRig);
                    return;
                }

                // Slope angle: normal.y = cos(angle_from_vertical) = cos(slope_angle_from_horizontal... no)
                // slope_angle_from_horizontal = acos(normal.y) ... wait, that's angle from vertical
                // Actually: slope steepness angle = acos(normal.y) where 0 = flat, 90 = vertical
                // sin(slope_angle) = sqrt(1 - normal.y^2) → vertical conversion factor
                // cos(slope_angle) = normal.y → horizontal remainder factor
                float normalY = Mathf.Clamp01(_surfNormal.y);
                float slopeSin = Mathf.Sqrt(1f - normalY * normalY);

                // Convert horizontal speed to vertical based on slope steepness
                float verticalBoost = hSpeed * slopeSin * _trimpMultiplier;
                float remainingHSpeed = hSpeed * normalY;

                // Keep horizontal direction, trade speed for height
                Vector3 hDir = horizontalVel.normalized;
                Vector3 trimpVel = hDir * remainingHSpeed;
                trimpVel.y = verticalBoost + _jumpForce;

                // Clamp horizontal to max speed
                float hMag = new Vector3(trimpVel.x, 0f, trimpVel.z).magnitude;
                if (hMag > _maxSpeed)
                {
                    float scale = _maxSpeed / hMag;
                    trimpVel.x *= scale;
                    trimpVel.z *= scale;
                }

                foreach (var rb in _cachedRbs)
                {
                    if (rb != null && rb.mass > 1f)
                        rb.velocity = trimpVel;
                }

                // Set preserved speed so air strafe maintains the new horizontal speed
                _preservedSpeed = new Vector3(trimpVel.x, 0f, trimpVel.z).magnitude;
            }
            catch { }
        }

        /// <summary>
        /// Get the wish direction from thumbstick input relative to camera.
        /// Falls back to camera forward if no stick input.
        /// </summary>
        private static Vector3 GetCameraRelativeWishDir()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.forward;

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            if (camForward.sqrMagnitude < 0.001f) return Vector3.forward;

            Vector2 input = GetMoveInput();
            if (input.sqrMagnitude < 0.1f)
                return camForward; // no stick input = forward hop

            return (camForward * input.y + camRight * input.x).normalized;
        }

        private static void ApplyAirStrafe(PhysicsRig physRig, RigManager rigManager)
        {
            ApplyAirStrafeEasy(physRig);
        }

        /// <summary>
        /// EASY mode: Hold W + Space, turn camera to go that direction.
        /// Instant velocity snap to camera look direction. No air friction.
        /// Supports forward, backward, and side bhop — just look where you want to go.
        /// </summary>
        private static void ApplyAirStrafeEasy(PhysicsRig physRig)
        {
            if (_cachedRbs == null || _cachedRbs.Length == 0) return;

            try
            {
                var pelvisRb = physRig.torso?.rbPelvis;
                if (pelvisRb == null) return;

                Vector3 vel = pelvisRb.velocity;
                Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
                float currentSpeed = horizontalVel.magnitude;

                // No air friction: preserve peak horizontal speed
                if (currentSpeed > _preservedSpeed)
                    _preservedSpeed = currentSpeed;

                // Counteract engine drag
                if (currentSpeed < _preservedSpeed && currentSpeed > 0.1f)
                {
                    Vector3 corrected = horizontalVel.normalized * _preservedSpeed;
                    Vector3 correction = corrected - horizontalVel;
                    foreach (var rb in _cachedRbs)
                    {
                        if (rb != null && rb.mass > 1f)
                            rb.velocity += new Vector3(correction.x, 0f, correction.z);
                    }
                    currentSpeed = _preservedSpeed;
                }

                if (_airStrafeForce <= 0f) return;

                Vector2 input = GetMoveInput();
                if (input.sqrMagnitude < 0.1f) return;

                var cam = Camera.main;
                if (cam == null) return;

                Vector3 camForward = cam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = cam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();
                if (camForward.sqrMagnitude < 0.001f) return;

                // Wish direction from thumbstick (forward, back, side — all work)
                Vector3 wishDir = (camForward * input.y + camRight * input.x).normalized;

                float speed = Mathf.Max(currentSpeed, _airStrafeForce);
                speed = Mathf.Min(speed, _maxSpeed);

                Vector3 newHorizontal = wishDir * speed;
                _preservedSpeed = speed;

                foreach (var rb in _cachedRbs)
                {
                    if (rb != null && rb.mass > 1f)
                    {
                        float yVel = rb.velocity.y;
                        rb.velocity = new Vector3(newHorizontal.x, yVel, newHorizontal.z);
                    }
                }
            }
            catch { }
        }
    }
}
