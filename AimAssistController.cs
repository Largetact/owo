using System;
using System.Collections.Generic;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using HarmonyLib;
using MelonLoader;

namespace BonelabUtilityMod
{
    public enum AimTarget
    {
        Head,
        Chest,
        Closest
    }

    public enum CompensationSmoothing
    {
        None,
        Low,
        Medium,
        High,
        Adaptive
    }

    /// <summary>
    /// Fusion-player aimbot rewritten from AimHacksBL.dll.
    /// Targets other Fusion players (not NPCs) via PlayerTargeting.
    /// Hooks Gun.Fire to redirect the fire point toward the target.
    /// </summary>
    public static class AimAssistController
    {
        // ── Settings ──
        public static bool Enabled = false;
        public static bool AimBotEnabled = true;
        public static bool TriggerBotEnabled = false;
        public static bool HeadshotsOnly = false;
        public static bool BulletDropComp = true;
        public static bool MovementComp = true;
        public static bool AccelerationComp = true;
        public static float AimFOV = 180f;
        public static AimTarget Target = AimTarget.Head;
        public static CompensationSmoothing Smoothing = CompensationSmoothing.Adaptive;
        public static float TriggerBotDelay = 0.08f;

        // ── Internal ──
        private static readonly Dictionary<int, GunState> _guns = new Dictionary<int, GunState>();
        private static readonly Dictionary<int, PlayerVelocity> _playerVelocities = new Dictionary<int, PlayerVelocity>();

        private struct GunState
        {
            public Gun Gun;
            public Quaternion DefaultRotation;
            public float LastFired;
            public bool Registered;
        }

        private struct PlayerVelocity
        {
            public Vector3[] Positions;
            public float[] Times;
            public int Index;
            public int Count;
        }

        private const int SNAPSHOT_COUNT = 10;

        public static void Initialize()
        {
            _guns.Clear();
            _playerVelocities.Clear();
        }

        public static void Update()
        {
            if (!Enabled) return;

            UpdatePlayerVelocities();

            // Triggerbot: check all held guns
            if (TriggerBotEnabled)
            {
                foreach (var kv in _guns)
                {
                    var gs = kv.Value;
                    if (gs.Gun == null) continue;
                    try { RunTriggerBot(gs.Gun); } catch { }
                }
            }
        }

        // ═══════════════════════════════════════
        // GUN REGISTRATION (called from Harmony patch)
        // ═══════════════════════════════════════

