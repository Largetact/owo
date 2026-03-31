using System;
using System.Collections.Generic;
using System.Reflection;
using BoneLib;
using BoneLib.Notifications;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// AI NPC Controller — manipulate NPC mental states, HP, and physics.
    /// Works on NPCs you're holding or nearby NPCs.
    /// Safe and Fusion-friendly — only affects local NPC state (host only for synced NPCs).
    /// </summary>
    public static class AINpcController
    {
        public enum NpcMentalState
        {
            Rest = 0,
            Roam = 1,
            Investigate = 2,
            Agro = 3,
            Flee = 5
        }

        private static NpcMentalState _selectedState = NpcMentalState.Agro;
        private static float _customHp = 100f;
        private static float _customMass = 1f;

        public static NpcMentalState SelectedState { get => _selectedState; set => _selectedState = value; }
        public static float CustomHp { get => _customHp; set => _customHp = Mathf.Max(1f, value); }
        public static float CustomMass { get => _customMass; set => _customMass = Mathf.Clamp(value, 0.01f, 100f); }

        public static void Initialize()
        {
            Main.MelonLog.Msg("AI NPC controller initialized");
        }

        /// <summary>
        /// Apply the selected mental state to the NPC held in hand.
        /// </summary>
        public static void ApplyStateToHeld()
        {
            var brain = GetHeldNpcBrain();
            if (brain == null)
            {
                NotificationHelper.Send(NotificationType.Warning, "No NPC in hand");
                return;
            }

            ApplyMentalState(brain, _selectedState);
            NotificationHelper.Send(NotificationType.Success, $"NPC → {_selectedState}");
        }

        /// <summary>
        /// Set max HP on the NPC held in hand.
        /// </summary>
        public static void ApplyHpToHeld()
        {
            var brain = GetHeldNpcBrain();
            if (brain == null)
            {
                NotificationHelper.Send(NotificationType.Warning, "No NPC in hand");
                return;
            }

            SetNpcMaxHp(brain, _customHp);
            NotificationHelper.Send(NotificationType.Success, $"NPC HP → {_customHp}");
        }

        /// <summary>
        /// Set mass on the NPC held in hand.
        /// </summary>
        public static void ApplyMassToHeld()
        {
            var brain = GetHeldNpcBrain();
            if (brain == null)
            {
                NotificationHelper.Send(NotificationType.Warning, "No NPC in hand");
                return;
            }

            SetNpcMass(brain, _customMass);
            NotificationHelper.Send(NotificationType.Success, $"NPC mass → {_customMass}");
        }

        /// <summary>
        /// Apply selected mental state to ALL NPCs in the scene.
        /// </summary>
        public static void ApplyStateToAll()
        {
            int count = 0;
            try
            {
                // Find all AIBrain components by checking Il2Cpp type names
                var allComponents = UnityEngine.Object.FindObjectsOfType<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp != null && comp.GetIl2CppType().Name == "AIBrain")
                    {
                        ApplyMentalState(comp, _selectedState);
                        count++;
                    }
                }
            }
            catch { }

            NotificationHelper.Send(NotificationType.Success, $"{count} NPCs → {_selectedState}");
        }

        private static void ApplyMentalState(object brain, NpcMentalState state)
        {
            try
            {
                var behaviourProp = brain.GetType().GetProperty("behaviour",
                    BindingFlags.Public | BindingFlags.Instance);
                if (behaviourProp == null)
                    behaviourProp = brain.GetType().GetField("behaviour",
                        BindingFlags.Public | BindingFlags.Instance)?.FieldType.GetProperty("mentalState");

                var behaviour = brain.GetType().GetProperty("behaviour")?.GetValue(brain)
                    ?? brain.GetType().GetField("behaviour")?.GetValue(brain);
                if (behaviour == null) return;

                var mentalProp = behaviour.GetType().GetProperty("mentalState",
                    BindingFlags.Public | BindingFlags.Instance);
                if (mentalProp != null)
                {
                    mentalProp.SetValue(behaviour, (int)state);
                }
                else
                {
                    var mentalField = behaviour.GetType().GetField("mentalState",
                        BindingFlags.Public | BindingFlags.Instance);
                    mentalField?.SetValue(behaviour, (int)state);
                }
            }
            catch { }
        }

        private static void SetNpcMaxHp(object brain, float hp)
        {
            try
            {
                var behaviour = brain.GetType().GetProperty("behaviour")?.GetValue(brain)
                    ?? brain.GetType().GetField("behaviour")?.GetValue(brain);
                if (behaviour == null) return;

                var healthProp = behaviour.GetType().GetProperty("health")?.GetValue(behaviour)
                    ?? behaviour.GetType().GetField("health")?.GetValue(behaviour);
                if (healthProp == null) return;

                var maxHpField = healthProp.GetType().GetProperty("maxHitPoints",
                    BindingFlags.Public | BindingFlags.Instance);
                if (maxHpField != null)
                    maxHpField.SetValue(healthProp, hp);
                else
                {
                    var maxHpF = healthProp.GetType().GetField("maxHitPoints",
                        BindingFlags.Public | BindingFlags.Instance);
                    maxHpF?.SetValue(healthProp, hp);
                }
            }
            catch { }
        }

        private static void SetNpcMass(object brain, float mass)
        {
            try
            {
                var behaviour = brain.GetType().GetProperty("behaviour")?.GetValue(brain)
                    ?? brain.GetType().GetField("behaviour")?.GetValue(brain);
                if (behaviour == null) return;

                var selfRbsProp = behaviour.GetType().GetProperty("selfRbs")?.GetValue(behaviour)
                    ?? behaviour.GetType().GetField("selfRbs")?.GetValue(behaviour);
                if (selfRbsProp == null) return;

                var enumerable = selfRbsProp as System.Collections.IEnumerable;
                if (enumerable == null) return;

                foreach (var rbObj in enumerable)
                {
                    if (rbObj is Rigidbody rb)
                        rb.mass = mass;
                }
            }
            catch { }
        }

        /// <summary>
        /// Get the AIBrain from whatever the player is holding.
        /// </summary>
        private static object GetHeldNpcBrain()
        {
            try
            {
                // Check both hands
                foreach (bool left in new[] { true, false })
                {
                    var hand = left ? Player.LeftHand : Player.RightHand;
                    if (hand == null) continue;

                    var attached = hand.m_CurrentAttachedGO;
                    if (attached == null) continue;

                    var root = attached.transform.root;
                    if (root == null) continue;

                    // Look for AIBrain by checking all components
                    var components = root.GetComponentsInChildren<Component>(true);
                    foreach (var comp in components)
                    {
                        if (comp != null && comp.GetIl2CppType().Name == "AIBrain")
                            return comp;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
