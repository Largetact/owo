using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using HarmonyLib;
using Il2CppSLZ.Marrow;
using LabFusion.Player;
using System;

[assembly: MelonInfo(typeof(GodModeAntiKnockout.GodModeAntiKnockoutMod), "GodMode + AntiKnockout", "1.0.0", "XI")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace GodModeAntiKnockout
{
    public class GodModeAntiKnockoutMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Log.Msg("GodMode + AntiKnockout loaded");

            GodModeController.Initialize();

            Hooking.OnLevelLoaded += (info) => SetupBoneMenu();
        }

        public override void OnUpdate()
        {
            GodModeController.Update();
        }

        private static bool _boneMenuSetup = false;

        private static void SetupBoneMenu()
        {
            if (_boneMenuSetup) return;
            _boneMenuSetup = true;

            var page = Page.Root.CreatePage("GodMode + AntiKO", Color.red);

            page.CreateBool(
                "God Mode",
                Color.green,
                GodModeController.IsGodModeEnabled,
                (value) => GodModeController.IsGodModeEnabled = value
            );

            page.CreateBool(
                "Anti-Knockout",
                Color.yellow,
                AntiKnockoutController.IsEnabled,
                (value) => AntiKnockoutController.IsEnabled = value
            );
        }
    }

    // ═══════════════════════════════════════════════════════════
    // GOD MODE
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(Player_Health), "TAKEDAMAGE")]
    public static class GodModeDamagePatch
    {
        private static bool Prefix()
        {
            return !GodModeController.IsGodModeEnabled;
        }
    }

    public static class GodModeController
    {
        private static bool _godModeEnabled = false;
        private static bool _initialized = false;

        public static bool IsGodModeEnabled
        {
            get => _godModeEnabled;
            set
            {
                _godModeEnabled = value;
                GodModeAntiKnockoutMod.Log.Msg($"God Mode {(value ? "ENABLED" : "DISABLED")}");
                if (value)
                {
                    ApplyFusionOverrides();
                    HealToFull();
                }
                else
                {
                    ClearFusionOverrides();
                }
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            GodModeAntiKnockoutMod.Log.Msg("God Mode controller initialized");
        }

        private static void HealToFull()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager != null && rigManager.health != null)
                {
                    rigManager.health.ResetHits();

                    var healthType = rigManager.health.GetType();
                    var currHealthField = healthType.GetField("curr_Health",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var maxHealthField = healthType.GetField("max_Health",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (currHealthField != null && maxHealthField != null)
                    {
                        var maxHealth = maxHealthField.GetValue(rigManager.health);
                        currHealthField.SetValue(rigManager.health, maxHealth);
                    }
                }
            }
            catch { }
        }

        private static void ApplyFusionOverrides()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type localHealthType = null;
                foreach (var asm in assemblies)
                {
                    try { localHealthType = asm.GetType("LabFusion.Player.LocalHealth"); } catch { }
                    if (localHealthType != null) break;
                }

                if (localHealthType != null)
                {
                    var vitalityProp = localHealthType.GetProperty("VitalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var mortalityProp = localHealthType.GetProperty("MortalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (vitalityProp != null)
                        try { vitalityProp.SetValue(null, (float?)999999f); } catch { }
                    if (mortalityProp != null)
                        try { mortalityProp.SetValue(null, (bool?)false); } catch { }

                    var setFull = localHealthType.GetMethod("SetFullHealth",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    try { setFull?.Invoke(null, null); } catch { }
                }
            }
            catch { }
        }

        private static void ClearFusionOverrides()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type localHealthType = null;
                foreach (var asm in assemblies)
                {
                    try { localHealthType = asm.GetType("LabFusion.Player.LocalHealth"); } catch { }
                    if (localHealthType != null) break;
                }

                if (localHealthType != null)
                {
                    var vitalityProp = localHealthType.GetProperty("VitalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var mortalityProp = localHealthType.GetProperty("MortalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (vitalityProp != null)
                        try { vitalityProp.SetValue(null, (float?)null); } catch { }
                    if (mortalityProp != null)
                        try { mortalityProp.SetValue(null, (bool?)null); } catch { }
                }
            }
            catch { }
        }

        public static void Update()
        {
            if (!_godModeEnabled) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager != null && rigManager.health != null)
                    rigManager.health.ResetHits();
            }
            catch { }

            ApplyFusionOverrides();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ANTI-KNOCKOUT
    // ═══════════════════════════════════════════════════════════

    public static class AntiKnockoutController
    {
        private static bool _isEnabled = false;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                GodModeAntiKnockoutMod.Log.Msg($"Anti-Knockout {(value ? "ENABLED" : "DISABLED")}");
            }
        }
    }

    [HarmonyPatch(typeof(LocalRagdoll), "KnockoutCoroutine")]
    public static class AntiKnockoutPatch
    {
        public static bool Prefix()
        {
            if (AntiKnockoutController.IsEnabled)
                return false;
            return true;
        }
    }
}
