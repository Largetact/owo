using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Il2CppSLZ.Marrow;
using BoneLib;
using BoneLib.BoneMenu;

namespace BonelabUtilityMod
{
    public enum TargetFilter
    {
        NEAREST,
        HEAVIEST,
        LIGHTEST,
        SELECTED,
        FURTHEST
    }

    /// <summary>
    /// Shared utility for finding and targeting other players via LabFusion reflection.
    /// Uses the same PlayerIDManager + NetworkPlayerManager approach as TeleportController.
    /// Used by ObjectLauncher (homing), Dash (lock-on), and Flight (lock-on).
    /// </summary>
    public static class PlayerTargeting
    {
        // ───── Cached player list (same structure as TeleportController) ─────
        public struct PlayerEntry
        {
            public byte SmallID;
            public string DisplayName;
            public RigManager Rig;        // resolved from NetworkPlayer.RigRefs.RigManager
            public float Mass;
        }

        private static List<PlayerEntry> _cachedPlayers = new List<PlayerEntry>();
        private static float _lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 0.25f;

        public static List<PlayerEntry> GetCachedPlayers()
        {
            RefreshPlayerList();
            return _cachedPlayers;
        }

        // Selected player (for SELECTED filter)
        private static RigManager _selectedPlayer = null;
        private static string _selectedPlayerName = "None";

        public static string SelectedPlayerName => _selectedPlayerName;

        public static RigManager SelectedPlayer
        {
            get => _selectedPlayer;
            set
            {
                _selectedPlayer = value;
                if (value != null)
                {
                    // Find name from cached list
                    var entry = _cachedPlayers.FirstOrDefault(p => p.Rig == value);
                    _selectedPlayerName = entry.Rig != null ? entry.DisplayName : "Unknown";
                }
                else
                {
                    _selectedPlayerName = "None";
                }
                Main.MelonLog.Msg($"Selected target: {_selectedPlayerName}");
            }
        }

        // ───── Player search for BoneMenu ─────
        private static string _playerSearchQuery = "";
        public static string PlayerSearchQuery
        {
            get => _playerSearchQuery;
            set => _playerSearchQuery = value ?? "";
        }

        public static void Initialize()
        {
            _cachedPlayers.Clear();
            _selectedPlayer = null;
            _selectedPlayerName = "None";
            Main.MelonLog.Msg("PlayerTargeting initialized");
        }

