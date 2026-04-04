using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    public static class SpawnLimiterController
    {
        private static bool _enabled = true;
        private static bool _hostOnly = true;           // only enforce when host
        private static float _spawnDelay = 0.3f;        // min seconds between spawns (lowered for snappier feel)
        private static int _maxPerFrame = 3;             // max spawns per frame

        // Per-player rate tracking (works as both host and client)
        internal static readonly Dictionary<ulong, float> LastSpawnTimeByPlayer = new();
        // Client-side global rate tracking
        internal static float LastLocalSpawnTime = 0f;
        internal static int SpawnsThisFrame = 0;
        internal static int LastFrameCount = -1;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static bool HostOnly
        {
            get => _hostOnly;
            set => _hostOnly = value;
        }

        /// <summary>
        /// Returns true if we should actively enforce spawn limiting.
        /// When HostOnly is true, only enforces if we are the host.
        /// </summary>
        private static bool ShouldEnforce()
        {
            if (!_enabled) return false;
            if (!_hostOnly) return true;
            try { return NetworkInfo.IsHost; } catch { return false; }
        }

        /// <summary>
        /// Minimum seconds between spawns (per-player when host, global when client).
        /// </summary>
        public static float SpawnDelay
        {
            get => _spawnDelay;
            set => _spawnDelay = Mathf.Clamp(value, 0.05f, 5f);
        }

        /// <summary>
        /// Maximum number of spawns allowed in a single frame before blocking.
        /// </summary>
        public static int MaxPerFrame
        {
            get => _maxPerFrame;
            set => _maxPerFrame = Mathf.Clamp(value, 1, 50);
        }

        /// <summary>
        /// Returns true if the spawn should be ALLOWED, false if blocked.
        /// Used for client-side rate limiting (SpawnResponseMessage).
        /// </summary>
        internal static bool AllowLocalSpawn()
        {
            if (!ShouldEnforce()) return true;

            int frame = Time.frameCount;
            if (frame != LastFrameCount)
            {
                LastFrameCount = frame;
                SpawnsThisFrame = 0;
            }

            // Per-frame cap
            if (SpawnsThisFrame >= _maxPerFrame)
                return false;

            // Time-based rate limit
            float now = Time.realtimeSinceStartup;
            if (now - LastLocalSpawnTime < _spawnDelay)
                return false;

            LastLocalSpawnTime = now;
            SpawnsThisFrame++;
            return true;
        }

        /// <summary>
        /// Returns true if the spawn should be ALLOWED for a given player (host-side).
        /// </summary>
        internal static bool AllowPlayerSpawn(ulong platformId)
        {
            if (!ShouldEnforce()) return true;

            float now = Time.realtimeSinceStartup;
            if (LastSpawnTimeByPlayer.TryGetValue(platformId, out float last) && now - last < _spawnDelay)
                return false;

            LastSpawnTimeByPlayer[platformId] = now;
            return true;
        }
    }

    // ─── Per-player rate-limit on all spawn network messages ───
    [HarmonyPatch(typeof(NativeMessageHandler), "Handle")]
    public static class SpawnLimiterNetworkPatch
    {
        public static bool Prefix(NativeMessageHandler __instance, ReceivedMessage received)
        {
            if (!SpawnLimiterController.Enabled) return true;

            if (__instance is SpawnRequestMessage || __instance is SpawnResponseMessage)
            {
                try
                {
                    if (!received.PlatformID.HasValue) return true;

                    ulong platformId = received.PlatformID.Value;

                    if (!SpawnLimiterController.AllowPlayerSpawn(platformId))
                        return false;
                }
                catch { }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SpawnResponseMessage), "OnHandleMessage")]
    public static class SpawnLimiterFrameCapPatch
    {
        public static bool Prefix()
        {
            if (!SpawnLimiterController.Enabled) return true;

            if (!SpawnLimiterController.AllowLocalSpawn())
                return false;

            return true;
        }
    }
}
