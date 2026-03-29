using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Force Grab - Select items and pull them to your hand from anywhere.
    /// Supports: multi-item grab, player push/pull via Fusion, proper rig/held-item exclusion.
    /// </summary>
    public static class ForceGrabController
    {
        private static bool _enabled = false;
        private static bool _instantMode = false;
        private static bool _globalMode = false;
        private static float _flySpeed = 25f;
        private static float _grabDistance = 0.5f;
        private static bool _gripOnly = true;
        private static bool _ignorePlayerRig = true;
        private static bool _forcePush = false;
        private static float _pushForce = 30f;
        private static bool _affectPlayers = false;

        // Multi-item state
        private static readonly List<Rigidbody> _selectedRbs = new List<Rigidbody>();
        private static readonly List<GameObject> _selectedObjects = new List<GameObject>();
        private static bool _isFlying = false;
        private static bool _flyingToLeft = false;

        // Global raycast settings
        private static float _globalRayMaxDist = 500f;
        private static float _globalSphereRadius = 0.4f;

        // Cached rig transforms for IsPlayerRig check (rebuilt periodically)
        private static readonly HashSet<Transform> _rigRoots = new HashSet<Transform>();
        private static float _lastRigCacheTime = 0f;

        // Perf caches: avoid FindObjectsOfType every frame
        private static Rigidbody[] _cachedAllRigidbodies = null;
        private static float _lastRbCacheTime = -999f;
        private static RigManager[] _cachedAllRigManagers = null;
        private static float _lastRigManagerCacheTime = -999f;
        private const float RB_CACHE_INTERVAL = 0.25f;
        private const float RIGMGR_CACHE_INTERVAL = 0.5f;

        // Cached PropertyInfo for GetHeldObject (avoid reflection every frame)
        private static PropertyInfo _cachedAttachedObjectProp;
        private static PropertyInfo _cachedAttachedReceiverProp;
        private static bool _heldObjectPropsResolved = false;
        private static Type _lastHandType = null;

        public static bool IsEnabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                {
                    _isFlying = false;
                    _selectedObjects.Clear();
                    _selectedRbs.Clear();
                }
                Main.MelonLog.Msg($"Force Grab {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static bool InstantMode
        {
            get => _instantMode;
            set
            {
                _instantMode = value;
                Main.MelonLog.Msg($"Force Grab Mode: {(value ? "Instant" : "Fly")}");
            }
        }

        public static bool GlobalMode
        {
            get => _globalMode;
            set
            {
                _globalMode = value;
                Main.MelonLog.Msg($"Force Grab Global Mode: {(value ? "ON" : "OFF")}");
            }
        }

        public static float FlySpeed
        {
            get => _flySpeed;
            set => _flySpeed = Mathf.Clamp(value, 5f, 200f);
        }

        public static bool GripOnly
        {
            get => _gripOnly;
            set
            {
                _gripOnly = value;
                Main.MelonLog.Msg($"Force Grab Grip Only: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool IgnorePlayerRig
        {
            get => _ignorePlayerRig;
            set
            {
                _ignorePlayerRig = value;
                Main.MelonLog.Msg($"Force Grab Ignore Player Rig: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool ForcePush
        {
            get => _forcePush;
            set
            {
                _forcePush = value;
                Main.MelonLog.Msg($"Force Push: {(value ? "ON (Push)" : "OFF (Pull)")}");
            }
        }

        public static float PushForce
        {
            get => _pushForce;
            set => _pushForce = Mathf.Clamp(value, 5f, 500f);
        }

        public static bool AffectPlayers
        {
            get => _affectPlayers;
            set
            {
                _affectPlayers = value;
                Main.MelonLog.Msg($"Force Grab Affect Players: {(value ? "ON" : "OFF")}");
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Force Grab controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            _selectedRbs.Clear();
            _selectedObjects.Clear();
            _rigRoots.Clear();
            _lastRigCacheTime = 0f;
            _isFlying = false;
            _flyingToLeft = false;
            _cachedAllRigidbodies = null;
            _lastRbCacheTime = -999f;
            _cachedAllRigManagers = null;
            _lastRigManagerCacheTime = -999f;
        }

        /// <summary>
        /// Rebuild the set of OTHER players' RigManager root transforms (excludes own rig).
        /// </summary>
        private static void RefreshRigCache()
        {
            float now = Time.time;
            if (now - _lastRigCacheTime < 2f && _rigRoots.Count > 0) return;
            _lastRigCacheTime = now;
            _rigRoots.Clear();

            try
            {
                var myRig = Player.RigManager;
                var allRigs = GetCachedRigManagers();
                if (allRigs != null)
                {
                    foreach (var rig in allRigs)
                    {
                        if (rig == null) continue;
                        if (myRig != null && rig == myRig) continue; // skip own rig
                        var rigComp = (Component)rig;
                        if (rigComp != null)
                            _rigRoots.Add(rigComp.transform);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceGrab] Rig cache refresh error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a transform belongs to the LOCAL player's own rig. Always excluded.
        /// Checks both the RigManager hierarchy and its root parent to catch sibling rig objects.
        /// </summary>
        private static bool IsOwnRig(Transform t)
        {
            try
            {
                var myRig = Player.RigManager;
                if (myRig == null) return false;
                var myComp = (Component)myRig;
                if (myComp == null) return false;

                // Direct child check
                if (t.IsChildOf(myComp.transform))
                    return true;

                // Also check the root of the rig hierarchy — catches siblings like
                // OpenControllerRig, ControllerRig, etc. that share the same root
                Transform rigRoot = myComp.transform.root;
                if (rigRoot != null && t.IsChildOf(rigRoot))
                    return true;

                // Distance-based sanity check: if a Rigidbody is very close to
                // the player head/hands, it's likely our own rig body part
                var head = Player.Head;
                if (head != null && Vector3.Distance(t.position, head.position) < 0.05f)
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a transform belongs to another player's rig (networked). Gated by _ignorePlayerRig toggle.
        /// </summary>
        private static bool IsOtherPlayerRig(Transform t)
        {
            try
            {
                RefreshRigCache();
                foreach (var root in _rigRoots)
                {
                    if (root != null && t.IsChildOf(root))
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a transform belongs to ANY player rig (own or networked).
        /// </summary>
        private static bool IsPlayerRig(Transform t)
        {
            return IsOwnRig(t) || IsOtherPlayerRig(t);
        }

        /// <summary>
        /// Get the GameObject currently held by a specific hand, or null.
        /// </summary>
        private static Rigidbody[] GetCachedRigidbodies()
        {
            float now = Time.time;
            if (_cachedAllRigidbodies == null || now - _lastRbCacheTime > RB_CACHE_INTERVAL)
            {
                _cachedAllRigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                _lastRbCacheTime = now;
            }
            return _cachedAllRigidbodies;
        }

        private static RigManager[] GetCachedRigManagers()
        {
            float now = Time.time;
            if (_cachedAllRigManagers == null || now - _lastRigManagerCacheTime > RIGMGR_CACHE_INTERVAL)
            {
                _cachedAllRigManagers = UnityEngine.Object.FindObjectsOfType<RigManager>();
                _lastRigManagerCacheTime = now;
            }
            return _cachedAllRigManagers;
        }

        private static GameObject GetHeldObject(bool leftHand)
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null || rigManager.physicsRig == null) return null;

                var physRig = rigManager.physicsRig;
                Hand hand = leftHand ? physRig.leftHand : physRig.rightHand;
                if (hand == null) return null;

                var handType = ((object)hand).GetType();

                // Cache PropertyInfo once (or re-resolve if Hand type changes)
                if (!_heldObjectPropsResolved || _lastHandType != handType)
                {
                    _cachedAttachedObjectProp = handType.GetProperty("AttachedObject", BindingFlags.Public | BindingFlags.Instance)
                        ?? handType.GetProperty("m_CurrentAttachedGO", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _cachedAttachedReceiverProp = handType.GetProperty("AttachedReceiver", BindingFlags.Public | BindingFlags.Instance);
                    _lastHandType = handType;
                    _heldObjectPropsResolved = true;
                }

                if (_cachedAttachedObjectProp != null)
                {
                    var val = _cachedAttachedObjectProp.GetValue(hand);
                    if (val is GameObject go && go != null) return go;
                    if (val is Component comp && comp != null) return comp.gameObject;
                }

                if (_cachedAttachedReceiverProp != null)
                {
                    var val = _cachedAttachedReceiverProp.GetValue(hand);
                    if (val is Component comp && comp != null) return comp.gameObject;
                }

                var handComp = (Component)hand;
                if (handComp != null)
                {
                    var joints = handComp.gameObject.GetComponentsInChildren<ConfigurableJoint>();
                    if (joints != null)
                    {
                        foreach (var j in joints)
                        {
                            if (j != null && j.connectedBody != null && !IsPlayerRig(j.connectedBody.transform))
                                return j.connectedBody.gameObject;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsHandHoldingItem(bool leftHand)
        {
            return GetHeldObject(leftHand) != null;
        }

        /// <summary>
        /// Check if a specific GameObject is currently held in either hand.
        /// </summary>
        private static bool IsHeldByPlayer(GameObject obj)
        {
            if (obj == null) return false;
            try
            {
                var leftHeld = GetHeldObject(true);
                var rightHeld = GetHeldObject(false);

                if (leftHeld != null && (leftHeld == obj || leftHeld.transform.IsChildOf(obj.transform) || obj.transform.IsChildOf(leftHeld.transform)))
                    return true;
                if (rightHeld != null && (rightHeld == obj || rightHeld.transform.IsChildOf(obj.transform) || obj.transform.IsChildOf(rightHeld.transform)))
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Select an item from the specified hand. Adds to the multi-select list.
        /// </summary>
        public static void SelectItemFromHand(bool leftHand)
        {
            try
            {
                var hand = leftHand ? Player.LeftHand : Player.RightHand;
                if (hand == null)
                {
                    SendNotification(NotificationType.Warning, "Cannot access hand");
                    return;
                }

                Vector3 handPos = hand.transform.position;
                float closestDist = 2f;
                Rigidbody closestRb = null;

                var allRbs = GetCachedRigidbodies();
                foreach (var rb in allRbs)
                {
                    if (rb == null || rb.gameObject == null) continue;
                    if (IsOwnRig(rb.transform)) continue;
                    if (_ignorePlayerRig && IsOtherPlayerRig(rb.transform)) continue;
                    if (_gripOnly && !HasGripPoint(rb.gameObject)) continue;
                    if (IsHeldByPlayer(rb.gameObject)) continue;
                    if (_selectedRbs.Contains(rb)) continue;

                    float dist = Vector3.Distance(rb.transform.position, handPos);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestRb = rb;
                    }
                }

                if (closestRb != null)
                {
                    _selectedRbs.Add(closestRb);
                    _selectedObjects.Add(closestRb.gameObject);
                    string objName = closestRb.gameObject.name ?? "Unknown";
                    SendNotification(NotificationType.Success, $"Selected: {objName} ({_selectedRbs.Count} total)");
                    Main.MelonLog.Msg($"Force Grab selected: {objName} (dist: {closestDist:0.00}m, total: {_selectedRbs.Count})");
                }
                else
                {
                    SendNotification(NotificationType.Warning, "No object found near hand");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Force Grab select error: {ex.Message}");
            }
        }

        public static void ClearSelected()
        {
            _selectedObjects.Clear();
            _selectedRbs.Clear();
            _isFlying = false;
            SendNotification(NotificationType.Success, "Selection cleared");
            Main.MelonLog.Msg("Force Grab selection cleared");
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                bool leftGrip = false, leftTrigger = false;
                bool rightGrip = false, rightTrigger = false;

                try
                {
                    leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger") > 0.7f;
                    leftTrigger = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.7f;
                    rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger") > 0.7f;
                    rightTrigger = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.7f;
                }
                catch { }

                bool leftActive = leftGrip && leftTrigger;
                bool rightActive = rightGrip && rightTrigger;

                if (IsHandHoldingItem(true)) leftActive = false;
                if (IsHandHoldingItem(false)) rightActive = false;

                bool anyActive = leftActive || rightActive;
                bool targetLeft = leftActive && !rightActive ? true
                                : rightActive && !leftActive ? false
                                : true;

                if (anyActive)
                {
                    if (_globalMode)
                    {
                        TryGlobalRaycastSelect(targetLeft);
                    }

                    if (_selectedRbs.Count > 0)
                    {
                        PerformForceGrab(targetLeft);
                    }

                    if (_affectPlayers)
                    {
                        ApplyForceToPlayers(targetLeft);
                    }
                }
                else
                {
                    _isFlying = false;
                }
            }
            catch { }
        }

        private static void TryGlobalRaycastSelect(bool useLeft)
        {
            try
            {
                var head = Player.Head;
                var hand = useLeft ? Player.LeftHand : Player.RightHand;
                if (head == null && hand == null) return;

                if (head != null)
                {
                    Vector3 headOrigin = head.position;
                    Vector3 headDir = head.forward;

                    RaycastHit hit;
                    if (Physics.Raycast(headOrigin, headDir, out hit, _globalRayMaxDist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        TryAddFromHit(hit, useLeft, "Head ray");
                    }

                    if (Physics.SphereCast(headOrigin, 0.5f, headDir, out hit, _globalRayMaxDist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        TryAddFromHit(hit, useLeft, "Head sphere");
                    }
                }

                if (hand != null)
                {
                    Vector3 handOrigin = hand.transform.position;
                    Vector3[] directions = new Vector3[]
                    {
                        hand.transform.forward,
                        -hand.transform.up,
                        (hand.transform.forward - hand.transform.up).normalized
                    };

                    foreach (var dir in directions)
                    {
                        RaycastHit hit;
                        if (Physics.SphereCast(handOrigin, _globalSphereRadius, dir, out hit, _globalRayMaxDist, ~0, QueryTriggerInteraction.Ignore))
                        {
                            TryAddFromHit(hit, useLeft, "Hand cast");
                        }
                    }
                }

                // Cone search: grab ALL qualifying objects in a tight cone
                if (head != null)
                {
                    Vector3 headOrigin = head.position;
                    Vector3 headDir = head.forward;

                    var allRbs = GetCachedRigidbodies();
                    foreach (var rb in allRbs)
                    {
                        if (rb == null || rb.gameObject == null) continue;
                        if (_selectedRbs.Contains(rb)) continue;
                        if (IsOwnRig(rb.transform)) continue;
                        if (_ignorePlayerRig && IsOtherPlayerRig(rb.transform)) continue;
                        if (_gripOnly && !HasGripPoint(rb.gameObject)) continue;
                        if (IsHeldByPlayer(rb.gameObject)) continue;

                        Vector3 toObj = rb.transform.position - headOrigin;
                        float dist = toObj.magnitude;
                        if (dist < 0.5f || dist > _globalRayMaxDist) continue;

                        float angle = Vector3.Angle(headDir, toObj);
                        if (angle > 5f) continue;

                        AddToSelection(rb, useLeft, "Cone");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Global raycast error: {ex.Message}");
            }
        }

        private static void TryAddFromHit(RaycastHit hit, bool useLeft, string source)
        {
            var rb = FindRigidbodyOnHit(hit);
            if (rb == null) return;
            if (_selectedRbs.Contains(rb)) return;
            if (IsOwnRig(rb.transform)) return;
            if (_ignorePlayerRig && IsOtherPlayerRig(rb.transform)) return;
            if (_gripOnly && !HasGripPoint(rb.gameObject)) return;
            if (IsHeldByPlayer(rb.gameObject)) return;

            AddToSelection(rb, useLeft, source);
        }

        private static void AddToSelection(Rigidbody rb, bool useLeft, string source)
        {
            if (_selectedRbs.Contains(rb)) return;

            _selectedRbs.Add(rb);
            _selectedObjects.Add(rb.gameObject);
            _isFlying = true;
            _flyingToLeft = useLeft;

            TryFusionSyncForceGrab(rb.gameObject, useLeft);
            Main.MelonLog.Msg($"[ForceGrab Global] {source} added: {rb.gameObject.name} (total: {_selectedRbs.Count})");
        }

        private static Rigidbody FindRigidbodyOnHit(RaycastHit hit)
        {
            if (hit.collider == null) return null;
            var rb = hit.collider.GetComponentInParent<Rigidbody>();
            if (rb != null) return rb;
            rb = hit.collider.GetComponent<Rigidbody>();
            if (rb != null) return rb;
            rb = hit.collider.GetComponentInChildren<Rigidbody>();
            return rb;
        }

        /// <summary>
        /// Apply force grab to ALL selected objects.
        /// </summary>
        private static void PerformForceGrab(bool useLeft)
        {
            var hand = useLeft ? Player.LeftHand : Player.RightHand;
            if (hand == null) return;

            Vector3 handPos = hand.transform.position;
            _isFlying = true;
            _flyingToLeft = useLeft;

            for (int i = _selectedRbs.Count - 1; i >= 0; i--)
            {
                var rb = _selectedRbs[i];
                var obj = _selectedObjects[i];

                if (rb == null || obj == null)
                {
                    _selectedRbs.RemoveAt(i);
                    _selectedObjects.RemoveAt(i);
                    continue;
                }

                if (IsHeldByPlayer(obj))
                {
                    _selectedRbs.RemoveAt(i);
                    _selectedObjects.RemoveAt(i);
                    continue;
                }

                Vector3 objPos = obj.transform.position;
                float distance = Vector3.Distance(handPos, objPos);

                if (_forcePush)
                {
                    if (rb.isKinematic) rb.isKinematic = false;
                    Vector3 pushDir = (objPos - handPos).normalized;
                    rb.velocity = pushDir * _pushForce;
                }
                else if (_instantMode)
                {
                    obj.transform.position = handPos;
                    if (rb.isKinematic) rb.isKinematic = false;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else
                {
                    if (rb.isKinematic) rb.isKinematic = false;

                    if (distance < _grabDistance)
                    {
                        rb.velocity = Vector3.zero;
                        obj.transform.position = handPos;
                    }
                    else
                    {
                        Vector3 dir = (handPos - objPos).normalized;
                        float speed = Mathf.Max(_flySpeed, _flySpeed * (2f / Mathf.Max(distance, 0.1f)));
                        rb.velocity = dir * speed;
                    }
                }
            }
        }

        /// <summary>
        /// Apply push/pull force to other players' rigs in your look direction.
        /// </summary>
        private static void ApplyForceToPlayers(bool useLeft)
        {
            try
            {
                var hand = useLeft ? Player.LeftHand : Player.RightHand;
                if (hand == null) return;

                Vector3 handPos = hand.transform.position;
                Vector3 headPos = Player.Head != null ? Player.Head.position : handPos;
                Vector3 lookDir = Player.Head != null ? Player.Head.forward : hand.transform.forward;

                var allRigs = GetCachedRigManagers();
                if (allRigs == null) return;

                var myRig = Player.RigManager;

                foreach (var rig in allRigs)
                {
                    if (rig == null) continue;
                    if (myRig != null && rig == myRig) continue;

                    var rigComp = (Component)rig;
                    if (rigComp == null) continue;
                    Vector3 rigPos = rigComp.transform.position;
                    float angle = Vector3.Angle(lookDir, rigPos - headPos);
                    if (angle > 30f) continue;

                    var physRig = rig.physicsRig;
                    if (physRig == null) continue;

                    Vector3 forceDir = _forcePush
                        ? (rigPos - handPos).normalized
                        : (handPos - rigPos).normalized;

                    try
                    {
                        var pelvisRb = physRig.torso?.rbPelvis;
                        if (pelvisRb != null)
                            pelvisRb.AddForce(forceDir * _pushForce, ForceMode.VelocityChange);
                    }
                    catch { }

                    try
                    {
                        var rigRbs = ((Component)physRig).GetComponentsInChildren<Rigidbody>();
                        if (rigRbs != null)
                        {
                            foreach (var rb in rigRbs)
                            {
                                if (rb != null)
                                    rb.AddForce(forceDir * _pushForce * 0.5f, ForceMode.VelocityChange);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceGrab] Player force error: {ex.Message}");
            }
        }

        private static bool HasGripPoint(GameObject obj)
        {
            if (obj == null) return false;
            try
            {
                var host = obj.GetComponentInChildren<InteractableHost>();
                if (host == null) host = obj.GetComponentInParent<InteractableHost>();

                if (host != null)
                {
                    if (host.IsStatic) return false;
                    if (host.IsInteractionDisabled) return false;
                    if (!host.HasRigidbody) return false;

                    var fpGrip = host.GetForcePullGrip();
                    if (fpGrip != null) return true;

                    var fpGrips = host._fpGrips;
                    if (fpGrips != null && fpGrips.Count > 0) return true;

                    return false;
                }

                var directFpGrip = obj.GetComponentInChildren<ForcePullGrip>();
                if (directFpGrip != null) return true;
                directFpGrip = obj.GetComponentInParent<ForcePullGrip>();
                if (directFpGrip != null) return true;
            }
            catch { }
            return false;
        }

        private static Grip FindGrip(GameObject obj)
        {
            if (obj == null) return null;
            try
            {
                var host = obj.GetComponentInChildren<InteractableHost>();
                if (host == null) host = obj.GetComponentInParent<InteractableHost>();

                if (host != null)
                {
                    var grip = host.GetGrip();
                    if (grip != null) return grip;

                    var grips = host._grips;
                    if (grips != null && grips.Count > 0) return grips[0];
                }

                var directGrip = obj.GetComponentInChildren<Grip>();
                if (directGrip != null) return directGrip;
                directGrip = obj.GetComponentInParent<Grip>();
                return directGrip;
            }
            catch { return null; }
        }

        private static void TryFusionSyncForceGrab(GameObject obj, bool useLeft)
        {
            try
            {
                if (!IsInMultiplayerServer()) return;

                var grip = FindGrip(obj);
                if (grip == null)
                {
                    Main.MelonLog.Msg("[ForceGrab] No Grip found for Fusion sync - skipping ownership request");
                    return;
                }

                var rigManager = Player.RigManager;
                if (rigManager == null || rigManager.physicsRig == null) return;

                Hand hand = useLeft ? rigManager.physicsRig.leftHand : rigManager.physicsRig.rightHand;
                if (hand == null) return;

                var grabHelperType = FindTypeByName("GrabHelper");
                if (grabHelperType == null)
                {
                    Main.MelonLog.Msg("[ForceGrab] GrabHelper type not found - Fusion sync skipped");
                    return;
                }

                var forcePullMethod = grabHelperType.GetMethod("SendObjectForcePull",
                    BindingFlags.Public | BindingFlags.Static);
                if (forcePullMethod == null)
                {
                    Main.MelonLog.Msg("[ForceGrab] SendObjectForcePull method not found");
                    return;
                }

                forcePullMethod.Invoke(null, new object[] { hand, grip });
                Main.MelonLog.Msg($"[ForceGrab] Fusion sync: SendObjectForcePull sent for {obj.name}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceGrab] Fusion sync error: {ex.Message}");
            }
        }

        private static bool IsInMultiplayerServer()
        {
            try
            {
                var networkInfoType = FindTypeByName("NetworkInfo");
                if (networkInfoType == null) return false;

                var hasServerProp = networkInfoType.GetProperty("HasServer",
                    BindingFlags.Public | BindingFlags.Static);
                if (hasServerProp == null) return false;

                return (bool)hasServerProp.GetValue(null);
            }
            catch { return false; }
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch { }
            }
            return null;
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}
