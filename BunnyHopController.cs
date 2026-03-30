using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Bonelab;
using System;

namespace BonelabUtilityMod
{
    public enum AirStrafeMode { EASY, SOURCE }

    /// <summary>
    /// Bunny Hop controller — supports two air-strafe modes:
    /// EASY: Hold W + Space, turn camera to redirect. Instant velocity snap, no air friction.
    /// SOURCE: Classic Quake/Source air acceleration. Strafe + turn to build speed.
    /// </summary>
    public static class BunnyHopController
    {
        // ───── Settings ─────
        private static bool _enabled = false;
        private static float _hopBoost = 1.5f;        // extra horizontal speed added per hop (m/s)
        private static float _maxSpeed = 50f;          // horizontal speed cap
        private static float _airStrafeForce = 12f;    // Easy: min strafe speed / Source: air accel multiplier
        private static float _jumpForce = 5.5f;        // upward velocity on hop
        private static bool _autoHop = true;            // hold A to keep hopping
        private static AirStrafeMode _airStrafeMode = AirStrafeMode.EASY;
        private static float _standableNormal = 0.7f;  // Source sv_standable_normal (0.7 = ~45°)

        // ───── Internal State ─────
        private static PhysGrounder _grounder;
        private static bool _wasGrounded = true;
        private static bool _wasJumpHeld = false;
        private static Rigidbody[] _cachedRbs;
        private static float _lastCacheTime = 0f;
        private static float _preservedSpeed = 0f;     // highest horizontal speed while airborne (no air friction)
        private static bool _onSurfRamp = false;        // true when on a slope steeper than standable normal

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
        public static AirStrafeMode StrafeMode { get => _airStrafeMode; set => _airStrafeMode = value; }
        public static float StandableNormal { get => _standableNormal; set => _standableNormal = Mathf.Clamp(value, 0f, 1f); }