        public static void OnGunStart(Gun gun)
        {
            if (gun == null) return;
            try
            {
                // Skip NPC guns — only register player-held guns
                var root = ((UnityEngine.Component)gun).transform.root;
                if (root.GetComponentInChildren<Il2CppSLZ.Marrow.AI.AIBrain>() != null) return;

                int id = gun.GetInstanceID();
                if (_guns.ContainsKey(id)) return;

                _guns[id] = new GunState
                {
                    Gun = gun,
                    DefaultRotation = gun.firePointTransform.localRotation,
                    LastFired = 0f,
                    Registered = true
                };
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // AIMBOT (called from pre-fire hook)
        // ═══════════════════════════════════════

        public static void OnPreFire(Gun gun)
        {
            if (!Enabled || !AimBotEnabled) return;
            if (gun == null) return;

            try
            {
                // Verify the player is holding this gun
                if (gun.host == null || gun.host.HandCount() < 1) return;
                if (gun.host._hands[0].manager != Player.RigManager) return;

                int id = gun.GetInstanceID();
                if (!_guns.ContainsKey(id)) return;

                var gs = _guns[id];
                gun.firePointTransform.localRotation = gs.DefaultRotation;

                RunAimbot(gun, false);
            }
            catch { }
        }

        public static void OnPostFire(Gun gun)
        {
            if (gun == null) return;
            try
            {
                if (gun.host == null || gun.host.HandCount() < 1) return;
                if (gun.host._hands[0].manager != Player.RigManager) return;

                int id = gun.GetInstanceID();
                if (_guns.ContainsKey(id))
                {
                    var gs = _guns[id];
                    gun.firePointTransform.localRotation = gs.DefaultRotation;
                    gs.LastFired = Time.time;
                    _guns[id] = gs;
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // AIMBOT CORE
        // ═══════════════════════════════════════

        private static void RunAimbot(Gun gun, bool noCompensation)
        {
            var targetEntry = FindClosestPlayer(gun);
            if (targetEntry == null) return;

            RigManager targetRig = targetEntry;
            float startVelocity = gun.defaultCartridge.projectile.startVelocity;

            // Get target point
            Vector3 targetPos = GetTargetPoint(targetRig, gun, Target);
            if (HeadshotsOnly && TriggerBotEnabled)
                targetPos = GetHeadPos(targetRig);

            // Movement compensation
            if (!noCompensation && MovementComp)
            {
                Vector3 comp = GetMovementCompensation(targetRig, targetPos, startVelocity, gun);
                targetPos += comp;
            }

            // Aim
            Vector3 aimDir = (targetPos - gun.firePointTransform.position).normalized;
            gun.firePointTransform.forward = aimDir;

            // Bullet drop
            if (!noCompensation && BulletDropComp)
            {
                Vector3 dropAngle = CalculateBulletDrop(targetPos, startVelocity, gun);
                gun.firePointTransform.Rotate(dropAngle, Space.Self);
            }
        }

        // ═══════════════════════════════════════
        // TRIGGERBOT
        // ═══════════════════════════════════════

        private static void RunTriggerBot(Gun gun)
        {
            if (gun.host == null || gun.host.HandCount() < 1) return;
            if (gun.host._hands[0].manager != Player.RigManager) return;

            int id = gun.GetInstanceID();
            if (!_guns.ContainsKey(id)) return;

            var gs = _guns[id];
            if (Time.time - gs.LastFired < TriggerBotDelay) return;

            // Raycast from fire point
            if (!Physics.Raycast(gun.firePointTransform.position, gun.firePointTransform.forward, out RaycastHit hit, 500f))
                return;

            // Check if hit a player rig
            var hitRoot = hit.collider.transform.root;
            var hitRig = hitRoot.GetComponentInChildren<RigManager>();
            if (hitRig == null || hitRig == Player.RigManager) return;
            if (IsPlayerDead(hitRig)) return;

            // Verify it's a Fusion player
            bool isFusionPlayer = false;
            var cached = PlayerTargeting.GetCachedPlayers();
            foreach (var p in cached)
            {
                if (p.Rig == hitRig) { isFusionPlayer = true; break; }
            }
            if (!isFusionPlayer) return;

            // Aim + fire
            if (AimBotEnabled)
                RunAimbot(gun, false);

            try { gun.Fire(); } catch { }
        }

        // ═══════════════════════════════════════
        // TARGET FINDING (Fusion players)
        // ═══════════════════════════════════════

        /// <summary>Returns true only if the rig is definitively dead (alive=false).
        /// Does NOT check deathIsImminent or curr_Health — god mode players can sit
        /// at zero HP / imminent death indefinitely without actually dying.</summary>
        private static bool IsPlayerDead(RigManager rig)
        {
            try
            {
                var h = rig.health;
                if (h == null) return false;
                if (!h.alive) return true;
            }
            catch { }
            return false;
        }

        private static RigManager FindClosestPlayer(Gun gun)
        {
            var players = PlayerTargeting.GetCachedPlayers();
            var localRig = Player.RigManager;

            float halfFov = AimFOV / 2f;
            RigManager best = null;
            float bestAngle = halfFov;

            foreach (var p in players)
            {
                if (p.Rig == null || p.Rig == localRig) continue;
                if (IsPlayerDead(p.Rig)) continue;

                Vector3? headPos = PlayerTargeting.GetTargetHeadPosition(p.Rig);
                if (!headPos.HasValue) continue;

                Vector3 toTarget = headPos.Value - gun.firePointTransform.position;
                float angle = Vector3.Angle(gun.firePointTransform.forward, toTarget);

                if (angle < bestAngle)
                {
                    // Visibility check — RaycastAll to skip spent casings/projectiles near muzzle
                    if (CheckTargetVisible(gun.firePointTransform.position, toTarget.normalized, toTarget.magnitude + 1f, p.Rig))
                    {
                        bestAngle = angle;
                        best = p.Rig;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// RaycastAll visibility check that skips dynamic objects (spent casings, projectiles,
        /// debris) which would otherwise block a single Raycast after a few shots.
        /// </summary>
        private static bool CheckTargetVisible(Vector3 origin, Vector3 direction, float maxDist, RigManager targetRig)
        {
            var hits = Physics.RaycastAll(origin, direction, maxDist);
            if (hits == null || hits.Length == 0) return false;

            int targetRootId = ((UnityEngine.Component)targetRig).transform.root.GetInstanceID();
            int localRootId = 0;
            try
            {
                if (Player.RigManager != null)
                    localRootId = ((UnityEngine.Component)Player.RigManager).transform.root.GetInstanceID();
            }
            catch { }

            // Sort by distance so we process nearest hits first
            System.Array.Sort<RaycastHit>(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;

                int hitRootId = hit.collider.transform.root.GetInstanceID();

                // Skip local player body (hands, torso near gun)
                if (localRootId != 0 && hitRootId == localRootId) continue;

                // Hit the target player — visible
                if (hitRootId == targetRootId) return true;

                // Static geometry (no rigidbody) = wall/floor — target is occluded
                if (hit.collider.attachedRigidbody == null) return false;

                // Dynamic object (has rigidbody) = casing, projectile, debris — skip past it
            }

            return false;
        }

        private static Vector3 GetTargetPoint(RigManager rig, Gun gun, AimTarget target)
        {
            try
            {
                var pr = rig.physicsRig;
                if (pr == null) return ((UnityEngine.Component)rig).transform.position;

                if (target == AimTarget.Head)
                    return pr.torso.rbHead != null ? pr.torso.rbHead.position : ((UnityEngine.Component)rig).transform.position;

                if (target == AimTarget.Chest)
                    return pr.torso.rbChest != null ? pr.torso.rbChest.position : ((UnityEngine.Component)rig).transform.position;

                if (target == AimTarget.Closest)
                {
                    // Find the bone closest to the gun's aim direction
                    float bestAngle = 999f;
                    Vector3 bestPos = pr.torso.rbChest != null ? pr.torso.rbChest.position : ((UnityEngine.Component)rig).transform.position;

                    var bones = new Rigidbody[] {
                        pr.torso.rbHead, pr.torso.rbNeck, pr.torso.rbChest,
                        pr.torso.rbSpine, pr.torso.rbPelvis
                    };

                    foreach (var bone in bones)
                    {
                        if (bone == null) continue;
                        Vector3 dir = bone.position - gun.firePointTransform.position;
                        float a = Vector3.Angle(gun.firePointTransform.forward, dir);
                        if (a < bestAngle)
                        {
                            bestAngle = a;
                            bestPos = bone.position;
                        }
                    }
                    return bestPos;
                }
            }
            catch { }
            return ((UnityEngine.Component)rig).transform.position;
        }

        private static Vector3 GetHeadPos(RigManager rig)
        {
            try
            {
                var pr = rig.physicsRig;
                if (pr?.torso?.rbHead != null) return pr.torso.rbHead.position;
            }
            catch { }
            return ((UnityEngine.Component)rig).transform.position;
        }

        // ═══════════════════════════════════════
        // MOVEMENT COMPENSATION
        // ═══════════════════════════════════════

        private static void UpdatePlayerVelocities()
        {
            var localRig = Player.RigManager;
            var players = PlayerTargeting.GetCachedPlayers();

            foreach (var p in players)
            {
                if (p.Rig == null || p.Rig == localRig) continue;

                int id = p.Rig.GetInstanceID();
                Vector3? pos = PlayerTargeting.GetTargetPosition(p.Rig);
                if (!pos.HasValue) continue;

                if (!_playerVelocities.ContainsKey(id))
                {
                    _playerVelocities[id] = new PlayerVelocity
                    {
                        Positions = new Vector3[SNAPSHOT_COUNT],
                        Times = new float[SNAPSHOT_COUNT],
                        Index = 0,
                        Count = 0
                    };
                }

                var pv = _playerVelocities[id];
                pv.Positions[pv.Index] = pos.Value;
                pv.Times[pv.Index] = Time.time;
                pv.Index = (pv.Index + 1) % SNAPSHOT_COUNT;
                if (pv.Count < SNAPSHOT_COUNT) pv.Count++;
                _playerVelocities[id] = pv;
            }
        }

        private static Vector3 GetAverageVelocity(int rigId)
        {
            if (!_playerVelocities.ContainsKey(rigId)) return Vector3.zero;
            var pv = _playerVelocities[rigId];
            if (pv.Count < 2) return Vector3.zero;

            int smoothCount = GetSmoothCount();
            if (smoothCount > pv.Count - 1) smoothCount = pv.Count - 1;

            Vector3 totalVel = Vector3.zero;
            int count = 0;

            for (int i = 0; i < smoothCount; i++)
            {
                int curr = (pv.Index - 1 - i + SNAPSHOT_COUNT) % SNAPSHOT_COUNT;
                int prev = (curr - 1 + SNAPSHOT_COUNT) % SNAPSHOT_COUNT;
                if (curr < 0 || prev < 0) continue;

                float dt = pv.Times[curr] - pv.Times[prev];
                if (dt <= 0.001f) continue;

                totalVel += (pv.Positions[curr] - pv.Positions[prev]) / dt;
                count++;
            }

            return count > 0 ? totalVel / count : Vector3.zero;
        }

        private static Vector3 GetAverageAcceleration(int rigId)
        {
            if (!AccelerationComp) return Vector3.zero;
            if (!_playerVelocities.ContainsKey(rigId)) return Vector3.zero;
            var pv = _playerVelocities[rigId];
            if (pv.Count < 3) return Vector3.zero;

            int smoothCount = GetSmoothCount();
            if (smoothCount > pv.Count - 2) smoothCount = pv.Count - 2;

            Vector3 totalAccel = Vector3.zero;
            int count = 0;

            for (int i = 0; i < smoothCount; i++)
            {
                int i2 = (pv.Index - 1 - i + SNAPSHOT_COUNT) % SNAPSHOT_COUNT;
                int i1 = (i2 - 1 + SNAPSHOT_COUNT) % SNAPSHOT_COUNT;
                int i0 = (i1 - 1 + SNAPSHOT_COUNT) % SNAPSHOT_COUNT;

                float dt1 = pv.Times[i2] - pv.Times[i1];
                float dt0 = pv.Times[i1] - pv.Times[i0];
                if (dt1 <= 0.001f || dt0 <= 0.001f) continue;

                Vector3 v1 = (pv.Positions[i2] - pv.Positions[i1]) / dt1;
                Vector3 v0 = (pv.Positions[i1] - pv.Positions[i0]) / dt0;
                float dtAvg = (dt1 + dt0) / 2f;
                if (dtAvg <= 0.001f) continue;

                totalAccel += (v1 - v0) / dtAvg;
                count++;
            }

            return count > 0 ? totalAccel / count : Vector3.zero;
        }

        private static int GetSmoothCount()
        {
            switch (Smoothing)
            {
                case CompensationSmoothing.None: return 1;
                case CompensationSmoothing.Low: return 2;
                case CompensationSmoothing.Medium: return 4;
                case CompensationSmoothing.High: return 8;
                case CompensationSmoothing.Adaptive:
                default: return 5;
            }
        }

        private static Vector3 GetMovementCompensation(RigManager rig, Vector3 targetPos, float bulletVelocity, Gun gun)
        {
            int rigId = rig.GetInstanceID();
            Vector3 velocity = GetAverageVelocity(rigId);
            Vector3 acceleration = GetAverageAcceleration(rigId);

            float dist = Vector3.Distance(gun.firePointTransform.position, targetPos);
            float travelTime = dist / Mathf.Max(bulletVelocity, 1f);

            // First pass: basic prediction
            Vector3 predicted = velocity * travelTime;
            if (AccelerationComp)
                predicted += 0.5f * acceleration * travelTime * travelTime;

            // Second pass: refine with new distance
            Vector3 newTarget = targetPos + predicted;
            float newDist = Vector3.Distance(gun.firePointTransform.position, newTarget);
            float newTime = newDist / Mathf.Max(bulletVelocity, 1f);

            Vector3 refined = velocity * newTime;
            if (AccelerationComp)
                refined += 0.5f * acceleration * newTime * newTime;

            return refined;
        }

        // ═══════════════════════════════════════
        // BULLET DROP
        // ═══════════════════════════════════════

        private static Vector3 CalculateBulletDrop(Vector3 targetPos, float velocity, Gun gun)
        {
            Vector3 gravity = Physics.gravity;
            Vector3 firePos = gun.firePointTransform.position;

            float angleX = 0f, angleY = 0f, angleZ = 0f;

            if (gravity.y != 0f)
            {
                float horizDist = Vector3.Distance(
                    new Vector3(firePos.x, 0f, firePos.z),
                    new Vector3(targetPos.x, 0f, targetPos.z));
                float vertDist = firePos.y - targetPos.y;
                float theta = Mathf.Atan2(horizDist, vertDist) * Mathf.Rad2Deg;
                float inner = (-gravity.y * horizDist * horizDist / (velocity * velocity) - vertDist)
                    / Mathf.Sqrt(vertDist * vertDist + horizDist * horizDist);
                if (inner <= 1f)
                    angleY = 90f - (Mathf.Acos(inner) * Mathf.Rad2Deg + theta) / 2f;
            }

            if (gravity.x != 0f)
            {
                float d = Vector3.Distance(
                    new Vector3(0f, firePos.y, firePos.z),
                    new Vector3(0f, targetPos.y, targetPos.z));
                float h = firePos.x - targetPos.x;
                float theta = Mathf.Atan2(d, h) * Mathf.Rad2Deg;
                float inner = (-gravity.x * d * d / (velocity * velocity) - h) / Mathf.Sqrt(h * h + d * d);
                if (inner <= 1f)
                    angleX = 90f - (Mathf.Acos(inner) * Mathf.Rad2Deg + theta) / 2f;
            }

            if (gravity.z != 0f)
            {
                float d = Vector3.Distance(
                    new Vector3(firePos.x, firePos.y, 0f),
                    new Vector3(targetPos.x, targetPos.y, 0f));
                float h = firePos.x - targetPos.x;
                float theta = Mathf.Atan2(d, h) * Mathf.Rad2Deg;
                float inner = (gravity.z * d * d / (velocity * velocity) - h) / Mathf.Sqrt(h * h + d * d);
                if (inner <= 1f)
                    angleZ = 90f - (Mathf.Acos(inner) * Mathf.Rad2Deg + theta) / 2f;
            }

            angleX *= Mathf.Sign(firePos.z - targetPos.z);
            angleZ *= Mathf.Sign(firePos.x - targetPos.x);

            return new Vector3(-angleY, -angleX - angleZ, 0f);
        }
    }

    // ═══════════════════════════════════════
    // Harmony patches for Gun — auto-applied by MelonLoader
    // ═══════════════════════════════════════

    [HarmonyPatch(typeof(Gun), "Fire")]
    internal static class AimAssistGunFirePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Gun __instance)
        {
            // Auto-register guns on first fire (Gun has no patchable Start/OnEnable in IL2CPP)
            AimAssistController.OnGunStart(__instance);
            AimAssistController.OnPreFire(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(Gun __instance) => AimAssistController.OnPostFire(__instance);
    }
}
