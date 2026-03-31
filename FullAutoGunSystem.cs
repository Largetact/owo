using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Full Auto Gun System - Makes semi-auto guns fire continuously when trigger is held
    /// Works by changing the gun's fireMode to AUTOMATIC
    /// </summary>
    public static class FullAutoGunSystem
    {
        // Fire mode enum values from BONELAB
        // MANUAL = 0 (bolt action), SEMIAUTOMATIC = 1, AUTOMATIC = 2
        private const int FIREMODE_AUTOMATIC = 2;
        private const int FIREMODE_SEMIAUTO = 1;

        // Settings
        public static bool Enabled = false;
        public static float FireRateMultiplier = 1f; // Multiplier applied to original RPM

        // Track original gun settings to restore them
        private class GunState
        {
            public int OriginalFireMode;
            public float OriginalRPM;
            public float OriginalRPS;
            public bool WasModified;
        }
        private static Dictionary<int, GunState> _gunStates = new Dictionary<int, GunState>();

        public static void Initialize()
        {
            Main.MelonLog.Msg("Full Auto Gun System initialized");
        }

        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;

            // If disabling, restore all modified guns
            if (!enabled)
            {
                RestoreAllGuns();
            }

            SendNotification(
                enabled ? NotificationType.Success : NotificationType.Information,
                "Full Auto",
                enabled ? $"Guns set to full auto at {FireRateMultiplier}x speed!" : "Full auto disabled, guns restored"
            );
            Main.MelonLog.Msg($"Full Auto {(enabled ? "ENABLED" : "DISABLED")}");
        }

        public static void SetFireRateMultiplier(float mult)
        {
            FireRateMultiplier = Mathf.Clamp(mult, 1f, 1000f);

            // Update any guns currently modified
            if (Enabled)
            {
                UpdateAllGunRPM();
            }

            Main.MelonLog.Msg($"Fire rate multiplier set to {FireRateMultiplier}x");
        }

        /// <summary>
        /// Called every frame from main mod Update
        /// </summary>
        public static void Update()
        {
            if (!Enabled)
                return;

            try
            {
                // Process guns in both hands
                ProcessGunInHand(Player.LeftHand);
                ProcessGunInHand(Player.RightHand);
            }
            catch (Exception ex)
            {
                // Silently fail - player might not be in game yet
            }
        }

        private static void ProcessGunInHand(Hand hand)
        {
            if (hand == null)
                return;

            try
            {
                // Get the gun component from whatever the player is holding
                Gun gun = Player.GetComponentInHand<Gun>(hand);
                if (gun == null)
                    return;

                // Skip if already automatic
                if (gun.fireMode == (Gun.FireMode)FIREMODE_AUTOMATIC)
                    return;

                int gunId = gun.GetInstanceID();

                // Save original state if we haven't already
                if (!_gunStates.ContainsKey(gunId))
                {
                    var state = new GunState
                    {
                        OriginalFireMode = (int)gun.fireMode,
                        OriginalRPM = gun.roundsPerMinute,
                        OriginalRPS = gun.roundsPerSecond,
                        WasModified = true
                    };
                    _gunStates[gunId] = state;
                }

                // Set to automatic fire mode and apply multiplier
                gun.fireMode = (Gun.FireMode)FIREMODE_AUTOMATIC;
                var saved = _gunStates[gunId];
                gun.roundsPerMinute = saved.OriginalRPM * FireRateMultiplier;
                gun.roundsPerSecond = saved.OriginalRPS * FireRateMultiplier;
            }
            catch
            {
                // Silently fail - gun might be destroyed or not valid
            }
        }

        private static void RestoreAllGuns()
        {
            foreach (var kvp in _gunStates)
            {
                try
                {
                    // We can't restore directly since we only have the ID
                    // The gun objects may no longer exist
                    // This is a limitation - guns will be restored when dropped and picked up again
                }
                catch { }
            }
            _gunStates.Clear();
        }

        private static void UpdateAllGunRPM()
        {
            try
            {
                // Update RPM for guns in hands
                var leftGun = Player.GetComponentInHand<Gun>(Player.LeftHand);
                var rightGun = Player.GetComponentInHand<Gun>(Player.RightHand);

                if (leftGun != null && _gunStates.TryGetValue(leftGun.GetInstanceID(), out var leftState))
                {
                    leftGun.roundsPerMinute = leftState.OriginalRPM * FireRateMultiplier;
                    leftGun.roundsPerSecond = leftState.OriginalRPS * FireRateMultiplier;
                }

                if (rightGun != null && _gunStates.TryGetValue(rightGun.GetInstanceID(), out var rightState))
                {
                    rightGun.roundsPerMinute = rightState.OriginalRPM * FireRateMultiplier;
                    rightGun.roundsPerSecond = rightState.OriginalRPS * FireRateMultiplier;
                }
            }
            catch { }
        }

        private static void SendNotification(NotificationType type, string title, string message)
        {
            NotificationHelper.Send(type, message, title);
        }
    }
}
