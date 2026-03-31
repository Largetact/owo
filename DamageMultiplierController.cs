using System;
using System.Reflection;
using BoneLib;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Damage Multiplier — scales gun and melee damage locally.
    /// Modifies gun projectile damage via MagazineState and melee StabSlash damage.
    /// Safe and Fusion-friendly — local damage modification only.
    /// </summary>
    public static class DamageMultiplierController
    {
        private static bool _enabled = false;
        private static float _gunMultiplier = 1f;
        private static float _meleeMultiplier = 1f;
        private static float _lastApply = 0f;
        private const float APPLY_INTERVAL = 0.5f;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Damage Multiplier: {(value ? "ON" : "OFF")}");
                if (value) ApplyMultipliers();
            }
        }

        public static float GunMultiplier
        {
            get => _gunMultiplier;
            set => _gunMultiplier = Mathf.Clamp(value, 0.1f, 100f);
        }

        public static float MeleeMultiplier
        {
            get => _meleeMultiplier;
            set => _meleeMultiplier = Mathf.Clamp(value, 0.1f, 100f);
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Damage Multiplier controller initialized");
        }

        public static void Update()
        {
            if (!_enabled) return;
            if (Time.time - _lastApply < APPLY_INTERVAL) return;
            _lastApply = Time.time;
            ApplyMultipliers();
        }

        private static void ApplyMultipliers()
        {
            try
            {
                ApplyGunMultiplier();
                ApplyMeleeMultiplier();
            }
            catch { }
        }

        /// <summary>
        /// Public entry point for "Apply Now" button in menus.
        /// </summary>
        public static void ApplyMultipliersNow()
        {
            ApplyMultipliers();
        }

        private static void ApplyGunMultiplier()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                // Get guns from both hands
                var physRig = rigManager.physicsRig;
                if (physRig == null) return;

                ApplyGunToHand(physRig.leftHand);
                ApplyGunToHand(physRig.rightHand);
            }
            catch { }
        }

        private static void ApplyGunToHand(Hand hand)
        {
            if (hand == null) return;

            try
            {
                var attached = hand.m_CurrentAttachedGO;
                if (attached == null) return;

                var gun = attached.GetComponentInParent<Gun>();
                if (gun == null) return;

                // Access MagazineState -> CartridgeData -> ProjectileData -> damageMultiplier
                var magState = gun.MagazineState;
                if (magState == null) return;

                // Use reflection to access cartridge data and set damage multiplier
                var cartridgeData = magState.GetType().GetProperty("CartridgeData",
                    BindingFlags.Public | BindingFlags.Instance);
                if (cartridgeData == null) return;

                var cartridge = cartridgeData.GetValue(magState);
                if (cartridge == null) return;

                var projectileProp = cartridge.GetType().GetProperty("projectile",
                    BindingFlags.Public | BindingFlags.Instance);
                if (projectileProp == null)
                    projectileProp = cartridge.GetType().GetField("projectile",
                        BindingFlags.Public | BindingFlags.Instance)?.GetType().GetProperty("damageMultiplier");

                // Try direct field access
                var projectileField = cartridge.GetType().GetField("projectile",
                    BindingFlags.Public | BindingFlags.Instance);
                if (projectileField != null)
                {
                    var projectile = projectileField.GetValue(cartridge);
                    if (projectile != null)
                    {
                        var dmgMultField = projectile.GetType().GetField("damageMultiplier",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (dmgMultField != null)
                        {
                            dmgMultField.SetValue(projectile, _gunMultiplier);
                        }
                    }
                }
            }
            catch { }
        }

        private static void ApplyMeleeMultiplier()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var physRig = rigManager.physicsRig;
                if (physRig == null) return;

                ApplyMeleeToHand(physRig.leftHand);
                ApplyMeleeToHand(physRig.rightHand);
            }
            catch { }
        }

        private static void ApplyMeleeToHand(Hand hand)
        {
            if (hand == null) return;

            try
            {
                var attached = hand.m_CurrentAttachedGO;
                if (attached == null) return;

                var root = attached.transform.root;
                if (root == null) return;

                // Apply to StabSlash components
                var stabSlash = root.GetComponentInChildren<StabSlash>();
                if (stabSlash == null) return;

                // Update slash blades
                try
                {
                    var bladesField = stabSlash.GetType().GetField("slashBlades",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (bladesField != null)
                    {
                        var blades = bladesField.GetValue(stabSlash) as System.Collections.IEnumerable;
                        if (blades != null)
                        {
                            foreach (var blade in blades)
                            {
                                if (blade == null) continue;
                                var dmgField = blade.GetType().GetField("damage",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (dmgField != null)
                                    dmgField.SetValue(blade, _meleeMultiplier * 10f);
                            }
                        }
                    }
                }
                catch { }

                // Update stab points
                try
                {
                    var pointsField = stabSlash.GetType().GetField("stabPoints",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (pointsField != null)
                    {
                        var points = pointsField.GetValue(stabSlash) as System.Collections.IEnumerable;
                        if (points != null)
                        {
                            foreach (var point in points)
                            {
                                if (point == null) continue;
                                var dmgField = point.GetType().GetField("damage",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (dmgField != null)
                                    dmgField.SetValue(point, _meleeMultiplier * 10f);
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }
    }
}
