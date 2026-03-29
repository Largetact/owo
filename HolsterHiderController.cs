using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Bonelab;
using System;

namespace BonelabUtilityMod
{
    public static class HolsterHiderController
    {
        private static bool _hideHolsters = false;
        private static bool _hideAmmoPouch = false;
        private static bool _hideBodyLog = false;

        // Retry timer: after level load, keep re-applying until transforms exist
        private static float _retryUntil = 0f;
        private static float _retryInterval = 0.5f;
        private static float _nextRetry = 0f;

        public static bool IsHidingAnything => _hideHolsters || _hideAmmoPouch || _hideBodyLog;

        public static bool HideHolsters
        {
            get => _hideHolsters;
            set { if (_hideHolsters == value) return; _hideHolsters = value; Apply(); }
        }

        public static bool HideAmmoPouch
        {
            get => _hideAmmoPouch;
            set { if (_hideAmmoPouch == value) return; _hideAmmoPouch = value; Apply(); }
        }

        public static bool HideBodyLog
        {
            get => _hideBodyLog;
            set
            {
                if (_hideBodyLog == value) return;
                _hideBodyLog = value;
                if (value)
                {
                    Apply();
                }
                else
                {
                    // Restore bodylog by re-enabling PullCordDevice + sphere
                    RestoreBodyLog();
                }
            }
        }

        public static void Initialize() { }

        /// <summary>
        /// Called on level load — schedule retries so state is applied even if rig isn't ready yet.
        /// Always retries: ensures bodylog is restored to correct state after level change.
        /// </summary>
        public static void OnLevelLoaded()
        {
            _retryUntil = Time.time + 5f;
            _nextRetry = 0f;
            Apply();
        }

        /// <summary>
        /// Called from main Update loop. Re-applies hiding periodically after level load.
        /// </summary>
        public static void Update()
        {
            if (_retryUntil <= 0f || Time.time > _retryUntil) return;
            if (Time.time < _nextRetry) return;
            _nextRetry = Time.time + _retryInterval;
            Apply();
        }

        public static void Apply()
        {
            try
            {
                var rm = Player.RigManager;
                if (rm == null) return;
                var physRig = rm.physicsRig;
                if (physRig == null) return;

                ApplyHolsters(physRig);
                ApplyAmmoPouch(physRig);
                ApplyBodyLog(physRig);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[HolsterHider] Apply error: {ex.Message}");
            }
        }

        private static void ApplyHolsters(PhysicsRig physRig)
        {
            bool show = !_hideHolsters;
            var spine = ((Rig)physRig).m_spine;
            if (spine == null) return;
            var spineTransform = ((Component)spine).transform;

            // Right hip holster
            ToggleMesh(spineTransform, "SideRt/prop_handGunHolster/strap_geo", show);
            ToggleMesh(spineTransform, "SideRt/prop_handGunHolster/handgunHolster_geo", show);
            // Left hip holster
            ToggleMesh(spineTransform, "SideLf/prop_handGunHolster/strap_geo", show);
            ToggleMesh(spineTransform, "SideLf/prop_handGunHolster/handgunHolster_geo", show);
            // Back pouch
            var pelvis = ((Rig)physRig).m_pelvis;
            if (pelvis != null)
            {
                ToggleMesh(((Component)pelvis).transform, "BackCt/prop_pouch", show);
            }
        }

        private static void ApplyAmmoPouch(PhysicsRig physRig)
        {
            bool show = !_hideAmmoPouch;
            var pelvis = ((Rig)physRig).m_pelvis;
            if (pelvis == null) return;
            var pelvisTransform = ((Component)pelvis).transform;

            ToggleMesh(pelvisTransform, "BeltLf1/InventoryAmmoReceiver/Holder", show);
            ToggleMesh(pelvisTransform, "BeltRt1/InventoryAmmoReceiver/Holder", show);
        }

        private static Transform FindBodyLog(PhysicsRig physRig)
        {
            var elbowRt = ((Rig)physRig).m_elbowRt;
            Transform bodyLogTransform = null;
            if (elbowRt != null)
                bodyLogTransform = elbowRt.Find("BodyLogSlot/BodyLog");
            if (bodyLogTransform == null)
            {
                var elbowLf = ((Rig)physRig).m_elbowLf;
                if (elbowLf != null)
                    bodyLogTransform = elbowLf.Find("BodyLogSlot/BodyLog");
            }
            return bodyLogTransform;
        }

        private static void ApplyBodyLog(PhysicsRig physRig)
        {
            try
            {
                var bodyLogTransform = FindBodyLog(physRig);
                if (bodyLogTransform == null) return;

                bool show = !_hideBodyLog;

                var pullCord = bodyLogTransform.GetComponent<PullCordDevice>();
                if (pullCord != null)
                {
                    if (pullCord.ballArt != null)
                        pullCord.ballArt.gameObject.SetActive(show);
                    if (pullCord.ballLine != null)
                        pullCord.ballLine.gameObject.SetActive(show);
                }

                // Outer ring mesh
                var outerRing = bodyLogTransform.Find("BodyLog/BodyLog");
                if (outerRing != null)
                {
                    var mr = outerRing.GetComponent<MeshRenderer>();
                    if (mr != null) ((Component)mr).gameObject.SetActive(show);
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[HolsterHider] BodyLog error: {ex.Message}");
            }
        }

        private static void RestoreBodyLog()
        {
            // Apply() now handles both show and hide states
            Apply();
        }

        private static void ToggleMesh(Transform root, string path, bool active)
        {
            if (root == null) return;
            var child = root.Find(path);
            if (child == null) return;
            child.gameObject.SetActive(active);
        }
    }
}
