using MelonLoader;
using UnityEngine;
using BoneLib;
using System;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Unbreakable Grip - Prevents the player's grip from being forcibly broken
    /// when climbing, grabbing players, or holding heavy objects.
    /// Works by continuously resetting grip release conditions on the physics rig.
    /// </summary>
    public static class UnbreakableGripController
    {
        private static bool _enabled = false;

        public static bool IsEnabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Unbreakable Grip {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Unbreakable Grip controller initialized");
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                PreventGripBreak();
            }
            catch { }
        }

        /// <summary>
        /// Prevent grip from detaching by overriding grip force thresholds and pull limits.
        /// Uses reflection to find and modify relevant properties on the PhysicsRig hands.
        /// </summary>
        private static void PreventGripBreak()
        {
            var rigManager = Player.RigManager;
            if (rigManager == null || rigManager.physicsRig == null)
                return;

            try
            {
                // Get both hands from the physics rig
                var physRig = rigManager.physicsRig;

                // Try to modify hand grip strength properties
                ModifyHandGrip(physRig.leftHand);
                ModifyHandGrip(physRig.rightHand);

                // Also try to find and modify any HandGripPair or Grip components
                ModifyAllGripComponents(physRig);
            }
            catch { }
        }

        /// <summary>
        /// Modify a hand's grip settings to prevent forced release
        /// </summary>
        private static void ModifyHandGrip(object hand)
        {
            if (hand == null) return;

            try
            {
                var handType = hand.GetType();

                // Try to increase grip force / change grip break distance
                // Common properties in SLZ.Marrow.Hand:
                // - maxGrabMass, forceMultiplier, pullForce, etc.

                var maxGrabMassProp = handType.GetProperty("maxGrabMass", BindingFlags.Public | BindingFlags.Instance);
                if (maxGrabMassProp != null && maxGrabMassProp.PropertyType == typeof(float))
                {
                    try { maxGrabMassProp.SetValue(hand, 999999f); } catch { }
                }

                // Set grip pull limit very high to prevent forced ungrip
                var pullLimitField = handType.GetField("_maxPullDistance", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? handType.GetField("maxPullDistance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pullLimitField != null && pullLimitField.FieldType == typeof(float))
                {
                    try { pullLimitField.SetValue(hand, 999f); } catch { }
                }

                // Try to set grip break force very high
                var gripBreakField = handType.GetField("_gripBreakForce", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? handType.GetField("gripBreakForce", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (gripBreakField != null && gripBreakField.FieldType == typeof(float))
                {
                    try { gripBreakField.SetValue(hand, 999999f); } catch { }
                }

                // Try to override any attached joint break force
                var comp = hand as Component;
                if (comp != null)
                {
                    var joints = comp.gameObject.GetComponentsInChildren<Joint>();
                    if (joints != null)
                    {
                        foreach (var joint in joints)
                        {
                            if (joint != null)
                            {
                                try
                                {
                                    joint.breakForce = float.PositiveInfinity;
                                    joint.breakTorque = float.PositiveInfinity;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Find all grip-related components on the physics rig and make them unbreakable
        /// </summary>
        private static void ModifyAllGripComponents(object physRig)
        {
            try
            {
                var physRigComponent = physRig as Component;
                if (physRigComponent == null) return;

                // Find all ConfigurableJoints on the rig and set break forces to infinity
                var joints = physRigComponent.gameObject.GetComponentsInChildren<ConfigurableJoint>(true);
                if (joints != null)
                {
                    foreach (var joint in joints)
                    {
                        if (joint != null)
                        {
                            try
                            {
                                joint.breakForce = float.PositiveInfinity;
                                joint.breakTorque = float.PositiveInfinity;
                            }
                            catch { }
                        }
                    }
                }

                // Also try regular joints
                var regularJoints = physRigComponent.gameObject.GetComponentsInChildren<Joint>(true);
                if (regularJoints != null)
                {
                    foreach (var joint in regularJoints)
                    {
                        if (joint != null)
                        {
                            try
                            {
                                joint.breakForce = float.PositiveInfinity;
                                joint.breakTorque = float.PositiveInfinity;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
