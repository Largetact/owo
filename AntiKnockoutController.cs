using MelonLoader;
using UnityEngine;
using BoneLib.Notifications;
using HarmonyLib;
using LabFusion.Player;
using System;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Anti-Knockout Controller - Prevents player knockout
    /// Direct port from FusionProtector (without permission checking)
    /// </summary>
    public static class AntiKnockoutController
    {
        private static bool _isEnabled = false;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                Main.MelonLog.Msg($"Anti-Knockout: {(_isEnabled ? "ENABLED" : "DISABLED")}");
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Anti-Knockout controller initialized");
        }

        public static void Toggle()
        {
            IsEnabled = !IsEnabled;
            SendNotification(IsEnabled ? NotificationType.Success : NotificationType.Warning,
                IsEnabled ? "Anti-Knockout ENABLED" : "Anti-Knockout DISABLED");
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }

    /// <summary>
    /// Harmony patch to block knockout when Anti-Knockout is enabled
    /// Based on FusionProtector's RealKnockOutScrubs patch
    /// </summary>
    [HarmonyPatch(typeof(LocalRagdoll), "KnockoutCoroutine")]
    public static class AntiKnockoutPatch
    {
        public static bool Prefix()
        {
            // If Anti-Knockout is enabled, prevent knockout
            if (AntiKnockoutController.IsEnabled)
            {
                return false; // Skip the original method
            }
            return true; // Allow original method to run
        }
    }
}