        /// <summary>
        /// Cycle to the next available target (for keybind target switching).
        /// Uses the given filter to determine sort order, then picks the next one after the current selected.
        /// </summary>
        public static void CycleTarget(TargetFilter filter)
        {
            var head = Player.Head;
            if (head == null) return;
            Vector3 fromPos = head.position;

            RefreshPlayerList();
            var valid = GetValidPlayers(fromPos);
            if (valid.Count == 0)
            {
                _selectedPlayer = null;
                _selectedPlayerName = "None";
                Main.MelonLog.Msg("[Target] No targets available");
                return;
            }

            // Sort by distance
            valid.Sort((a, b) => a.dist.CompareTo(b.dist));

            // Find current index
            int currentIdx = -1;
            if (_selectedPlayer != null)
            {
                for (int i = 0; i < valid.Count; i++)
                {
                    if (valid[i].rig == _selectedPlayer)
                    {
                        currentIdx = i;
                        break;
                    }
                }
            }

            // Pick next
            int nextIdx = (currentIdx + 1) % valid.Count;
            var next = valid[nextIdx];
            SelectedPlayer = next.rig;
            Main.MelonLog.Msg($"[Target] Cycled to: {_selectedPlayerName} ({next.dist:0.0}m)");
            try
            {
                NotificationHelper.Send(
                    BoneLib.Notifications.NotificationType.Information,
                    $"Target: {_selectedPlayerName}");
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        // PUBLIC API — used by ObjectLauncher, Dash, Flight
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Find a target player based on filter criteria relative to a reference position.
        /// Returns null if no valid target found.
        /// </summary>
        public static RigManager FindTarget(TargetFilter filter, Vector3 fromPosition)
        {
            if (filter == TargetFilter.SELECTED)
            {
                if (_selectedPlayer != null)
                {
                    try
                    {
                        var comp = _selectedPlayer as UnityEngine.Component;
                        if (comp != null && comp.gameObject != null)
                            return _selectedPlayer;
                    }
                    catch { }
                }
                // Selected player gone — clear
                _selectedPlayer = null;
                _selectedPlayerName = "None";
                return null;
            }

            RefreshPlayerList();
            var valid = GetValidPlayers(fromPosition);
            if (valid.Count == 0) return null;

            switch (filter)
            {
                case TargetFilter.NEAREST:
                    return valid.OrderBy(v => v.dist).First().rig;
                case TargetFilter.HEAVIEST:
                    return valid.OrderByDescending(v => v.mass).First().rig;
                case TargetFilter.LIGHTEST:
                    return valid.OrderBy(v => v.mass).First().rig;
                case TargetFilter.FURTHEST:
                    return valid.OrderByDescending(v => v.dist).First().rig;
                default:
                    return valid[0].rig;
            }
        }

        /// <summary>
        /// Get the position of a target player (center mass — pelvis).
        /// </summary>
        public static Vector3? GetTargetPosition(RigManager target)
        {
            if (target == null) return null;
            try
            {
                var physRig = target.physicsRig;
                if (physRig?.torso?.rbPelvis != null)
                    return physRig.torso.rbPelvis.position;
                return ((UnityEngine.Component)target).transform.position;
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the position of a target player's head.
        /// </summary>
        public static Vector3? GetTargetHeadPosition(RigManager target)
        {
            if (target == null) return null;
            try
            {
                var physRig = target.physicsRig;
                if (physRig?.torso?.rbHead != null)
                    return physRig.torso.rbHead.position;
                // Fallback to pelvis/transform
                return GetTargetPosition(target);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get direction from a position toward the best target.
        /// Returns null if no target available.
        /// </summary>
        public static Vector3? GetDirectionToTarget(TargetFilter filter, Vector3 fromPosition)
        {
            return GetDirectionToTarget(filter, fromPosition, false);
        }

        /// <summary>
        /// Get direction from a position toward the best target (head or torso).
        /// </summary>
        public static Vector3? GetDirectionToTarget(TargetFilter filter, Vector3 fromPosition, bool targetHead)
        {
            var target = FindTarget(filter, fromPosition);
            if (target == null) return null;
            var pos = targetHead ? GetTargetHeadPosition(target) : GetTargetPosition(target);
            if (!pos.HasValue) return null;
            Vector3 dir = (pos.Value - fromPosition).normalized;
            return dir.sqrMagnitude > 0.001f ? dir : (Vector3?)null;
        }

        // ═══════════════════════════════════════════════════
        // BONEMENU — Search + Select to page
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Populate a BoneMenu page with players. Clicking a player selects them as lock-on target.
        /// Uses the same PlayerIDManager/NetworkPlayerManager pattern as TeleportController.
        /// </summary>
        public static void PlayerSearchToPage(Page resultsPage)
        {
            // Force full refresh
            _lastRefreshTime = 0f;
            RefreshPlayerList();

            try { resultsPage?.RemoveAll(); } catch { }

            // Also include NPC rigs as fallback
            var localRig = Player.RigManager;
            var head = Player.Head;
            Vector3 fromPos = head != null ? head.position : Vector3.zero;

            // Add Fusion players
            int count = 0;
            string searchLower = (_playerSearchQuery ?? "").ToLower();

            foreach (var player in _cachedPlayers)
            {
                if (!string.IsNullOrEmpty(searchLower) && !player.DisplayName.ToLower().Contains(searchLower))
                    continue;

                var capturedRig = player.Rig;
                var capturedName = player.DisplayName;
                float dist = 0f;
                if (capturedRig != null)
                {
                    var pos = GetTargetPosition(capturedRig);
                    if (pos.HasValue) dist = Vector3.Distance(fromPos, pos.Value);
                }

                resultsPage?.CreateFunction(
                    $"{capturedName} ({dist:0.0}m)",
                    Color.green,
                    () =>
                    {
                        SelectedPlayer = capturedRig;
                        NotificationHelper.Send(
                            BoneLib.Notifications.NotificationType.Success,
                            $"Target: {capturedName}", "DOOBER UTILS", 3f, true);
                    }
                );
                count++;
            }

            // If no Fusion players, add scene NPCs
            if (count == 0 && localRig != null)
            {
                try
                {
                    var allRigs = UnityEngine.Object.FindObjectsOfType<RigManager>();
                    int npcIdx = 0;
                    foreach (var rig in allRigs)
                    {
                        if (rig == null || rig == localRig) continue;
                        var pos = GetTargetPosition(rig);
                        float npcDist = pos.HasValue ? Vector3.Distance(fromPos, pos.Value) : 0f;
                        var capturedRig = rig;
                        string npcName = $"NPC {++npcIdx}";

                        resultsPage?.CreateFunction(
                            $"{npcName} ({npcDist:0.0}m)",
                            Color.yellow,
                            () =>
                            {
                                SelectedPlayer = capturedRig;
                                _selectedPlayerName = npcName; // override for NPCs
                                NotificationHelper.Send(
                                    BoneLib.Notifications.NotificationType.Success,
                                    $"Target: {npcName}", "DOOBER UTILS", 3f, true);
                            }
                        );
                        count++;
                    }
                }
                catch { }
            }

            if (count == 0)
            {
                resultsPage?.CreateFunction("No players found", Color.gray, () => { });
            }

            Main.MelonLog.Msg($"[PlayerTargeting] Found {count} target(s)");
        }

        // ═══════════════════════════════════════════════════
        // INTERNAL — Player list refresh via Fusion reflection
        // ═══════════════════════════════════════════════════

        private static void RefreshPlayerList()
        {
            if (Time.time - _lastRefreshTime < REFRESH_INTERVAL && _cachedPlayers.Count > 0)
                return;

            _lastRefreshTime = Time.time;
            _cachedPlayers.Clear();

            try
            {
                var playerIdManagerType = FindTypeByName("PlayerIDManager", "LabFusion.Player");
                if (playerIdManagerType == null) return;

                var playerIdsField = playerIdManagerType.GetField("PlayerIDs", BindingFlags.Public | BindingFlags.Static);
                if (playerIdsField == null) return;

                var playerIds = playerIdsField.GetValue(null) as System.Collections.IEnumerable;
                if (playerIds == null) return;

                var localSmallIdProp = playerIdManagerType.GetProperty("LocalSmallID", BindingFlags.Public | BindingFlags.Static);
                byte localSmallId = 255;
                if (localSmallIdProp != null)
                    localSmallId = (byte)localSmallIdProp.GetValue(null);

                foreach (var playerIdObj in playerIds)
                {
                    if (playerIdObj == null) continue;

                    var playerIdType = playerIdObj.GetType();
                    var smallIdProp = playerIdType.GetProperty("SmallID", BindingFlags.Public | BindingFlags.Instance);
                    if (smallIdProp == null) continue;
                    byte smallId = (byte)smallIdProp.GetValue(playerIdObj);

                    if (smallId == localSmallId) continue;

                    // Resolve display name (same multi-approach as TeleportController)
                    string displayName = $"Player {smallId}";
                    RigManager rig = null;

                    // Get NetworkPlayer via NetworkPlayerManager.TryGetPlayer(smallId)
                    try
                    {
                        var npmType = FindTypeByName("NetworkPlayerManager", "LabFusion.Entities");
                        if (npmType != null)
                        {
                            var tryGet = npmType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                                    && m.GetParameters()[0].ParameterType == typeof(byte));
                            if (tryGet != null)
                            {
                                var args = new object[] { smallId, null };
                                bool found = (bool)tryGet.Invoke(null, args);
                                if (found && args[1] != null)
                                {
                                    var networkPlayer = args[1];

                                    // Username
                                    var uProp = networkPlayer.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (uProp != null)
                                    {
                                        var uname = uProp.GetValue(networkPlayer) as string;
                                        if (!string.IsNullOrWhiteSpace(uname) && uname != "No Name")
                                            displayName = uname;
                                    }

                                    // RigManager via RigRefs
                                    try
                                    {
                                        var hasRigProp = networkPlayer.GetType().GetProperty("HasRig", BindingFlags.Public | BindingFlags.Instance);
                                        if (hasRigProp != null && (bool)hasRigProp.GetValue(networkPlayer))
                                        {
                                            var rigRefsProp = networkPlayer.GetType().GetProperty("RigRefs", BindingFlags.Public | BindingFlags.Instance);
                                            if (rigRefsProp != null)
                                            {
                                                var rigRefs = rigRefsProp.GetValue(networkPlayer);
                                                if (rigRefs != null)
                                                {
                                                    var rigMgrProp = rigRefs.GetType().GetProperty("RigManager", BindingFlags.Public | BindingFlags.Instance);
                                                    if (rigMgrProp != null)
                                                        rig = rigMgrProp.GetValue(rigRefs) as RigManager;
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }

                    // Fallback: Metadata nickname/username
                    if (displayName == $"Player {smallId}")
                    {
                        try
                        {
                            var metaProp = playerIdType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Instance);
                            if (metaProp != null)
                            {
                                var meta = metaProp.GetValue(playerIdObj);
                                if (meta != null)
                                {
                                    foreach (var propName in new[] { "Nickname", "Username" })
                                    {
                                        var p = meta.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                                        if (p == null) continue;
                                        var val = p.GetValue(meta);
                                        if (val == null) continue;
                                        var getVal = val.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .FirstOrDefault(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod);
                                        if (getVal == null) continue;
                                        var str = getVal.Invoke(val, null) as string;
                                        if (!string.IsNullOrWhiteSpace(str) && str != "No Name")
                                        {
                                            displayName = str;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    _cachedPlayers.Add(new PlayerEntry
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        Rig = rig,
                        Mass = rig != null ? GetRigMass(rig) : 1f
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Get valid players with distances from a position.
        /// Only returns players that have a loaded rig.
        /// </summary>
        private static List<(RigManager rig, float dist, float mass)> GetValidPlayers(Vector3 fromPos)
        {
            var result = new List<(RigManager, float, float)>();
            var localRig = Player.RigManager;

            // Fusion players with rigs
            foreach (var p in _cachedPlayers)
            {
                if (p.Rig == null || p.Rig == localRig) continue;
                var pos = GetTargetPosition(p.Rig);
                if (!pos.HasValue) continue;
                result.Add((p.Rig, Vector3.Distance(fromPos, pos.Value), p.Mass));
            }

            // Fallback: scene RigManagers (NPCs if no Fusion players)
            if (result.Count == 0 && localRig != null)
            {
                try
                {
                    var allRigs = UnityEngine.Object.FindObjectsOfType<RigManager>();
                    foreach (var rig in allRigs)
                    {
                        if (rig == null || rig == localRig) continue;
                        var pos = GetTargetPosition(rig);
                        if (!pos.HasValue) continue;
                        result.Add((rig, Vector3.Distance(fromPos, pos.Value), GetRigMass(rig)));
                    }
                }
                catch { }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        private static Type FindTypeByName(string typeName, string namespaceName = null)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName
                        && (namespaceName == null || t.Namespace == namespaceName));
            }
            catch { return null; }
        }

        private static float GetRigMass(RigManager rig)
        {
            try
            {
                var physRig = rig?.physicsRig;
                if (physRig == null) return 1f;
                float mass = 0f;
                var torso = physRig.torso;
                if (torso != null)
                {
                    if (torso.rbHead != null) mass += torso.rbHead.mass;
                    if (torso.rbChest != null) mass += torso.rbChest.mass;
                    if (torso.rbSpine != null) mass += torso.rbSpine.mass;
                    if (torso.rbPelvis != null) mass += torso.rbPelvis.mass;
                }
                return mass > 0f ? mass : 1f;
            }
            catch { return 1f; }
        }
    }
}
