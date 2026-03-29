using System;
using System.Collections.Generic;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Manages customisable keyboard bindings for toggleable functions.
    /// Each binding maps a KeyCode to a named action.
    /// </summary>
    public static class KeybindManager
    {
        public struct Keybind
        {
            public string Name;        // Display name
            public string Id;          // Unique ID for settings
            public KeyCode Key;        // Current bound key
            public KeyCode DefaultKey; // Factory default
            public Action OnPress;     // Callback when pressed (GetKeyDown)
            public Action OnHeld;      // Callback while held (GetKey, every frame)
        }

        private static List<Keybind> _keybinds = new List<Keybind>();
        private static bool _initialized = false;

        // For rebinding UI
        private static int _rebindingIndex = -1;
        public static bool IsRebinding => _rebindingIndex >= 0;
        public static string RebindingName => _rebindingIndex >= 0 ? _keybinds[_rebindingIndex].Name : "";

        public static IReadOnlyList<Keybind> Keybinds => _keybinds;

        public static void Initialize()
        {
            _keybinds.Clear();

            // ── Default keybinds (have default keys) ──
            Register("God Mode Toggle", "GodMode", KeyCode.Alpha0,
                () => { GodModeController.IsGodModeEnabled = !GodModeController.IsGodModeEnabled; SettingsManager.MarkDirty(); });

            Register("Cycle Target (Dash/Flight)", "CycleTargetDF", KeyCode.Minus,
                () => PlayerTargeting.CycleTarget(DashController.LockOnFilter));

            Register("Cycle Target (Obj Launcher)", "CycleTargetOL", KeyCode.Equals,
                () => PlayerTargeting.CycleTarget(ObjectLauncherController.HomingFilter));

            Register("Despawn Launched Objects", "DespawnLaunched", KeyCode.Backslash,
                () => ObjectLauncherController.DespawnLaunchedObjects());

            Register("Toggle Overlay Menu", "OverlayToggle", KeyCode.Alpha1, null); // handled by OverlayMenu itself

            Register("Quick Menu", "QuickMenu", KeyCode.Alpha2, null); // handled by QuickMenuController itself

            Register("Despawn Held Item", "DespawnHeld", KeyCode.BackQuote,
                () => BlockController.DespawnHeldItem());

            // ── Toggle keybinds (default None, user assignable) ──
            Register("Dash Toggle", "DashToggle", KeyCode.None,
                () => { DashController.IsDashEnabled = !DashController.IsDashEnabled; SettingsManager.MarkDirty(); });

            Register("Object Launcher Toggle", "LauncherToggle", KeyCode.None,
                () => { ObjectLauncherController.IsLauncherEnabled = !ObjectLauncherController.IsLauncherEnabled; SettingsManager.MarkDirty(); });

            Register("Launcher Fire", "LauncherFire", KeyCode.None,
                () => { if (ObjectLauncherController.IsLauncherEnabled) ObjectLauncherController.LaunchObject(); },
                () => { if (ObjectLauncherController.IsLauncherEnabled && ObjectLauncherController.IsFullAuto) ObjectLauncherController.KeybindFullAutoFire(); });

            Register("Ragdoll Toggle", "RagdollToggle", KeyCode.None,
                () => { RagdollController.Enabled = !RagdollController.Enabled; SettingsManager.MarkDirty(); });
            Register("Ragdoll/Unragdoll", "RagdollUnragdoll", KeyCode.None,
                () =>
                {
                    try
                    {
                        var rm = BoneLib.Player.RigManager;
                        if (rm == null) return;
                        var physRig = rm.physicsRig;
                        if (physRig == null) return;

                        if (!physRig.torso.shutdown && physRig.ballLocoEnabled)
                        {
                            RagdollController.ManualRagdoll(physRig);
                        }
                        else
                        {
                            RagdollController.UnragdollPlayer(physRig);
                        }
                    }
                    catch { }
                });

            Register("Screen Share", "ScreenShare", KeyCode.None,
                () => { ScreenShareController.Enabled = !ScreenShareController.Enabled; SettingsManager.MarkDirty(); });

            // Actions
            Register("Test Explosion", "TestExplosion", KeyCode.None,
                () => RandomExplodeController.TriggerExplosion());
            Register("Despawn All", "DespawnAll", KeyCode.Backspace,
                () => DespawnAllController.DespawnAll());

            _initialized = true;
            Main.MelonLog.Msg($"KeybindManager initialized with {_keybinds.Count} bindings");
        }

        private static void Register(string name, string id, KeyCode defaultKey, Action onPress, Action onHeld = null)
        {
            _keybinds.Add(new Keybind
            {
                Name = name,
                Id = id,
                Key = defaultKey,
                DefaultKey = defaultKey,
                OnPress = onPress,
                OnHeld = onHeld
            });
        }

        public static void Update()
        {
            if (!_initialized) return;

            // Handle rebinding mode
            if (_rebindingIndex >= 0)
            {
                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    if (kc == KeyCode.None || kc == KeyCode.Mouse0 || kc == KeyCode.Mouse1) continue;
                    if (Input.GetKeyDown(kc))
                    {
                        if (kc == KeyCode.Escape)
                        {
                            // Cancel rebind
                            _rebindingIndex = -1;
                            return;
                        }
                        var kb = _keybinds[_rebindingIndex];
                        kb.Key = kc;
                        _keybinds[_rebindingIndex] = kb;
                        Main.MelonLog.Msg($"[Keybind] '{kb.Name}' bound to {kc}");
                        _rebindingIndex = -1;
                        SettingsManager.ForceSave();
                        return;
                    }
                }
                return; // Don't process normal keybinds while rebinding
            }

            // Normal keybind processing
            for (int i = 0; i < _keybinds.Count; i++)
            {
                var kb = _keybinds[i];
                if (kb.Key == KeyCode.None) continue;

                if (kb.OnPress != null && Input.GetKeyDown(kb.Key))
                {
                    try { kb.OnPress(); }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"[Keybind] '{kb.Name}' error: {ex.Message}");
                    }
                }
                else if (kb.OnHeld != null && Input.GetKey(kb.Key))
                {
                    try { kb.OnHeld(); }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"[Keybind] '{kb.Name}' held error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Start rebinding mode for the keybind at the given index.
        /// Next key press will be captured as the new binding.
        /// </summary>
        public static void StartRebind(int index)
        {
            if (index >= 0 && index < _keybinds.Count)
            {
                _rebindingIndex = index;
                Main.MelonLog.Msg($"[Keybind] Press a key to rebind '{_keybinds[index].Name}' (ESC to cancel)");
            }
        }

        /// <summary>
        /// Reset a keybind to its default.
        /// </summary>
        public static void ResetToDefault(int index)
        {
            if (index >= 0 && index < _keybinds.Count)
            {
                var kb = _keybinds[index];
                kb.Key = kb.DefaultKey;
                _keybinds[index] = kb;
                SettingsManager.ForceSave();
            }
        }

        /// <summary>
        /// Reset all keybinds to defaults.
        /// </summary>
        public static void ResetAllDefaults()
        {
            for (int i = 0; i < _keybinds.Count; i++)
            {
                var kb = _keybinds[i];
                kb.Key = kb.DefaultKey;
                _keybinds[i] = kb;
            }
            SettingsManager.ForceSave();
        }

        /// <summary>
        /// Get the KeyCode for a keybind by its ID.
        /// </summary>
        public static KeyCode GetKey(string id)
        {
            for (int i = 0; i < _keybinds.Count; i++)
            {
                if (_keybinds[i].Id == id) return _keybinds[i].Key;
            }
            return KeyCode.None;
        }

        /// <summary>
        /// Set the KeyCode for a keybind by its ID.
        /// </summary>
        public static void SetKey(string id, KeyCode key)
        {
            for (int i = 0; i < _keybinds.Count; i++)
            {
                if (_keybinds[i].Id == id)
                {
                    var kb = _keybinds[i];
                    kb.Key = key;
                    _keybinds[i] = kb;
                    return;
                }
            }
        }
    }
}
