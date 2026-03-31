using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Audio;
using Il2CppSLZ.Bonelab;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace BonelabUtilityMod
{
    public enum RagdollBinding
    {
        NONE,
        THUMBSTICK_PRESS,
        DOUBLE_TAP_B
    }

    public enum RagdollHand
    {
        RIGHT_HAND,
        LEFT_HAND
    }

    /// <summary>
    /// Ragdoll mode: LIMP = full ragdoll, ARM_CONTROL = arms follow controllers, legs limp.
    /// </summary>
    public enum RagdollMode
    {
        LIMP,
        ARM_CONTROL
    }

    /// <summary>
    /// Ragdoll controller that combines:
    ///   - Grab-based ragdolling (head/neck, both arms, body)
    ///   - Physics-based ragdolling ported from RagdollExt (fall, impact, launch, slip, wall push)
    ///   - Death persistence (stay ragdolled after dying, maintain arm control)
    /// Does NOT auto-unragdoll. Player must manually recover.
    /// </summary>
    public static class RagdollController
    {
        // ───── Master Toggle ─────
        private static bool _enabled = false;

        // ───── Mode ─────
        private static RagdollMode _mode = RagdollMode.LIMP;
        private static bool _tantrumMode = false; // ENABLED = old basic ragdoll, DISABLED = new LIMP/ARM_CONTROL

        // ───── Grab Detection ─────
        private static bool _grabEnabled = true;
        private static bool _neckGrabDisablesArms = true; // Auto-switch to LIMP on neck grab
        private static bool _armGrabEnabled = true; // Single arm grab with 2.5x mass diff triggers ragdoll

        // ───── VR Controller Keybind ─────
        private static RagdollBinding _binding = RagdollBinding.NONE;
        private static RagdollHand _keybindHand = RagdollHand.RIGHT_HAND;

        // Double-tap B state
        private const float DOUBLE_TAP_TIMER = 0.32f;
        private static float _lastTimeInput = 0f;
        private static bool _ragdollNextButton = false;

        // Footstep SFX muting
        private static List<FootstepSFX> _footsteps = new List<FootstepSFX>();

        // ───── Physics Ragdoll (ported from RagdollExt) ─────
        private static bool _fallEnabled = true;
        private static float _fallVelocityThreshold = 10f;

        private static bool _impactEnabled = true;
        private static float _impactThreshold = 10f;

        private static bool _launchEnabled = true;
        private static float _launchThreshold = 10f;

        private static bool _slipEnabled = false;
        private static float _slipFrictionThreshold = 0.15f;
        private static float _slipVelocityThreshold = 3f;

        private static bool _wallPushEnabled = true;
        private static float _wallPushVelocityThreshold = 3.2f;

        // ───── Internal State ─────
        private static PhysGrounder _grounder;
        private static bool _sceneLoaded = false;
        private static Vector3 _lastVelocity;
        private static float _nextRagdollTime = 0f;
        private static float _ragdollCooldownUntil = 0f;
        private const float COOLDOWN = 2f;

        // Grab transition protection: suppress physics ragdoll briefly after grab/release
        private static bool _wasHoldingLeft = false;
        private static bool _wasHoldingRight = false;
        private static float _grabTransitionCooldownUntil = 0f;
        private const float GRAB_TRANSITION_COOLDOWN = 1.5f;

        // Saved head rotation before ragdoll so we can restore it on un-ragdoll
        private static Quaternion _savedHeadLocalRotation = Quaternion.identity;
        private static bool _hasHeadRotationSaved = false;

        // Cache for arm grip scanning
        private static Grip[] _cachedRigGrips = null;
        private static float _lastGripCacheTime = 0f;
        private const float GRIP_CACHE_INTERVAL = 2f;

        // ───── Properties ─────
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Ragdoll Controller {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static bool GrabEnabled { get => _grabEnabled; set => _grabEnabled = value; }
        public static bool NeckGrabDisablesArms { get => _neckGrabDisablesArms; set => _neckGrabDisablesArms = value; }
        public static bool ArmGrabEnabled { get => _armGrabEnabled; set => _armGrabEnabled = value; }

        public static bool FallEnabled { get => _fallEnabled; set => _fallEnabled = value; }
        public static float FallVelocityThreshold
        {
            get => _fallVelocityThreshold;
            set => _fallVelocityThreshold = Mathf.Clamp(value, 1f, 50f);
        }

        public static bool ImpactEnabled { get => _impactEnabled; set => _impactEnabled = value; }
        public static float ImpactThreshold
        {
            get => _impactThreshold;
            set => _impactThreshold = Mathf.Clamp(value, 1f, 50f);
        }

        public static bool LaunchEnabled { get => _launchEnabled; set => _launchEnabled = value; }
        public static float LaunchThreshold
        {
            get => _launchThreshold;
            set => _launchThreshold = Mathf.Clamp(value, 1f, 50f);
        }

        public static bool SlipEnabled { get => _slipEnabled; set => _slipEnabled = value; }
        public static float SlipFrictionThreshold
        {
            get => _slipFrictionThreshold;
            set => _slipFrictionThreshold = Mathf.Clamp(value, 0.01f, 1f);
        }
        public static float SlipVelocityThreshold
        {
            get => _slipVelocityThreshold;
            set => _slipVelocityThreshold = Mathf.Clamp(value, 0.5f, 20f);
        }

        public static bool WallPushEnabled { get => _wallPushEnabled; set => _wallPushEnabled = value; }
        public static float WallPushVelocityThreshold
        {
            get => _wallPushVelocityThreshold;
            set => _wallPushVelocityThreshold = Mathf.Clamp(value, 0.1f, 20f);
        }

        public static RagdollMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                Main.MelonLog.Msg($"Ragdoll Mode: {value}");
            }
        }

        public static bool TantrumMode
        {
            get => _tantrumMode;
            set
            {
                _tantrumMode = value;
                Main.MelonLog.Msg($"Tantrum Mode: {(value ? "ON (flailing ragdoll)" : "OFF")}");
            }
        }

        public static RagdollBinding Binding { get => _binding; set => _binding = value; }
        public static RagdollHand KeybindHand { get => _keybindHand; set => _keybindHand = value; }

        // ───── Footstep SFX Suppression ─────

        public static bool DisableFootstep(FootstepSFX sfx)
        {
            if (!_enabled) return false;
            if (!_footsteps.Contains(sfx)) return false;
            var physRig = Player.PhysicsRig;
            if (physRig == null) return false;
            return physRig.torso.shutdown || !physRig.ballLocoEnabled;
        }

        // ───── Initialization ─────

        public static void Initialize()
        {
            Hooking.OnLevelLoaded += (info) => OnSceneLoaded();
            Hooking.OnPlayerDeath += OnPlayerDeathHook;
            Hooking.OnPlayerResurrected += OnPlayerResurrectedHook;
            DisableExternalRagdollMods();
            Main.MelonLog.Msg("RagdollController initialized");
        }

        /// <summary>
        /// Detect and disable external ragdoll mods (RagdollPlayer / RagdollExt)
        /// so our implementation takes priority.
        /// </summary>
        private static void DisableExternalRagdollMods()
        {
            try
            {
                foreach (var mod in MelonMod.RegisteredMelons)
                {
                    string name = mod.Info?.Name ?? "";
                    if (name.Contains("Ragdoll Player") || name.Contains("RagdollExt") || name.Contains("Ragdoll Ext"))
                    {
                        var type = mod.GetType();
                        // RagdollPlayerMod — static property IsEnabled
                        var prop = type.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Static);
                        if (prop != null && prop.CanWrite) prop.SetValue(null, false);
                        // RagdollExt Core — static field ModEnabled
                        var field = type.GetField("ModEnabled", BindingFlags.Public | BindingFlags.Static);
                        if (field != null) field.SetValue(null, false);
                        // Remove their Harmony patches
                        try { mod.HarmonyInstance?.UnpatchSelf(); } catch { }
                        Main.MelonLog.Msg($"Overrode external ragdoll mod: {name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"External ragdoll override: {ex.Message}");
            }
        }

        private static void OnSceneLoaded()
        {
            _sceneLoaded = true;
            _cachedRigGrips = null;
            _lastGripCacheTime = 0f;
            try
            {
                if (Player.PhysicsRig != null)
                    _grounder = Player.PhysicsRig.feet.GetComponent<PhysGrounder>();
            }
            catch { }
            _lastVelocity = Vector3.zero;
            _nextRagdollTime = 0f;
            _wasHoldingLeft = false;
            _wasHoldingRight = false;
            _grabTransitionCooldownUntil = 0f;
            _lastTimeInput = 0f;
            _ragdollNextButton = false;

            // Cache footstep SFX for muting during ragdoll
            try
            {
                _footsteps = new List<FootstepSFX>();
                var footstepsArr = Player.RigManager.animationRig.body.locomotion.footsteps;
                _footsteps.Add(footstepsArr[0].stepSfx);
                _footsteps.Add(footstepsArr[1].stepSfx);
            }
            catch { _footsteps = new List<FootstepSFX>(); }

            // Re-run in case mods loaded after us
            DisableExternalRagdollMods();
        }

        // ───── Main Update ─────

        public static void Update()
        {
            if (!_enabled) return;
            if (!_sceneLoaded) return;

            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                // === VR Controller Keybind (works regardless of cooldowns, including in vehicles) ===
                if (!UIRig.Instance.popUpMenu.m_IsCursorShown &&
                    CheckKeybindInput())
                {
                    if (!physRig.torso.shutdown && physRig.ballLocoEnabled)
                    {
                        ManualRagdoll(physRig);
                        _nextRagdollTime = Time.time + COOLDOWN;
                        Main.MelonLog.Msg($"Ragdoll toggled ON ({_mode})");
                    }
                    else
                    {
                        UnragdollPlayer(physRig);
                        Main.MelonLog.Msg("Ragdoll toggled OFF");
                    }
                }

                // Skip auto-ragdoll while on death cooldown
                if (Time.time < _ragdollCooldownUntil) return;

                // Detect grab/release transitions to suppress physics triggers
                UpdateGrabTransitionCooldown(physRig);

                // Check if already ragdolled in ARM_CONTROL — neck grab can force LIMP live
                if (_neckGrabDisablesArms && _grabEnabled && _mode == RagdollMode.ARM_CONTROL &&
                    (!physRig.ballLocoEnabled || physRig.torso.shutdown))
                {
                    CheckNeckGrabForceLimp(rigManager, physRig);
                }

                // Don't process if already ragdolled or shut down
                if (physRig.torso.shutdown || !physRig.ballLocoEnabled)
                {
                    _lastVelocity = physRig.wholeBodyVelocity;
                    return;
                }

                if (Time.time < _nextRagdollTime)
                {
                    _lastVelocity = physRig.wholeBodyVelocity;
                    return;
                }

                // === Grab Detection ===
                if (_grabEnabled)
                {
                    CheckGrabRagdoll(rigManager, physRig);
                }

                // === Physics-based Ragdoll ===
                // Skip physics triggers during grab transitions (network guns cause velocity spikes)
                if (Time.time < _grabTransitionCooldownUntil)
                {
                    _lastVelocity = physRig.wholeBodyVelocity;
                    return;
                }

                CheckPhysicsRagdoll(rigManager, physRig);
            }
            catch { }
        }

        /// <summary>
        /// Check VR controller keybind input based on binding type.
        /// Supports thumbstick press and double-tap B button.
        /// </summary>
        private static bool CheckKeybindInput()
        {
            try
            {
                var controller = _keybindHand == RagdollHand.LEFT_HAND
                    ? Player.LeftController : Player.RightController;
                if (controller == null) return false;

                switch (_binding)
                {
                    case RagdollBinding.NONE:
                        return false;

                    case RagdollBinding.THUMBSTICK_PRESS:
                        _lastTimeInput = 0f;
                        _ragdollNextButton = false;
                        return controller.GetThumbStickDown();

                    case RagdollBinding.DOUBLE_TAP_B:
                        bool bDown = controller.GetBButtonDown();
                        float now = Time.realtimeSinceStartup;

                        if (bDown && _ragdollNextButton)
                        {
                            if (now - _lastTimeInput <= DOUBLE_TAP_TIMER)
                            {
                                _ragdollNextButton = false;
                                _lastTimeInput = 0f;
                                return true;
                            }
                            _ragdollNextButton = false;
                            _lastTimeInput = 0f;
                        }
                        else if (bDown)
                        {
                            _lastTimeInput = now;
                            _ragdollNextButton = true;
                        }
                        else if (now - _lastTimeInput > DOUBLE_TAP_TIMER)
                        {
                            _ragdollNextButton = false;
                            _lastTimeInput = 0f;
                        }
                        return false;

                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Detect when the player grabs or releases an object and set a cooldown
        /// to suppress physics-based ragdoll triggers during the transition.
        /// Networked guns cause velocity spikes during ownership transfer.
        /// </summary>
        private static void UpdateGrabTransitionCooldown(PhysicsRig physRig)
        {
            try
            {
                bool holdingLeft = physRig.leftHand != null && physRig.leftHand.m_CurrentAttachedGO != null;
                bool holdingRight = physRig.rightHand != null && physRig.rightHand.m_CurrentAttachedGO != null;

                if (holdingLeft != _wasHoldingLeft || holdingRight != _wasHoldingRight)
                {
                    _grabTransitionCooldownUntil = Time.time + GRAB_TRANSITION_COOLDOWN;
                    _lastVelocity = physRig.wholeBodyVelocity;
                }

                _wasHoldingLeft = holdingLeft;
                _wasHoldingRight = holdingRight;
            }
            catch { }
        }



        // ═══════════════════════════════════════════════════
        // GRAB DETECTION
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// While already ragdolled in ARM_CONTROL, check if neck is grabbed
        /// and force switch to LIMP (disable arm control live).
        /// </summary>
        private static void CheckNeckGrabForceLimp(RigManager rigManager, PhysicsRig physRig)
        {
            try
            {
                var torso = physRig.torso;
                if (torso == null) return;

                float myMass = GetRigMass(rigManager);
                var neckGrabber = GetExternalGrabber(torso.gNeck, rigManager);

                if (neckGrabber != null)
                {
                    float grabberMass = GetRigMass(neckGrabber);
                    if (grabberMass > myMass * 0.9f)
                    {
                        // Force full LIMP — shutdown the whole rig
                        ForceRagdollLimp(physRig, "Neck Grab while ARM_CONTROL");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Detect grab-based ragdoll scenarios:
        ///   1. Head or neck grabbed by someone bigger (neck grab can force LIMP)
        ///   2. Single arm grabbed by someone with 2.5x+ mass difference
        ///   3. Both arms grabbed separately by someone equal/bigger
        ///   4. Body grabbed with both hands by someone significantly stronger
        /// </summary>
        private static void CheckGrabRagdoll(RigManager rigManager, PhysicsRig physRig)
        {
            try
            {
                var torso = physRig.torso;
                if (torso == null) return;

                float myMass = GetRigMass(rigManager);

                // --- 1. Head or Neck grabbed by someone bigger ---
                var headGrabber = GetExternalGrabber(torso.gHead, rigManager);
                var neckGrabber = GetExternalGrabber(torso.gNeck, rigManager);

                if (headGrabber != null)
                {
                    float grabberMass = GetRigMass(headGrabber);
                    if (grabberMass > myMass * 0.9f) // bigger (with small tolerance)
                    {
                        TriggerRagdoll(rigManager, "Head Grab", false);
                        return;
                    }
                }

                if (neckGrabber != null)
                {
                    float grabberMass = GetRigMass(neckGrabber);
                    if (grabberMass > myMass * 0.9f)
                    {
                        // If neck grab disables arms is on, force LIMP regardless of current mode
                        if (_neckGrabDisablesArms && _mode == RagdollMode.ARM_CONTROL)
                        {
                            ForceRagdollLimp(physRig, "Neck Grab (arms disabled)");
                        }
                        else
                        {
                            TriggerRagdoll(rigManager, "Neck Grab", false);
                        }
                        return;
                    }
                }

                // --- 2. Arm grabs ---
                // Scan all grips on our rig to find arm grips with external hands
                RefreshGripCache(rigManager);

                RigManager leftArmGrabber = null;
                RigManager rightArmGrabber = null;

                if (_cachedRigGrips != null)
                {
                    Transform leftHandTf = physRig.leftHand != null ? ((Component)physRig.leftHand).transform : null;
                    Transform rightHandTf = physRig.rightHand != null ? ((Component)physRig.rightHand).transform : null;
                    Transform myRoot = ((Component)rigManager).transform;

                    // Get root GameObjects of held items so we can skip their grips
                    GameObject leftHeldRoot = null;
                    GameObject rightHeldRoot = null;
                    try { if (physRig.leftHand?.m_CurrentAttachedGO != null) leftHeldRoot = physRig.leftHand.m_CurrentAttachedGO.transform.root.gameObject; } catch { }
                    try { if (physRig.rightHand?.m_CurrentAttachedGO != null) rightHeldRoot = physRig.rightHand.m_CurrentAttachedGO.transform.root.gameObject; } catch { }

                    foreach (var grip in _cachedRigGrips)
                    {
                        if (grip == null) continue;
                        if (!grip.HasAttachedHands()) continue;

                        // Skip known torso grips (already handled above)
                        if (IsTorsoGrip(grip, torso)) continue;

                        // Skip grips that belong to a held object (e.g. gun grips)
                        // These are NOT body grips — they're item grips that ended up
                        // in the rig hierarchy because the item is held in-hand.
                        Transform gripRoot = ((Component)grip).transform.root;
                        if (leftHeldRoot != null && gripRoot.gameObject == leftHeldRoot) continue;
                        if (rightHeldRoot != null && gripRoot.gameObject == rightHeldRoot) continue;

                        // Also skip if grip is NOT a child of the rig root itself
                        // (held items parent under the hand but their root is the item, not the rig)
                        if (gripRoot != myRoot) continue;

                        var externalGrabber = GetExternalGrabberFromGrip(grip, rigManager);
                        if (externalGrabber == null) continue;

                        // Determine which side this grip is on by checking if it's
                        // closer to the left or right hand
                        Transform gripTf = ((Component)grip).transform;
                        bool isLeftSide = false;
                        bool isRightSide = false;

                        if (leftHandTf != null && rightHandTf != null)
                        {
                            float distLeft = Vector3.Distance(gripTf.position, leftHandTf.position);
                            float distRight = Vector3.Distance(gripTf.position, rightHandTf.position);
                            // Also check if it's a child of the hand transform
                            if (gripTf.IsChildOf(leftHandTf) || distLeft < distRight)
                                isLeftSide = true;
                            else
                                isRightSide = true;
                        }
                        else
                        {
                            // Fallback: use x position relative to rig center
                            Vector3 localPos = myRoot.InverseTransformPoint(gripTf.position);
                            if (localPos.x < 0) isLeftSide = true;
                            else isRightSide = true;
                        }

                        if (isLeftSide && leftArmGrabber == null) leftArmGrabber = externalGrabber;
                        if (isRightSide && rightArmGrabber == null) rightArmGrabber = externalGrabber;
                    }
                }

                if (leftArmGrabber != null && rightArmGrabber != null)
                {
                    // At least one grabber must be equal or bigger
                    float leftMass = GetRigMass(leftArmGrabber);
                    float rightMass = GetRigMass(rightArmGrabber);
                    if (leftMass >= myMass * 0.9f || rightMass >= myMass * 0.9f)
                    {
                        TriggerRagdoll(rigManager, "Both Arms Grabbed", false);
                        return;
                    }
                }

                // --- 2b. Single arm grabbed by someone with 2.5x+ mass difference ---
                if (_armGrabEnabled)
                {
                    if (leftArmGrabber != null)
                    {
                        float grabberMass = GetRigMass(leftArmGrabber);
                        if (grabberMass >= myMass * 2.5f)
                        {
                            TriggerRagdoll(rigManager, "Left Arm Grabbed (Much Bigger)", false);
                            return;
                        }
                    }
                    if (rightArmGrabber != null)
                    {
                        float grabberMass = GetRigMass(rightArmGrabber);
                        if (grabberMass >= myMass * 2.5f)
                        {
                            TriggerRagdoll(rigManager, "Right Arm Grabbed (Much Bigger)", false);
                            return;
                        }
                    }
                }

                // --- 3. Body grabbed: one hand if WAY bigger, both hands if slightly bigger ---
                int bodyGrabCount = 0;
                RigManager bodyGrabber = null;

                Grip[] bodyGrips = { torso.gChest, torso.gSpine, torso.gPelvis };
                foreach (var grip in bodyGrips)
                {
                    if (grip == null) continue;
                    var grabber = GetExternalGrabber(grip, rigManager);
                    if (grabber != null)
                    {
                        bodyGrabber = grabber;
                        bodyGrabCount += CountExternalHands(grip, rigManager);
                    }
                }

                // Also count hands on head/neck for body grab total (if same grabber has both hands on body)
                if (torso.gHead != null)
                    bodyGrabCount += CountExternalHands(torso.gHead, rigManager);
                if (torso.gNeck != null)
                    bodyGrabCount += CountExternalHands(torso.gNeck, rigManager);

                if (bodyGrabber != null && bodyGrabCount >= 1)
                {
                    float grabberMass = GetRigMass(bodyGrabber);
                    float massRatio = grabberMass / myMass;

                    // One hand is enough if grabber is WAY bigger (2x+ mass)
                    // Two hands required if only slightly bigger (1.3x–2x mass)
                    if (massRatio >= 2.0f && bodyGrabCount >= 1)
                    {
                        TriggerRagdoll(rigManager, "Body Grabbed (Much Bigger)", false);
                        return;
                    }
                    else if (massRatio >= 1.3f && bodyGrabCount >= 2)
                    {
                        TriggerRagdoll(rigManager, "Body Grabbed (Both Hands)", false);
                        return;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get the RigManager of an external entity grabbing a specific Grip.
        /// Returns null if no external hand is attached.
        /// </summary>
        private static RigManager GetExternalGrabber(Grip grip, RigManager myRig)
        {
            if (grip == null) return null;
            try
            {
                if (!grip.HasAttachedHands()) return null;
                return GetExternalGrabberFromGrip(grip, myRig);
            }
            catch { return null; }
        }

        private static RigManager GetExternalGrabberFromGrip(Grip grip, RigManager myRig)
        {
            try
            {
                var hands = grip.attachedHands;
                if (hands == null) return null;
                for (int i = 0; i < hands.Count; i++)
                {
                    var hand = hands[i];
                    if (hand == null) continue;
                    if (hand.manager != null && hand.manager != myRig)
                        return hand.manager;
                }
            }
            catch { }
            return null;
        }

        private static int CountExternalHands(Grip grip, RigManager myRig)
        {
            int count = 0;
            try
            {
                if (grip == null || !grip.HasAttachedHands()) return 0;
                var hands = grip.attachedHands;
                if (hands == null) return 0;
                for (int i = 0; i < hands.Count; i++)
                {
                    var hand = hands[i];
                    if (hand != null && hand.manager != null && hand.manager != myRig)
                        count++;
                }
            }
            catch { }
            return count;
        }

        private static bool IsTorsoGrip(Grip grip, PhysTorso torso)
        {
            return grip == torso.gHead || grip == (Grip)torso.gNeck ||
                   grip == torso.gChest || grip == torso.gSpine || grip == torso.gPelvis;
        }

        /// <summary>
        /// Get approximate mass of a rig for size comparison.
        /// Uses the sum of all rigidbody masses on the physics rig.
        /// </summary>
        private static float GetRigMass(RigManager rig)
        {
            try
            {
                if (rig == null) return 1f;
                var physRig = rig.physicsRig;
                if (physRig == null) return 1f;

                float mass = 0f;

                // Get torso body masses
                var torso = physRig.torso;
                if (torso != null)
                {
                    if (torso.rbHead != null) mass += torso.rbHead.mass;
                    if (torso.rbChest != null) mass += torso.rbChest.mass;
                    if (torso.rbSpine != null) mass += torso.rbSpine.mass;
                    if (torso.rbPelvis != null) mass += torso.rbPelvis.mass;
                }

                // Add hand masses
                if (physRig.leftHand != null && physRig.leftHand.rb != null)
                    mass += physRig.leftHand.rb.mass;
                if (physRig.rightHand != null && physRig.rightHand.rb != null)
                    mass += physRig.rightHand.rb.mass;

                return mass > 0f ? mass : 1f;
            }
            catch { return 1f; }
        }

        private static void RefreshGripCache(RigManager rigManager)
        {
            if (Time.time - _lastGripCacheTime < GRIP_CACHE_INTERVAL && _cachedRigGrips != null)
                return;

            try
            {
                _cachedRigGrips = ((Component)rigManager).GetComponentsInChildren<Grip>();
                _lastGripCacheTime = Time.time;
            }
            catch
            {
                _cachedRigGrips = null;
            }
        }

        // ═══════════════════════════════════════════════════
        // PHYSICS-BASED RAGDOLL (ported from RagdollExt)
        // ═══════════════════════════════════════════════════

        private static void CheckPhysicsRagdoll(RigManager rigManager, PhysicsRig physRig)
        {
            try
            {
                Vector3 velocity = physRig.wholeBodyVelocity;
                Vector3 deltaV = velocity - _lastVelocity;
                float yVel = velocity.y;
                float deltaMag = deltaV.magnitude;

                // Check if player is holding a FlyingGun (nimbus gun) — skip fall/slip checks
                bool holdingFlyingGun = IsHoldingFlyingGun(physRig);

                // --- Fall Detection ---
                if (_fallEnabled && !holdingFlyingGun)
                {
                    if (_grounder != null && !_grounder.isGrounded && yVel < -_fallVelocityThreshold)
                    {
                        TriggerRagdoll(rigManager, "Fall", false);
                        _lastVelocity = velocity;
                        return;
                    }
                }

                // --- Impact Detection ---
                if (deltaMag > 0.1f)
                {
                    float dot = Vector3.Dot(deltaV.normalized, velocity.normalized);

                    if (_impactEnabled && dot < -0.5f && deltaMag > _impactThreshold)
                    {
                        TriggerRagdoll(rigManager, "Impact", true);
                        _lastVelocity = velocity;
                        return;
                    }

                    // --- Launch Detection ---
                    if (_launchEnabled && dot > 0.5f && deltaMag > _launchThreshold)
                    {
                        TriggerRagdoll(rigManager, "Launch", false);
                        _lastVelocity = velocity;
                        return;
                    }
                }

                // --- Slip Detection ---
                if (_slipEnabled && !holdingFlyingGun && _grounder != null && _grounder.isGrounded)
                {
                    Vector3 horizontalVel = velocity;
                    horizontalVel.y = 0f;
                    float friction = GetGroundFriction(physRig);
                    bool hardLanding = Mathf.Abs(deltaV.y) > 4f;

                    if (friction < _slipFrictionThreshold &&
                        (horizontalVel.magnitude > _slipVelocityThreshold || hardLanding))
                    {
                        TriggerRagdoll(rigManager, "Slip", false);
                        _lastVelocity = velocity;
                        return;
                    }
                }

                // --- Wall Push Detection ---
                if (_wallPushEnabled && CheckWallPushFall(physRig, velocity))
                {
                    TriggerRagdoll(rigManager, "Wall Push", true);
                    _lastVelocity = velocity;
                    return;
                }

                _lastVelocity = velocity;
            }
            catch { }
        }

        private static bool IsHoldingFlyingGun(PhysicsRig physRig)
        {
            try
            {
                if (physRig.leftHand != null && physRig.leftHand.m_CurrentAttachedGO != null)
                {
                    if (physRig.leftHand.m_CurrentAttachedGO.GetComponentInParent<FlyingGun>() != null)
                        return true;
                }
                if (physRig.rightHand != null && physRig.rightHand.m_CurrentAttachedGO != null)
                {
                    if (physRig.rightHand.m_CurrentAttachedGO.GetComponentInParent<FlyingGun>() != null)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static float GetGroundFriction(PhysicsRig physRig)
        {
            try
            {
                RaycastHit hit;
                if (Physics.Raycast(physRig.feet.transform.position, Vector3.down, out hit, 2f))
                {
                    PhysicMaterial mat = hit.collider.sharedMaterial;
                    if (mat != null)
                        return mat.dynamicFriction;
                }
            }
            catch { }
            return 1f;
        }

        private static bool CheckWallPushFall(PhysicsRig physRig, Vector3 velocity)
        {
            try
            {
                if (_grounder == null || _grounder.isGrounded)
                    return false;

                Hand leftHand = physRig.leftHand;
                Hand rightHand = physRig.rightHand;

                bool handColliding = false;
                if (leftHand != null && leftHand.physHand._colliding) handColliding = true;
                if (rightHand != null && rightHand.physHand._colliding) handColliding = true;

                if (!handColliding) return false;

                Vector3 horizontal = velocity;
                horizontal.y = 0f;
                return horizontal.magnitude > _wallPushVelocityThreshold;
            }
            catch { return false; }
        }

        // ═══════════════════════════════════════════════════
        // RAGDOLL TRIGGER
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Trigger ragdoll on the player using the selected mode.
        /// LIMP = full shutdown, ARM_CONTROL = legs limp but arms follow controllers.
        /// </summary>
        private static void TriggerRagdoll(RigManager rig, string reason, bool dropItems)
        {
            try
            {
                var physRig = rig.physicsRig;
                if (physRig == null) return;

                // Save head rotation BEFORE ragdolling so we can restore it on un-ragdoll.
                // NOTE: Disabled — restoring localRotation fights the ConfigurableJoint
                // and causes jitter. Dash aiming is already fixed via Camera.main.transform.forward.
                try
                {
                    var headRb = physRig.torso?.rbHead;
                    if (headRb != null)
                    {
                        // No longer saving head rotation — joint handles it
                    }
                }
                catch { }

                ApplyRagdollMode(physRig);

                if (dropItems)
                {
                    TryDrop(Player.LeftHand);
                    TryDrop(Player.RightHand);
                }

                _nextRagdollTime = Time.time + COOLDOWN;
                Main.MelonLog.Msg($"Ragdoll triggered: {reason} ({_mode})");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Ragdoll trigger error: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply the ragdoll mode.
        /// Tantrum mode now respects the selected mode (LIMP vs ARM_CONTROL).
        /// ARM_CONTROL: legs go limp, arms follow VR controllers, head tracks for look-around.
        /// LIMP: full shutdown, no auto-recovery.
        /// </summary>
        private static void ApplyRagdollMode(PhysicsRig physRig)
        {
            // Both modes and tantrum start with RagdollRig (proven reliable)
            physRig.RagdollRig();

            if (_tantrumMode && _mode == RagdollMode.LIMP)
            {
                // Tantrum + LIMP: just RagdollRig (basic tantrum behavior) 
                // plus full shutdown so no auto-recovery
                physRig.ShutdownRig();
                return;
            }

            if (_mode == RagdollMode.ARM_CONTROL)
            {
                // Disable locomotion and switch to physical legs —
                // arms still follow VR controllers via IK tracking,
                // head still tracks via VR headset naturally
                physRig.DisableBallLoco();
                physRig.PhysicalLegs();
                physRig.legLf.ShutdownLimb();
                physRig.legRt.ShutdownLimb();
            }
            else
            {
                // LIMP: full shutdown on top of ragdoll to prevent auto-recovery
                physRig.ShutdownRig();
            }
        }

        /// <summary>
        /// Force the player into LIMP mode ragdoll, overriding ARM_CONTROL.
        /// Used when grabbed by the neck to disable arm control.
        /// </summary>
        private static void ForceRagdollLimp(PhysicsRig physRig, string reason)
        {
            try
            {
                physRig.RagdollRig();
                physRig.ShutdownRig();
                _nextRagdollTime = Time.time + COOLDOWN;
                Main.MelonLog.Msg($"Forced LIMP ragdoll: {reason}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"ForceRagdollLimp error: {ex.Message}");
            }
        }

        private static void TryDrop(Hand hand)
        {
            try
            {
                if (hand == null || !hand.HasAttachedObject()) return;
                var grip = ((Il2CppObjectBase)hand.AttachedReceiver).TryCast<Grip>();
                if (grip != null)
                    grip.ForceDetach(true);
            }
            catch { }
        }

        /// <summary>
        /// Manually ragdoll the player (for keybind / buttons).
        /// Uses the current mode settings.
        /// </summary>
        public static void ManualRagdoll(PhysicsRig physRig)
        {
            ApplyRagdollMode(physRig);
        }

        /// <summary>
        /// Un-ragdoll the player and reset feet/knee transforms to avoid
        /// teleporting issues when coming out of ragdoll.
        /// </summary>
        public static void UnragdollPlayer(PhysicsRig physRig)
        {
            var pelvisTransform = ((Il2CppSLZ.Marrow.Rig)physRig).m_pelvis.GetComponent<Transform>();

            // Zero out all rig rigidbody velocities BEFORE restoring the rig
            // so joints don't inherit stale ragdoll momentum
            try
            {
                var rbs = ((Component)physRig).gameObject.GetComponentsInChildren<Rigidbody>();
                if (rbs != null)
                {
                    foreach (var rb in rbs)
                    {
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }
            }
            catch { }

            physRig.TurnOnRig();
            physRig.UnRagdollRig();

            // Place feet at the ground surface below pelvis, knee halfway between.
            // Raycast to find actual ground — blind offset can push locoball underground
            // if the player ragdolled near the floor.
            var pos = pelvisTransform.position;
            var rot = pelvisTransform.rotation;

            float groundY = pos.y - 1.0f; // fallback
            RaycastHit hit;
            if (Physics.Raycast(pos, Vector3.down, out hit, 3f))
            {
                groundY = hit.point.y;
            }

            // Small upward offset so the locoball sits ON the ground, not in it
            float feetY = groundY + 0.05f;
            float kneeY = pos.y - (pos.y - feetY) * 0.5f;

            physRig.knee.transform.SetPositionAndRotation(new Vector3(pos.x, kneeY, pos.z), rot);
            physRig.feet.transform.SetPositionAndRotation(new Vector3(pos.x, feetY, pos.z), rot);

            // Zero velocities AGAIN after rig restore in case TurnOnRig/UnRagdollRig injected any
            try
            {
                var rbs = ((Component)physRig).gameObject.GetComponentsInChildren<Rigidbody>();
                if (rbs != null)
                {
                    foreach (var rb in rbs)
                    {
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }
            }
            catch { }

            // NOTE: Do NOT modify the head ConfigurableJoint here.
            // TurnOnRig()/UnRagdollRig() already restores joints to their correct state.
            // Overriding angularMotion to Locked breaks the head's spring-driven tracking
            // of the VR headset, causing persistent avatar jitter.

            // Prevent physics-based ragdoll from re-triggering immediately:
            // reset tracked velocity so deltaV isn't huge on the first frame back,
            // and set a cooldown window
            _lastVelocity = Vector3.zero;
            _nextRagdollTime = Time.time + COOLDOWN;
        }

        // ═══════════════════════════════════════════════════
        // DEATH HOOKS (via BoneLib v3.2.1 events)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Called when the player dies. If PersistOnDeath is on, re-ragdoll immediately
        /// so the death animation doesn't override us. If off, un-ragdoll so death plays normally.
        /// </summary>
        private static void OnPlayerDeathHook(RigManager rig)
        {
            if (!_enabled) return;
            try
            {
                var physRig = rig?.physicsRig;
                if (physRig != null)
                    UnragdollPlayer(physRig);
                _ragdollCooldownUntil = Time.time + COOLDOWN;
            }
            catch { }
        }

        private static void OnPlayerResurrectedHook(RigManager rig)
        {
            // No-op after removing PersistOnDeath
        }
    }

    /// <summary>
    /// Harmony patch to mute footstep sounds while the player is ragdolled.
    /// </summary>
    [HarmonyPatch(typeof(FootstepSFX))]
    public static class FootstepSFXPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("PlayStep")]
        public static bool PlayStep(FootstepSFX __instance)
        {
            if (RagdollController.DisableFootstep(__instance))
                return false;
            return true;
        }
    }
}
