using HarmonyLib;
using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    [HarmonyPatch]
    public static class ChaosGunController
    {
        // Sub-feature toggles (use properties so we can restore on disable)
        public static bool PurpleGuns = false;
        public static bool NoRecoil = false;
        public static bool NoReload = false;

        private static bool _insaneDamage = false;
        public static bool InsaneDamage
        {
            get => _insaneDamage;
            set
            {
                if (_insaneDamage && !value) RestoreDamage();
                _insaneDamage = value;
            }
        }

        private static bool _insaneFirerate = false;
        public static bool InsaneFirerate
        {
            get => _insaneFirerate;
            set
            {
                if (_insaneFirerate && !value) RestoreFirerate();
                _insaneFirerate = value;
            }
        }

        private static bool _noWeight = false;
        public static bool NoWeight
        {
            get => _noWeight;
            set
            {
                if (_noWeight && !value) RestoreWeight();
                _noWeight = value;
            }
        }

        private static bool _gunsBounce = false;
        public static bool GunsBounce
        {
            get => _gunsBounce;
            set
            {
                if (_gunsBounce && !value) RestoreBounce();
                _gunsBounce = value;
            }
        }

        // Original value caches (keyed by instance ID)
        private static Dictionary<int, (CartridgeData data, float orig)> _origDamageMultiplier = new Dictionary<int, (CartridgeData, float)>();
        private static Dictionary<int, (Gun.FireMode mode, float rps)> _origFirerate = new Dictionary<int, (Gun.FireMode, float)>();
        private static Dictionary<int, (float mass, float drag, float angDrag)> _origRbData = new Dictionary<int, (float, float, float)>();
        private static Dictionary<int, (float bounciness, PhysicMaterialCombine bounceCombine, float staticFriction, PhysicMaterialCombine frictionCombine)> _origColliderData =
            new Dictionary<int, (float, PhysicMaterialCombine, float, PhysicMaterialCombine)>();

        // Track which guns have already had glow applied (avoid re-applying every frame)
        private static HashSet<int> _glowedGuns = new HashSet<int>();

        // Cache renderers per gun instance to avoid per-frame GetComponentsInChildren GC alloc
        private static Dictionary<int, Il2CppArrayBase<Renderer>> _cachedRenderers = new Dictionary<int, Il2CppArrayBase<Renderer>>();

        // Rainbow hue state for glow guns (cycles 0→1 over time)
        private static float _glowHue = 0f;
        private const float GLOW_CYCLE_SPEED = 0.25f; // full cycle every 4 seconds

        /// <summary>
        /// Per-frame fallback that applies effects to held guns.
        /// Harmony patches on IL2CPP types can fail silently, so this ensures effects work.
        /// </summary>
        public static void Update()
        {
            bool anyFeature = PurpleGuns || InsaneDamage || NoRecoil || InsaneFirerate || NoWeight || GunsBounce || NoReload;
            if (!anyFeature) return;

            // Advance rainbow hue each frame
            if (PurpleGuns)
                _glowHue = (_glowHue + GLOW_CYCLE_SPEED * Time.deltaTime) % 1f;

            try
            {
                ProcessHeldGun(Player.LeftHand);
                ProcessHeldGun(Player.RightHand);
            }
            catch { }
        }

        private static void ProcessHeldGun(Hand hand)
        {
            if (hand == null) return;
            try
            {
                Gun gun = Player.GetComponentInHand<Gun>(hand);
                if (gun == null) return;

                if (InsaneDamage) ApplyDamage(gun);
                if (NoReload) ApplyNoReload(gun);
                if (InsaneFirerate) ApplyFirerate(gun);
                if (NoWeight) ApplyNoWeight(gun);
                if (GunsBounce) ApplyBounce(gun);

                if (PurpleGuns)
                {
                    int id = ((Object)gun).GetInstanceID();
                    if (!_cachedRenderers.TryGetValue(id, out var renderers))
                    {
                        renderers = ((Component)gun).GetComponentsInChildren<Renderer>();
                        _cachedRenderers[id] = renderers;
                    }
                    ApplyGlowRainbow(renderers);
                }
            }
            catch { }
        }

        /// <summary>
        /// Clear tracked state on level unload.
        /// </summary>
        public static void OnLevelUnloaded()
        {
            _glowedGuns.Clear();
            _cachedRenderers.Clear();
            _origDamageMultiplier.Clear();
            _origFirerate.Clear();
            _origRbData.Clear();
            _origColliderData.Clear();
        }

        // ── Harmony Patches (supplementary — may not fire on all IL2CPP builds) ──

        [HarmonyPatch(typeof(Gun), "Fire")]
        [HarmonyPostfix]
        public static void OnFire(Gun __instance)
        {
            ApplyDamage(__instance);
            ApplyNoReload(__instance);
        }

        [HarmonyPatch(typeof(Gun), "OnTriggerGripAttached")]
        [HarmonyPostfix]
        public static void OnGrip(Gun __instance)
        {
            ApplyBounce(__instance);
            ApplyNoWeight(__instance);
            ApplyFirerate(__instance);
            ApplyPurple(__instance);
        }

        [HarmonyPatch(typeof(Rigidbody), "AddForceAtPosition", new System.Type[]
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(ForceMode)
        })]
        [HarmonyPrefix]
        public static bool AddForceAtPositionPrefix(Rigidbody __instance, ref Vector3 force)
        {
            if (!NoRecoil)
                return true;
            if ((Object)(object)((Component)__instance).GetComponentInParent<Gun>() != (Object)null)
                force = Vector3.zero;
            return true;
        }

        [HarmonyPatch(typeof(Magazine), "OnGrab")]
        [HarmonyPostfix]
        public static void OnMagGrab(Magazine __instance)
        {
            ApplyBounceMag(__instance);
            ApplyPurpleMag(__instance);
            ApplyNoWeightMag(__instance);
        }

        // ── Feature Implementations ──

        private static void ApplyDamage(Gun gun)
        {
            if (!InsaneDamage) return;
            MagazineState magState = gun._magState;
            if (magState == null) return;
            CartridgeData cartridgeData = magState.cartridgeData;
            if ((Object)(object)cartridgeData == (Object)null) return;
            if (cartridgeData.projectile == null) return;
            int id = ((Object)cartridgeData).GetInstanceID();
            if (!_origDamageMultiplier.ContainsKey(id))
                _origDamageMultiplier[id] = (cartridgeData, cartridgeData.projectile.damageMultiplier);
            cartridgeData.projectile.damageMultiplier = float.MaxValue;
        }

        private static void RestoreDamage()
        {
            foreach (var kv in _origDamageMultiplier)
            {
                try
                {
                    var cd = kv.Value.data;
                    if (cd != null && cd.projectile != null)
                        cd.projectile.damageMultiplier = kv.Value.orig;
                }
                catch { }
            }
            _origDamageMultiplier.Clear();
        }

        private static void RestoreDamageOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            MagazineState magState = gun._magState;
            if (magState?.cartridgeData?.projectile == null) return;
            int id = ((Object)magState.cartridgeData).GetInstanceID();
            if (_origDamageMultiplier.TryGetValue(id, out var entry))
                magState.cartridgeData.projectile.damageMultiplier = entry.orig;
        }

        private static void ApplyNoReload(Gun gun)
        {
            if (!NoReload) return;
            MagazineState magState = gun._magState;
            if (magState != null)
                magState.Refill();
        }

        private static void ApplyFirerate(Gun gun)
        {
            if (!InsaneFirerate) return;
            int id = ((Object)gun).GetInstanceID();
            if (!_origFirerate.ContainsKey(id))
                _origFirerate[id] = (gun.fireMode, gun.roundsPerSecond);
            gun.fireMode = (Gun.FireMode)2;
            gun.roundsPerSecond = float.MaxValue;
        }

        private static void RestoreFirerate()
        {
            try
            {
                RestoreFirerateOnHeld(Player.LeftHand);
                RestoreFirerateOnHeld(Player.RightHand);
            }
            catch { }
            _origFirerate.Clear();
        }

        private static void RestoreFirerateOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            int id = ((Object)gun).GetInstanceID();
            if (_origFirerate.TryGetValue(id, out var orig))
            {
                gun.fireMode = orig.mode;
                gun.roundsPerSecond = orig.rps;
            }
        }

        private static void ApplyNoWeight(Gun gun)
        {
            if (!NoWeight) return;
            foreach (Rigidbody rb in ((Component)gun).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (!_origRbData.ContainsKey(id))
                    _origRbData[id] = (rb.mass, rb.drag, rb.angularDrag);
                rb.mass = 0.1f;
                rb.drag = 0f;
                rb.angularDrag = 0f;
            }
        }

        private static void RestoreWeight()
        {
            try
            {
                RestoreWeightOnHeld(Player.LeftHand);
                RestoreWeightOnHeld(Player.RightHand);
            }
            catch { }
            _origRbData.Clear();
        }

        private static void RestoreWeightOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Rigidbody rb in ((Component)gun).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (_origRbData.TryGetValue(id, out var orig))
                {
                    rb.mass = orig.mass;
                    rb.drag = orig.drag;
                    rb.angularDrag = orig.angDrag;
                }
            }
        }

        private static void ApplyBounce(Gun gun)
        {
            if (!GunsBounce) return;
            foreach (Collider col in ((Component)gun).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (!_origColliderData.ContainsKey(id))
                    _origColliderData[id] = (col.material.bounciness, col.material.bounceCombine, col.material.staticFriction, col.material.frictionCombine);
                col.material.bounciness = 1f;
                col.material.bounceCombine = (PhysicMaterialCombine)3;
                col.material.staticFriction = 0f;
                col.material.frictionCombine = (PhysicMaterialCombine)2;
            }
        }

        private static void RestoreBounce()
        {
            try
            {
                RestoreBounceOnHeld(Player.LeftHand);
                RestoreBounceOnHeld(Player.RightHand);
            }
            catch { }
            _origColliderData.Clear();
        }

        private static void RestoreBounceOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Collider col in ((Component)gun).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (_origColliderData.TryGetValue(id, out var orig))
                {
                    col.material.bounciness = orig.bounciness;
                    col.material.bounceCombine = orig.bounceCombine;
                    col.material.staticFriction = orig.staticFriction;
                    col.material.frictionCombine = orig.frictionCombine;
                }
            }
        }

        private static void ApplyPurple(Gun gun)
        {
            if (!PurpleGuns) return;
            ApplyGlowRainbow(((Component)gun).GetComponentsInChildren<Renderer>());
        }

        private static void ApplyGlowRainbow(Il2CppArrayBase<Renderer> renderers)
        {
            Color baseColor = Color.HSVToRGB(_glowHue, 1f, 1f);
            // HDR emission — multiplier > 1 produces bloom
            Color emission = baseColor * 4f;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)renderer.materials)
                {
                    mat.color = baseColor;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", baseColor);

                    mat.SetFloat("_Metallic", 1f);
                    mat.SetFloat("_Smoothness", 1f);

                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
        }

        // ── Magazine variants ──

        private static void ApplyBounceMag(Magazine mag)
        {
            if (!GunsBounce) return;
            foreach (Collider col in ((Component)mag).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (!_origColliderData.ContainsKey(id))
                    _origColliderData[id] = (col.material.bounciness, col.material.bounceCombine, col.material.staticFriction, col.material.frictionCombine);
                col.material.bounciness = 1f;
                col.material.bounceCombine = (PhysicMaterialCombine)3;
                col.material.staticFriction = 0f;
                col.material.frictionCombine = (PhysicMaterialCombine)2;
            }
        }

        private static void ApplyPurpleMag(Magazine mag)
        {
            if (!PurpleGuns) return;
            ApplyGlowRainbow(((Component)mag).GetComponentsInChildren<Renderer>());
        }

        private static void ApplyNoWeightMag(Magazine mag)
        {
            if (!NoWeight) return;
            foreach (Rigidbody rb in ((Component)mag).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (!_origRbData.ContainsKey(id))
                    _origRbData[id] = (rb.mass, rb.drag, rb.angularDrag);
                rb.mass = 0.1f;
                rb.drag = 0f;
                rb.angularDrag = 0f;
            }
        }
    }
}
