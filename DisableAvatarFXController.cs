using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.VRMK;
using System;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Disables the visual particle effects that play when switching avatars.
    /// Hooks OnSwitchAvatarPostfix and immediately stops/destroys any ParticleSystems
    /// on the avatar and RigManager hierarchy.
    /// </summary>
    public static class DisableAvatarFXController
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static void Initialize()
        {
            Hooking.OnSwitchAvatarPostfix += OnAvatarSwapped;
            Main.MelonLog.Msg("DisableAvatarFXController initialized");
        }

        private static void OnAvatarSwapped(Avatar avatar)
        {
            if (!_enabled) return;

            try
            {
                var rm = Player.RigManager;
                if (rm == null) return;

                // Stop all active particle systems on the RigManager hierarchy
                var particles = ((Component)rm).GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
                if (particles != null)
                {
                    foreach (var ps in particles)
                    {
                        if (ps == null) continue;
                        if (ps.isPlaying)
                        {
                            ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[AvatarFX] Error stopping particles: {ex.Message}");
            }
        }
    }
}
