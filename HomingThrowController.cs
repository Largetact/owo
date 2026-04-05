using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    public static class HomingThrowController
    {
        // ── Settings ──
        public static bool Enabled = false;
        public static TargetFilter Filter = TargetFilter.NEAREST;
        public static float Strength = 5f;
        public static float Speed = 0f;           // 0 = use throw velocity
        public static float Duration = 0f;         // 0 = unlimited (until max lifetime)
        public static float MaxLifetime = 10f;
        public static bool RotationLock = false;
        public static bool AccelEnabled = false;
        public static float AccelRate = 2f;
        public static bool TargetHead = false;
        public static bool Momentum = false;
        public static float StayDuration = 2f;
        public static float MinThrowSpeed = 2f;   // minimum velocity to count as a "throw"

        // ── FOV cone settings ──
        public static bool FovConeEnabled = false;
        public static float FovAngle = 90f;         // half-angle in degrees (90 = 180° total cone)

        // ── Recall settings ──
        public static bool RecallEnabled = false;
        public static float RecallSpeed = 15f;
        public static float RecallStrength = 8f;

        // ── Internal state ──
        private struct HomingThrown
        {
            public Rigidbody Rb;
            public float SpawnTime;
            public float CurrentSpeed;
            public float StayStartTime;       // -1 = not yet on target
            public bool IsRecalling;           // in return-to-hand mode
            public int SourceHand;             // 0=left, 1=right
        }

        private static readonly List<HomingThrown> _active = new List<HomingThrown>();

        // Track what each hand was holding last frame
        private static GameObject _prevLeftHeld;
        private static GameObject _prevRightHeld;

        // Recall state — which hand is requesting recall
        private static bool _recallLeftRequested;
        private static bool _recallRightRequested;

        public static void Initialize()
        {
            _active.Clear();
            _prevLeftHeld = null;
            _prevRightHeld = null;
            _recallLeftRequested = false;
            _recallRightRequested = false;
        }

        public static void Update()
        {
            if (!Enabled)
            {
                // Still update recall even if homing is off (items in flight)
                if (_active.Count > 0) UpdateHoming();
                return;
            }

            DetectThrows();
            DetectRecall();
            UpdateHoming();
        }

        // ── Throw detection via per-frame polling ──
        private static void DetectThrows()
        {
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                var leftHand = physRig.leftHand;
                var rightHand = physRig.rightHand;

                GameObject curLeft = null;
                GameObject curRight = null;

                try { if (leftHand != null && leftHand.m_CurrentAttachedGO != null) curLeft = leftHand.m_CurrentAttachedGO; } catch { }
                try { if (rightHand != null && rightHand.m_CurrentAttachedGO != null) curRight = rightHand.m_CurrentAttachedGO; } catch { }

                // Left hand released something
                if (_prevLeftHeld != null && curLeft == null)
                    TryRegisterThrown(_prevLeftHeld, 0);

                // Right hand released something
                if (_prevRightHeld != null && curRight == null)
                    TryRegisterThrown(_prevRightHeld, 1);

                _prevLeftHeld = curLeft;
                _prevRightHeld = curRight;
            }
            catch { }
        }

        // ── Recall detection: grip both triggers while hand is empty ──
        private static void DetectRecall()
        {
            if (!RecallEnabled || _active.Count == 0) return;

            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                // Check if each hand is empty and gripping (trigger held)
                bool leftEmpty = true;
                bool rightEmpty = true;
                try { leftEmpty = physRig.leftHand == null || physRig.leftHand.m_CurrentAttachedGO == null; } catch { }
                try { rightEmpty = physRig.rightHand == null || physRig.rightHand.m_CurrentAttachedGO == null; } catch { }

                // Use BoneLib controller input: grip button on empty hand triggers recall
                bool leftGrip = false;
                bool rightGrip = false;
                try { leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger") > 0.7f; } catch { }
                try { rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger") > 0.7f; } catch { }

                _recallLeftRequested = leftEmpty && leftGrip;
                _recallRightRequested = rightEmpty && rightGrip;

                // Trigger recall on matching items
                if (_recallLeftRequested || _recallRightRequested)
                {
                    for (int i = 0; i < _active.Count; i++)
                    {
                        var hp = _active[i];
                        if (hp.IsRecalling) continue;

                        // Recall items back to the hand that threw them
                        if ((hp.SourceHand == 0 && _recallLeftRequested) ||
                            (hp.SourceHand == 1 && _recallRightRequested))
                        {
                            hp.IsRecalling = true;
                            hp.StayStartTime = -1f; // reset stay state
                            _active[i] = hp;
                        }
                    }
                }
            }
            catch { }
        }

        private static void TryRegisterThrown(GameObject obj, int hand)
        {
            try
            {
                if (obj == null) return;

                // Get rigidbody from the released object or its root
                var rb = obj.GetComponentInParent<Rigidbody>();
                if (rb == null) rb = obj.GetComponentInChildren<Rigidbody>();
                if (rb == null) return;

                // Already tracked?
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i].Rb == rb) return;

                // Check minimum throw speed
                float speed = rb.velocity.magnitude;
                if (speed < MinThrowSpeed) return;

                rb.useGravity = false;
                float initSpeed = Speed > 0f ? Speed : speed;
                if (initSpeed < 1f) initSpeed = 1f;

                _active.Add(new HomingThrown
                {
                    Rb = rb,
                    SpawnTime = Time.time,
                    CurrentSpeed = initSpeed,
                    StayStartTime = -1f,
                    IsRecalling = false,
                    SourceHand = hand
                });
            }
            catch { }
        }

        // ── Homing steering ──
        private static void UpdateHoming()
        {
            if (_active.Count == 0) return;

            Vector3 leftHandPos = Vector3.zero;
            Vector3 rightHandPos = Vector3.zero;
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig?.leftHand != null)
                    leftHandPos = ((UnityEngine.Component)physRig.leftHand).transform.position;
                if (physRig?.rightHand != null)
                    rightHandPos = ((UnityEngine.Component)physRig.rightHand).transform.position;
            }
            catch { }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var hp = _active[i];

                // Cleanup destroyed objects
                if (hp.Rb == null) { _active.RemoveAt(i); continue; }
                try { if (hp.Rb.gameObject == null) { _active.RemoveAt(i); continue; } }
                catch { _active.RemoveAt(i); continue; }

                float elapsed = Time.time - hp.SpawnTime;

                // Max lifetime (unless recalling)
                if (!hp.IsRecalling && elapsed > MaxLifetime)
                {
                    hp.Rb.useGravity = true;
                    _active.RemoveAt(i);
                    continue;
                }

                // Duration limit (unless recalling)
                if (!hp.IsRecalling && Duration > 0f && elapsed > Duration)
                {
                    hp.Rb.useGravity = true;
                    _active.RemoveAt(i);
                    continue;
                }

                // ── RECALL MODE: return to hand ──
                if (hp.IsRecalling)
                {
                    Vector3 handPos = hp.SourceHand == 0 ? leftHandPos : rightHandPos;
                    if (handPos == Vector3.zero) { continue; } // hand not available

                    Vector3 toHand = handPos - hp.Rb.position;
                    float distToHand = toHand.magnitude;

                    // Arrived — drop into hand
                    if (distToHand < 0.4f)
                    {
                        hp.Rb.useGravity = true;
                        hp.Rb.velocity = Vector3.zero;
                        hp.Rb.position = handPos;
                        _active.RemoveAt(i);
                        continue;
                    }

                    Vector3 dir = toHand / distToHand;

                    Vector3 recallDesired = dir * RecallSpeed;
                    Vector3 steerForce = (recallDesired - hp.Rb.velocity) * RecallStrength;
                    hp.Rb.velocity += steerForce * Time.deltaTime;

                    if (RotationLock)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(-dir); // face backward on return
                        hp.Rb.rotation = Quaternion.Slerp(hp.Rb.rotation, lookRot, RecallStrength * Time.deltaTime);
                    }
                    continue;
                }

                // ── HOMING MODE: steer toward target ──
                var target = PlayerTargeting.FindTarget(Filter, hp.Rb.position);
                if (target == null) continue;

                // FOV cone check — skip targets behind the player's looking direction
                if (FovConeEnabled)
                {
                    try
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            Vector3? tPos = TargetHead
                                ? PlayerTargeting.GetTargetHeadPosition(target)
                                : PlayerTargeting.GetTargetPosition(target);
                            if (tPos.HasValue)
                            {
                                Vector3 toTgt = (tPos.Value - cam.transform.position).normalized;
                                float angle = Vector3.Angle(cam.transform.forward, toTgt);
                                if (angle > FovAngle) continue; // outside cone
                            }
                        }
                    }
                    catch { }
                }

                Vector3? targetPos = TargetHead
                    ? PlayerTargeting.GetTargetHeadPosition(target)
                    : PlayerTargeting.GetTargetPosition(target);
                if (!targetPos.HasValue) continue;

                Vector3 toTarget = targetPos.Value - hp.Rb.position;
                float distToTarget = toTarget.magnitude;

                // ── Stay-on-target (within 0.5m) ──
                if (distToTarget < 0.5f)
                {
                    if (hp.StayStartTime < 0f)
                    {
                        hp.StayStartTime = Time.time;
                        _active[i] = hp;
                    }
                    hp.Rb.velocity = Vector3.zero;
                    hp.Rb.position = targetPos.Value;

                    if (Time.time - hp.StayStartTime >= StayDuration)
                    {
                        hp.Rb.useGravity = true;
                        _active.RemoveAt(i);
                    }
                    continue;
                }

                if (distToTarget < 0.01f) continue;
                Vector3 dirToTarget = toTarget / distToTarget;

                // ── Speed ──
                float speed = hp.CurrentSpeed;
                if (Speed > 0f && !AccelEnabled) speed = Speed;

                // ── Acceleration ──
                if (AccelEnabled)
                {
                    float targetSpeed = 0f;
                    try
                    {
                        var physRig = target.physicsRig;
                        if (physRig?.torso?.rbPelvis != null)
                            targetSpeed = physRig.torso.rbPelvis.velocity.magnitude;
                    }
                    catch { }

                    float accelFactor = 1f + targetSpeed * AccelRate * 0.1f;
                    speed *= Mathf.Pow(accelFactor, Time.deltaTime * AccelRate);
                    speed = Mathf.Min(speed, 500f);
                }

                hp.CurrentSpeed = speed;
                _active[i] = hp;

                // ── Steering ──
                Vector3 desired = dirToTarget * speed;
                if (Momentum)
                {
                    // Force-based: preserves inertia, target can dodge
                    Vector3 steerForce = (desired - hp.Rb.velocity) * Strength;
                    hp.Rb.velocity += steerForce * Time.deltaTime;
                }
                else
                {
                    // Direct: snap toward target
                    hp.Rb.velocity = Vector3.Lerp(hp.Rb.velocity, desired, Strength * Time.deltaTime);
                }

                // ── Rotation lock ──
                if (RotationLock)
                {
                    Vector3 velDir = hp.Rb.velocity.normalized;
                    if (velDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(velDir);
                        hp.Rb.rotation = Quaternion.Slerp(hp.Rb.rotation, lookRot, Strength * Time.deltaTime);
                    }
                }
            }
        }
    }
}