        public static void OnLevelUnloaded()
        {
            _grounder = null;
            _cachedRbs = null;
            _wasGrounded = true;
            _wasJumpHeld = false;
            _preservedSpeed = 0f;
            _onSurfRamp = false;
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
                    // Just landed or still on ground — reset preserved speed
                    _preservedSpeed = 0f;

                    bool shouldHop = false;

                    if (_autoHop)
                    {
                        // Auto-hop: jump fires as long as A is held
                        shouldHop = jumpHeld;
                    }
                    else
                    {
                        // Manual: only on fresh press
                        shouldHop = jumpHeld && !_wasJumpHeld;
                    }

                    if (shouldHop)
                    {
                        PerformHop(physRig);
                    }
                }
                else
                {
                    // Airborne — Source-style instant air strafe + no air friction
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
                var pelvis = physRig.torso?.rbPelvis;
                if (pelvis == null) return false;

                // Raycast down from pelvis
                if (Physics.Raycast(pelvis.position, Vector3.down, out RaycastHit hit, 3f))
                {
                    // hit.normal.y is the dot product with Vector3.up
                    // Flat ground = 1.0, vertical wall = 0.0
                    // If normal.y < standable threshold, it's a surf ramp
                    return hit.normal.y < _standableNormal;
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

                if (_airStrafeMode == AirStrafeMode.EASY)
                {
                    // EASY: hop in the stick direction relative to camera
                    // Holding D = side hop, S = back hop, W = forward hop, etc.
                    Vector3 hopDir = GetCameraRelativeWishDir();

                    // Use current speed or a minimum so hops from standstill still move
                    float hopSpeed = Mathf.Max(currentHSpeed, _airStrafeForce);
                    hopSpeed += _hopBoost;
                    hopSpeed = Mathf.Min(hopSpeed, _maxSpeed);

                    horizontalVel = hopDir * hopSpeed;
                }
                else
                {
                    // SOURCE: preserve current velocity direction, add boost
                    if (currentHSpeed > 0.5f && currentHSpeed < _maxSpeed)
                    {
                        Vector3 boostDir = horizontalVel.normalized;
                        horizontalVel += boostDir * _hopBoost;

                        if (horizontalVel.magnitude > _maxSpeed)
                            horizontalVel = horizontalVel.normalized * _maxSpeed;
                    }
                }

                Vector3 hopVelocity = new Vector3(horizontalVel.x, _jumpForce, horizontalVel.z);

                foreach (var rb in _cachedRbs)
                {
                    if (rb != null && rb.mass > 1f)
                    {
                        rb.velocity = hopVelocity;
                    }
                }
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
            if (_airStrafeMode == AirStrafeMode.EASY)
                ApplyAirStrafeEasy(physRig);
            else
                ApplyAirStrafeSource(physRig);
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

        // Source/Quake constants
        private const float SV_AIRACCELERATE = 10f;  // base air accel (Source default)
        private const float SV_MAXAIRSPEED = 30f;    // max wish speed in air (Source: 30 u/s)

        /// <summary>
        /// SOURCE mode: Classic Quake/Source air acceleration.
        /// Hold A or D (no W!) and smoothly turn your camera into the strafe.
        /// Speed builds through the dot-product mechanic. AirStrafeForce scales the accel.
        /// </summary>
        private static void ApplyAirStrafeSource(PhysicsRig physRig)
        {
            if (_cachedRbs == null || _cachedRbs.Length == 0) return;

            try
            {
                var pelvisRb = physRig.torso?.rbPelvis;
                if (pelvisRb == null) return;

                Vector3 vel = pelvisRb.velocity;
                Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);

                // No air friction in Source either — counteract engine drag
                float currentSpeed = horizontalVel.magnitude;
                if (currentSpeed > _preservedSpeed)
                    _preservedSpeed = currentSpeed;
                if (currentSpeed < _preservedSpeed && currentSpeed > 0.1f)
                {
                    horizontalVel = horizontalVel.normalized * _preservedSpeed;
                    currentSpeed = _preservedSpeed;
                }

                Vector2 input = GetMoveInput();
                if (input.sqrMagnitude < 0.1f)
                {
                    // No input — just apply drag correction and return
                    ApplyHorizontalVelocity(horizontalVel);
                    return;
                }

                var cam = Camera.main;
                if (cam == null) return;

                Vector3 camForward = cam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = cam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();
                if (camForward.sqrMagnitude < 0.001f) return;

                // Wish direction from thumbstick
                Vector3 wishDir = (camForward * input.y + camRight * input.x).normalized;
                float wishSpeed = SV_MAXAIRSPEED;

                // Source air acceleration formula:
                // currentspeed = dot(velocity, wishdir)
                // addspeed = wishspeed - currentspeed
                // if addspeed <= 0: no acceleration
                // accelspeed = accel * dt * wishspeed
                // if accelspeed > addspeed: accelspeed = addspeed
                // velocity += wishdir * accelspeed
                float currentSpeedInWishDir = Vector3.Dot(horizontalVel, wishDir);
                float addSpeed = wishSpeed - currentSpeedInWishDir;
                if (addSpeed <= 0f)
                {
                    ApplyHorizontalVelocity(horizontalVel);
                    return;
                }

                float accel = SV_AIRACCELERATE * (_airStrafeForce / 12f); // scale by user setting
                float accelSpeed = accel * Time.deltaTime * wishSpeed;
                if (accelSpeed > addSpeed)
                    accelSpeed = addSpeed;

                Vector3 newHorizontal = horizontalVel + wishDir * accelSpeed;

                // Clamp to max speed
                if (newHorizontal.magnitude > _maxSpeed)
                    newHorizontal = newHorizontal.normalized * _maxSpeed;

                // Update preserved speed
                if (newHorizontal.magnitude > _preservedSpeed)
                    _preservedSpeed = newHorizontal.magnitude;

                ApplyHorizontalVelocity(newHorizontal);
            }
            catch { }
        }

        private static void ApplyHorizontalVelocity(Vector3 newHorizontal)
        {
            if (_cachedRbs == null) return;
            foreach (var rb in _cachedRbs)
            {
                if (rb != null && rb.mass > 1f)
                {
                    float yVel = rb.velocity.y;
                    rb.velocity = new Vector3(newHorizontal.x, yVel, newHorizontal.z);
                }
            }
        }
    }
}
