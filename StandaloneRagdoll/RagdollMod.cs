using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Audio;
using Il2CppSLZ.Bonelab;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Page = BoneLib.BoneMenu.Page;

[assembly: MelonInfo(typeof(StandaloneRagdoll.RagdollMod), "Standalone Grabdoll", "2.1.0", "DOOBER")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace StandaloneRagdoll
{
    public enum RagdollMode
    {
        LIMP,
        ARM_CONTROL
    }

    public enum RagdollHand
    {
        RIGHT_HAND,
        LEFT_HAND
    }

    public enum RagdollBinding
    {
        NONE,
        THUMBSTICK_PRESS,
        DOUBLE_TAP_B
    }

    public class RagdollMod : MelonMod
    {
        private static MelonPreferences_Category _prefCategory;
        private static MelonPreferences_Entry<bool> _prefEnabled;
        private static MelonPreferences_Entry<bool> _prefGrabEnabled;
        private static MelonPreferences_Entry<int> _prefMode;
        private static MelonPreferences_Entry<bool> _prefTantrumMode;
        private static MelonPreferences_Entry<int> _prefKeybindHand;
        private static MelonPreferences_Entry<int> _prefBinding;

        public override void OnInitializeMelon()
        {
            _prefCategory = MelonPreferences.CreateCategory("Grabdoll");
            _prefEnabled = _prefCategory.CreateEntry("Enabled", false);
            _prefGrabEnabled = _prefCategory.CreateEntry("GrabEnabled", true);
            _prefMode = _prefCategory.CreateEntry("Mode", (int)RagdollMode.LIMP);
            _prefTantrumMode = _prefCategory.CreateEntry("TantrumMode", false);
            _prefKeybindHand = _prefCategory.CreateEntry("KeybindHand", (int)RagdollHand.RIGHT_HAND);
            _prefBinding = _prefCategory.CreateEntry("Binding", (int)RagdollBinding.THUMBSTICK_PRESS);

            LoadPreferences();
            RagdollController.Initialize();
            LoggerInstance.Msg("Standalone Grabdoll v2.1.0 loaded");
        }

        public override void OnLateInitializeMelon()
        {
            BuildMenu();
        }

        public override void OnUpdate()
        {
            RagdollController.Update();
        }

        public override void OnApplicationQuit()
        {
            SavePreferences();
        }

        private void LoadPreferences()
        {
            RagdollController.Enabled = _prefEnabled.Value;
            RagdollController.GrabEnabled = _prefGrabEnabled.Value;
            RagdollController.Mode = (RagdollMode)_prefMode.Value;
            RagdollController.TantrumMode = _prefTantrumMode.Value;
            RagdollController.KeybindHand = (RagdollHand)_prefKeybindHand.Value;
            RagdollController.Binding = (RagdollBinding)_prefBinding.Value;
        }

        public static void SavePreferences()
        {
            if (_prefCategory == null) return;
            _prefEnabled.Value = RagdollController.Enabled;
            _prefGrabEnabled.Value = RagdollController.GrabEnabled;
            _prefMode.Value = (int)RagdollController.Mode;
            _prefTantrumMode.Value = RagdollController.TantrumMode;
            _prefKeybindHand.Value = (int)RagdollController.KeybindHand;
            _prefBinding.Value = (int)RagdollController.Binding;
            _prefCategory.SaveToFile(false);
        }

        private void BuildMenu()
        {
            var page = Page.Root.CreatePage("Grabdoll", Color.red);
            page.CreateBool("Enabled", Color.white, RagdollController.Enabled,
                (v) => { RagdollController.Enabled = v; SavePreferences(); });
            page.CreateBool("Grab Ragdoll", Color.yellow, RagdollController.GrabEnabled,
                (v) => { RagdollController.GrabEnabled = v; SavePreferences(); });
            page.CreateEnum("Mode", Color.cyan, RagdollController.Mode,
                (v) => { RagdollController.Mode = (RagdollMode)v; SavePreferences(); });
            page.CreateBool("Tantrum Mode", Color.red, RagdollController.TantrumMode,
                (v) => { RagdollController.TantrumMode = v; SavePreferences(); });
            page.CreateEnum("Keybind", Color.green, RagdollController.Binding,
                (v) => { RagdollController.Binding = (RagdollBinding)v; SavePreferences(); });
            page.CreateEnum("Keybind Hand", Color.green, RagdollController.KeybindHand,
                (v) => { RagdollController.KeybindHand = (RagdollHand)v; SavePreferences(); });
        }
    }

    public static class RagdollController
    {
        private static bool _enabled = false;
        private static RagdollMode _mode = RagdollMode.LIMP;
        private static bool _tantrumMode = false;
        private static bool _grabEnabled = true;
        private static RagdollHand _keybindHand = RagdollHand.RIGHT_HAND;
        private static RagdollBinding _binding = RagdollBinding.NONE;

        // Double-tap B state
        private const float DOUBLE_TAP_TIMER = 0.32f;
        private static float _lastTimeInput = 0f;
        private static bool _ragdollNextButton = false;

        // Footstep SFX muting
        private static List<FootstepSFX> _footsteps = new List<FootstepSFX>();

        // Internal state
        private static bool _sceneLoaded = false;
        private static float _nextRagdollTime = 0f;
        private static float _ragdollCooldownUntil = 0f;
        private const float COOLDOWN = 2f;

        // Cached grip scanning
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
                MelonLogger.Msg($"Ragdoll Controller {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static bool GrabEnabled { get => _grabEnabled; set => _grabEnabled = value; }

        public static RagdollMode Mode
        {
            get => _mode;
            set { _mode = value; MelonLogger.Msg($"Ragdoll Mode: {value}"); }
        }

        public static bool TantrumMode
        {
            get => _tantrumMode;
            set { _tantrumMode = value; MelonLogger.Msg($"Tantrum Mode: {(value ? "ON" : "OFF")}"); }
        }

        public static RagdollHand KeybindHand { get => _keybindHand; set => _keybindHand = value; }
        public static RagdollBinding Binding { get => _binding; set => _binding = value; }

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
            MelonLogger.Msg("RagdollController initialized");
        }

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
                        var prop = type.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Static);
                        if (prop != null && prop.CanWrite) prop.SetValue(null, false);
                        var field = type.GetField("ModEnabled", BindingFlags.Public | BindingFlags.Static);
                        if (field != null) field.SetValue(null, false);
                        try { mod.HarmonyInstance?.UnpatchSelf(); } catch { }
                        MelonLogger.Msg($"Overrode external ragdoll mod: {name}");
                    }
                }
            }
            catch { }
        }

        private static void OnSceneLoaded()
        {
            _sceneLoaded = true;
            _cachedRigGrips = null;
            _lastGripCacheTime = 0f;
            _nextRagdollTime = 0f;
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

            DisableExternalRagdollMods();
        }

        // ───── Main Update ─────

        public static void Update()
        {
            if (!_sceneLoaded) return;

            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                // VR Controller keybind toggle (seat + cursor check)
                if (rigManager.activeSeat == null &&
                    !UIRig.Instance.popUpMenu.m_IsCursorShown &&
                    CheckKeybindInput())
                {
                    if (!physRig.torso.shutdown && physRig.ballLocoEnabled)
                    {
                        ApplyRagdollMode(physRig);
                        _nextRagdollTime = Time.time + COOLDOWN;
                        MelonLogger.Msg($"Ragdoll toggled ON ({_mode})");
                    }
                    else
                    {
                        UnragdollPlayer(physRig);
                        MelonLogger.Msg("Ragdoll toggled OFF");
                    }
                }

                // Grab detection (only when enabled and not on cooldown)
                if (!_enabled) return;
                if (Time.time < _ragdollCooldownUntil) return;

                if (physRig.torso.shutdown || !physRig.ballLocoEnabled)
                    return;

                if (Time.time < _nextRagdollTime)
                    return;

                if (_grabEnabled)
                    CheckGrabRagdoll(rigManager, physRig);
            }
            catch { }
        }

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

        // ───── Grab Detection ─────

        private static void CheckGrabRagdoll(RigManager rigManager, PhysicsRig physRig)
        {
            try
            {
                var torso = physRig.torso;
                if (torso == null) return;
                float myMass = GetRigMass(rigManager);

                var headGrabber = GetExternalGrabber(torso.gHead, rigManager);
                var neckGrabber = GetExternalGrabber(torso.gNeck, rigManager);

                if (headGrabber != null && GetRigMass(headGrabber) > myMass * 0.9f)
                { TriggerRagdoll(rigManager, "Head Grab", false); return; }

                if (neckGrabber != null && GetRigMass(neckGrabber) > myMass * 0.9f)
                { TriggerRagdoll(rigManager, "Neck Grab", false); return; }

                RefreshGripCache(rigManager);
                RigManager leftArmGrabber = null;
                RigManager rightArmGrabber = null;

                if (_cachedRigGrips != null)
                {
                    Transform leftHandTf = physRig.leftHand != null ? ((Component)physRig.leftHand).transform : null;
                    Transform rightHandTf = physRig.rightHand != null ? ((Component)physRig.rightHand).transform : null;
                    Transform myRoot = ((Component)rigManager).transform;

                    GameObject leftHeldRoot = null;
                    GameObject rightHeldRoot = null;
                    try { if (physRig.leftHand?.m_CurrentAttachedGO != null) leftHeldRoot = physRig.leftHand.m_CurrentAttachedGO.transform.root.gameObject; } catch { }
                    try { if (physRig.rightHand?.m_CurrentAttachedGO != null) rightHeldRoot = physRig.rightHand.m_CurrentAttachedGO.transform.root.gameObject; } catch { }

                    foreach (var grip in _cachedRigGrips)
                    {
                        if (grip == null || !grip.HasAttachedHands()) continue;
                        if (IsTorsoGrip(grip, torso)) continue;

                        Transform gripRoot = ((Component)grip).transform.root;
                        if (leftHeldRoot != null && gripRoot.gameObject == leftHeldRoot) continue;
                        if (rightHeldRoot != null && gripRoot.gameObject == rightHeldRoot) continue;
                        if (gripRoot != myRoot) continue;

                        var externalGrabber = GetExternalGrabberFromGrip(grip, rigManager);
                        if (externalGrabber == null) continue;

                        Transform gripTf = ((Component)grip).transform;
                        if (leftHandTf != null && rightHandTf != null)
                        {
                            float distLeft = Vector3.Distance(gripTf.position, leftHandTf.position);
                            float distRight = Vector3.Distance(gripTf.position, rightHandTf.position);
                            if (gripTf.IsChildOf(leftHandTf) || distLeft < distRight)
                            { if (leftArmGrabber == null) leftArmGrabber = externalGrabber; }
                            else
                            { if (rightArmGrabber == null) rightArmGrabber = externalGrabber; }
                        }
                        else
                        {
                            Vector3 localPos = myRoot.InverseTransformPoint(gripTf.position);
                            if (localPos.x < 0) { if (leftArmGrabber == null) leftArmGrabber = externalGrabber; }
                            else { if (rightArmGrabber == null) rightArmGrabber = externalGrabber; }
                        }
                    }
                }

                if (leftArmGrabber != null && rightArmGrabber != null)
                {
                    float leftMass = GetRigMass(leftArmGrabber);
                    float rightMass = GetRigMass(rightArmGrabber);
                    if (leftMass >= myMass * 0.9f || rightMass >= myMass * 0.9f)
                    { TriggerRagdoll(rigManager, "Both Arms Grabbed", false); return; }
                }

                int bodyGrabCount = 0;
                RigManager bodyGrabber = null;
                Grip[] bodyGrips = { torso.gChest, torso.gSpine, torso.gPelvis };
                foreach (var grip in bodyGrips)
                {
                    if (grip == null) continue;
                    var grabber = GetExternalGrabber(grip, rigManager);
                    if (grabber != null) { bodyGrabber = grabber; bodyGrabCount += CountExternalHands(grip, rigManager); }
                }
                if (torso.gHead != null) bodyGrabCount += CountExternalHands(torso.gHead, rigManager);
                if (torso.gNeck != null) bodyGrabCount += CountExternalHands(torso.gNeck, rigManager);

                if (bodyGrabber != null && bodyGrabCount >= 1)
                {
                    float massRatio = GetRigMass(bodyGrabber) / myMass;
                    if (massRatio >= 2.0f && bodyGrabCount >= 1)
                    { TriggerRagdoll(rigManager, "Body Grabbed (Much Bigger)", false); return; }
                    else if (massRatio >= 1.3f && bodyGrabCount >= 2)
                    { TriggerRagdoll(rigManager, "Body Grabbed (Both Hands)", false); return; }
                }
            }
            catch { }
        }

        // ───── Helpers ─────

        private static RigManager GetExternalGrabber(Grip grip, RigManager myRig)
        {
            if (grip == null || !grip.HasAttachedHands()) return null;
            return GetExternalGrabberFromGrip(grip, myRig);
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
                    if (hand != null && hand.manager != null && hand.manager != myRig)
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
                    if (hand != null && hand.manager != null && hand.manager != myRig) count++;
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

        private static float GetRigMass(RigManager rig)
        {
            try
            {
                if (rig == null) return 1f;
                var physRig = rig.physicsRig;
                if (physRig == null) return 1f;
                float mass = 0f;
                var torso = physRig.torso;
                if (torso != null)
                {
                    if (torso.rbHead != null) mass += torso.rbHead.mass;
                    if (torso.rbChest != null) mass += torso.rbChest.mass;
                    if (torso.rbSpine != null) mass += torso.rbSpine.mass;
                    if (torso.rbPelvis != null) mass += torso.rbPelvis.mass;
                }
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
            if (Time.time - _lastGripCacheTime < GRIP_CACHE_INTERVAL && _cachedRigGrips != null) return;
            try
            {
                _cachedRigGrips = ((Component)rigManager).GetComponentsInChildren<Grip>();
                _lastGripCacheTime = Time.time;
            }
            catch { _cachedRigGrips = null; }
        }

        // ───── Ragdoll Trigger ─────

        private static void TriggerRagdoll(RigManager rig, string reason, bool dropItems)
        {
            try
            {
                var physRig = rig.physicsRig;
                if (physRig == null) return;
                ApplyRagdollMode(physRig);
                if (dropItems)
                {
                    TryDrop(Player.LeftHand);
                    TryDrop(Player.RightHand);
                }
                _nextRagdollTime = Time.time + COOLDOWN;
                MelonLogger.Msg($"Ragdoll triggered: {reason} ({_mode})");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Ragdoll trigger error: {ex.Message}");
            }
        }

        private static void ApplyRagdollMode(PhysicsRig physRig)
        {
            if (_tantrumMode)
            {
                physRig.RagdollRig();
                return;
            }

            // Always RagdollRig first (proven reliable)
            physRig.RagdollRig();

            if (_mode == RagdollMode.ARM_CONTROL)
            {
                physRig.DisableBallLoco();
                physRig.PhysicalLegs();
                physRig.legLf.ShutdownLimb();
                physRig.legRt.ShutdownLimb();
            }
            else
            {
                physRig.ShutdownRig();
            }
        }

        private static void TryDrop(Hand hand)
        {
            try
            {
                if (hand == null || !hand.HasAttachedObject()) return;
                var grip = ((Il2CppObjectBase)hand.AttachedReceiver).TryCast<Grip>();
                grip?.ForceDetach(true);
            }
            catch { }
        }

        public static void UnragdollPlayer(PhysicsRig physRig)
        {
            var pelvisTransform = ((Il2CppSLZ.Marrow.Rig)physRig).m_pelvis.GetComponent<Transform>();
            physRig.TurnOnRig();
            physRig.UnRagdollRig();
            var pos = pelvisTransform.position;
            var rot = pelvisTransform.rotation;
            physRig.knee.transform.SetPositionAndRotation(pos, rot);
            physRig.feet.transform.SetPositionAndRotation(pos, rot);
        }

        // ───── Death Hooks ─────

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

        private static void OnPlayerResurrectedHook(RigManager rig) { }
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
