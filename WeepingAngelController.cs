using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Weeping Angel mode — if any player is looking at you, you cannot move.
    /// When nobody is looking, you can move freely.
    /// Freeze is enforced by setting all local rig rigidbodies to kinematic
    /// and snapping every bone transform back to its frozen position each tick.
    /// </summary>
    public static class WeepingAngelController
    {
        private static bool _enabled = false;
        private static bool _targetEveryone = true;
        private static float _viewAngle = 60f;
        private static float _viewDistance = 100f;
        private static bool _isFrozen = false;

        // Single-player targeting
        private static RigManager _targetRig = null;
        private static string _targetPlayerName = "(none)";

        // Freeze state — cached rigidbodies + bone snapshots
        private static Rigidbody[] _frozenRigidbodies;
        private static Transform[] _frozenTransforms;
        private static Vector3[] _frozenPositions;
        private static Quaternion[] _frozenRotations;

        private static float _lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 0.1f;

        // Rig cache
        private static readonly List<RigManager> _otherRigs = new List<RigManager>();
        private static float _lastRigCacheTime = 0f;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                    Unfreeze();
            }
        }

        public static bool TargetEveryone
        {
            get => _targetEveryone;
            set => _targetEveryone = value;
        }

        public static string TargetPlayerName => _targetPlayerName;

        public static float ViewAngle
        {
            get => _viewAngle;
            set => _viewAngle = Mathf.Clamp(value, 10f, 180f);
        }

        public static float ViewDistance
        {
            get => _viewDistance;
            set => _viewDistance = Mathf.Clamp(value, 5f, 500f);
        }

        public static bool IsFrozen => _isFrozen;

        public static void Initialize() { }

        public static void SetTargetPlayer(RigManager rig, string name)
        {
            _targetRig = rig;
            _targetPlayerName = name ?? "Unknown";
        }

        public static void ClearTarget()
        {
            _targetRig = null;
            _targetPlayerName = "(none)";
        }

        public static void OnLevelUnloaded()
        {
            _isFrozen = false;
            _targetRig = null;
            _frozenRigidbodies = null;
            _frozenTransforms = null;
            _frozenPositions = null;
            _frozenRotations = null;
            _otherRigs.Clear();
            _lastRigCacheTime = 0f;
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var myRig = Player.RigManager;
                if (myRig == null) return;
                var head = Player.Head;
                if (head == null) return;

                Vector3 myPos = head.position;

                // Check every 0.1s whether anyone is looking at us
                if (Time.time - _lastCheckTime >= CHECK_INTERVAL)
                {
                    _lastCheckTime = Time.time;

                    bool anyoneLooking = false;

                    if (_targetEveryone)
                    {
                        RefreshRigCache(myRig);
                        foreach (var rig in _otherRigs)
                        {
                            if (rig == null) continue;
                            if (IsLookingAtMe(rig, myPos))
                            {
                                anyoneLooking = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (_targetRig != null)
                        {
                            try
                            {
                                if (((Component)_targetRig) != null)
                                    anyoneLooking = IsLookingAtMe(_targetRig, myPos);
                                else
                                    _targetRig = null;
                            }
                            catch { _targetRig = null; }
                        }
                    }

                    if (anyoneLooking && !_isFrozen)
                        Freeze(myRig);
                    else if (!anyoneLooking && _isFrozen)
                        Unfreeze();
                }

                // Every tick while frozen: snap all bones back and zero velocities
                if (_isFrozen)
                    MaintainFreeze();
            }
            catch { }
        }

        private static void Freeze(RigManager myRig)
        {
            try
            {
                var rigGo = ((Component)myRig).gameObject;
                _frozenRigidbodies = rigGo.GetComponentsInChildren<Rigidbody>();

                _frozenTransforms = new Transform[_frozenRigidbodies.Length];
                _frozenPositions = new Vector3[_frozenRigidbodies.Length];
                _frozenRotations = new Quaternion[_frozenRigidbodies.Length];

                for (int i = 0; i < _frozenRigidbodies.Length; i++)
                {
                    if (_frozenRigidbodies[i] == null) continue;
                    _frozenTransforms[i] = _frozenRigidbodies[i].transform;
                    _frozenPositions[i] = _frozenTransforms[i].position;
                    _frozenRotations[i] = _frozenTransforms[i].rotation;
                    _frozenRigidbodies[i].velocity = Vector3.zero;
                    _frozenRigidbodies[i].angularVelocity = Vector3.zero;
                    _frozenRigidbodies[i].isKinematic = true;
                }

                _isFrozen = true;
            }
            catch { }
        }

        private static void Unfreeze()
        {
            if (_frozenRigidbodies != null)
            {
                try
                {
                    foreach (var rb in _frozenRigidbodies)
                    {
                        if (rb == null) continue;
                        rb.isKinematic = false;
                    }
                }
                catch { }
            }
            _frozenRigidbodies = null;
            _frozenTransforms = null;
            _frozenPositions = null;
            _frozenRotations = null;
            _isFrozen = false;
        }

        private static void MaintainFreeze()
        {
            if (_frozenTransforms == null) return;
            try
            {
                for (int i = 0; i < _frozenTransforms.Length; i++)
                {
                    if (_frozenTransforms[i] == null) continue;
                    _frozenTransforms[i].position = _frozenPositions[i];
                    _frozenTransforms[i].rotation = _frozenRotations[i];
                }
                if (_frozenRigidbodies != null)
                {
                    foreach (var rb in _frozenRigidbodies)
                    {
                        if (rb == null) continue;
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            catch { }
        }

        private static bool IsLookingAtMe(RigManager otherRig, Vector3 myPos)
        {
            try
            {
                Vector3? headPos = null;
                Vector3? headForward = null;

                var physRig = otherRig.physicsRig;
                if (physRig != null)
                {
                    var headBone = physRig.m_head;
                    if (headBone != null)
                    {
                        headPos = ((Component)headBone).transform.position;
                        headForward = ((Component)headBone).transform.forward;
                    }
                }

                if (!headPos.HasValue)
                {
                    var rigComp = (Component)otherRig;
                    if (rigComp == null) return false;
                    headPos = rigComp.transform.position + Vector3.up * 1.5f;
                    headForward = rigComp.transform.forward;
                }

                if (!headPos.HasValue || !headForward.HasValue) return false;

                Vector3 toMe = myPos - headPos.Value;
                float dist = toMe.magnitude;
                if (dist > _viewDistance || dist < 0.5f) return false;

                float angle = Vector3.Angle(headForward.Value, toMe);
                return angle < _viewAngle * 0.5f;
            }
            catch { return false; }
        }

        private static void RefreshRigCache(RigManager myRig)
        {
            float now = Time.time;
            if (now - _lastRigCacheTime < 2f && _otherRigs.Count > 0) return;
            _lastRigCacheTime = now;
            _otherRigs.Clear();

            try
            {
                var allRigs = UnityEngine.Object.FindObjectsOfType<RigManager>();
                if (allRigs == null) return;

                foreach (var rig in allRigs)
                {
                    if (rig == null) continue;
                    if (myRig != null && rig == myRig) continue;
                    _otherRigs.Add(rig);
                }
            }
            catch { }
        }
    }
}
