using System;
using BoneLib;
using Il2CppSLZ.Marrow;
using UnityEngine;
using static BonelabUtilityMod.Main;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Allows magazine insertion while ragdolled in ARM_CONTROL mode.
    /// RagdollRig() weakens arm joint drives, making the strict AlignPlug angle/distance
    /// checks nearly impossible to satisfy. This controller detects when a held magazine is
    /// brought near a held gun's AmmoSocket and calls AlignPlug.ForceInSocket() to assist
    /// the insertion with a generous proximity threshold.
    /// </summary>
    public static class RagdollReloadController
    {
        // ── Public Settings ──
        public static bool Enabled = true;

        /// <summary>
        /// Maximum distance (metres) between the magazine and the gun's ammo socket
        /// for assisted insertion to trigger. Default 0.15m (~6 inches) feels natural.
        /// </summary>
        public static float InsertDistance = 0.15f;

        // ── Internal State ──
        private static float _cooldown;
        private const float COOLDOWN_DURATION = 0.3f; // prevent rapid re-inserts

        // ── Entry Points ──

        public static void Initialize()
        {
            // Nothing needed — stateless per-frame check
        }

        public static void Update()
        {
            if (!Enabled) return;

            // Cooldown between insertions to avoid double-triggers
            if (_cooldown > 0f) { _cooldown -= Time.deltaTime; return; }

            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                // Only assist when actually ragdolled (torso shutdown or ball loco disabled)
                bool isRagdolled = physRig.torso.shutdown || !physRig.ballLocoEnabled;
                if (!isRagdolled) return;

                // Try both hand combinations: gun in left + mag in right, and vice versa
                if (TryAssistReload(Player.LeftHand, Player.RightHand)) return;
                TryAssistReload(Player.RightHand, Player.LeftHand);
            }
            catch { }
        }

        /// <summary>
        /// Check if gunHand holds a gun and magHand holds a magazine,
        /// and if the magazine is close enough to the gun's ammo socket,
        /// force-insert it.
        /// </summary>
        private static bool TryAssistReload(Hand gunHand, Hand magHand)
        {
            if (gunHand == null || magHand == null) return false;

            // Get gun from one hand
            Gun gun = Player.GetComponentInHand<Gun>(gunHand);
            if (gun == null) return false;

            // Check the gun actually has an ammo socket and it doesn't already have a mag
            AmmoSocket ammoSocket = gun.ammoSocket;
            if (ammoSocket == null) return false;
            if (ammoSocket._isMagazineInserted) return false;

            // Get magazine from the other hand
            Magazine mag = Player.GetComponentInHand<Magazine>(magHand);
            if (mag == null) return false;

            // Get the magazine's AmmoPlug (Magazine → AmmoPlug on same hierarchy)
            AmmoPlug ammoPlug = ((Component)mag).GetComponentInChildren<AmmoPlug>();
            if (ammoPlug == null) return false;

            // Already being inserted or locked
            if (ammoPlug._isLocked || ammoPlug._isEnterTransition) return false;

            // Distance check between the magazine plug and the socket's end position
            Vector3 plugPos = ((Component)ammoPlug).transform.position;
            Transform socketEnd = ammoSocket.endTransform;
            Vector3 socketPos = socketEnd != null ? socketEnd.position : ((Component)ammoSocket).transform.position;

            float dist = Vector3.Distance(plugPos, socketPos);
            if (dist > InsertDistance) return false;

            // Force the magazine into the socket — bypasses the strict alignment checks
            ammoPlug.ForceInSocket((Socket)ammoSocket);
            _cooldown = COOLDOWN_DURATION;
            return true;
        }
    }
}
