using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Stare at Player controller — continuously rotates the local player's rig
    /// to face a selected target player. Works in VR by rotating the play-space.
    /// </summary>
    public static class StareAtPlayerController
    {
        private static bool _enabled = false;
        private static float _turnSpeed = 5f;
        private static RigManager _targetRig = null;
        private static string _targetName = "None";
        private static string _playerSearchQuery = "";

        // Cached player list (refreshed on search)
        private static List<PlayerEntry> _cachedPlayers = new List<PlayerEntry>();

        public struct PlayerEntry
        {
            public byte SmallID;
            public string DisplayName;
            public RigManager Rig;
        }

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Main.MelonLog.Msg($"Stare at Player: {(value ? "ON" : "OFF")}");
            }
        }

        public static float TurnSpeed
        {
            get => _turnSpeed;
            set => _turnSpeed = Mathf.Clamp(value, 1f, 30f);
        }

        public static string TargetName => _targetName;

        public static string PlayerSearchQuery
        {
            get => _playerSearchQuery;
            set => _playerSearchQuery = value ?? "";
        }

        public static void Initialize()
        {
            _targetRig = null;
            _targetName = "None";
            Main.MelonLog.Msg("StareAtPlayerController initialized");
        }

        public static void Update()
        {
            if (!_enabled || _targetRig == null) return;

            try
            {
                // Validate target is still alive
                var targetComp = _targetRig as UnityEngine.Component;
                if (targetComp == null || targetComp.gameObject == null)
                {
                    _targetRig = null;
                    _targetName = "None";
                    return;
                }

                // Get target position (head)
                Vector3? targetPos = PlayerTargeting.GetTargetHeadPosition(_targetRig);
                if (!targetPos.HasValue) return;

                var head = Player.Head;
                if (head == null) return;

                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                Vector3 headPos = head.position;
                Vector3 dir = targetPos.Value - headPos;
                if (dir.sqrMagnitude < 0.01f) return;

                // ── Yaw (horizontal rotation) ──
                Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
                if (flatDir.sqrMagnitude < 0.001f) return;

                Vector3 currentForward = head.forward;
                currentForward.y = 0;
                currentForward.Normalize();
                if (currentForward.sqrMagnitude < 0.001f) return;

                float yawAngle = Vector3.SignedAngle(currentForward, flatDir.normalized, Vector3.up);

                var rigTransform = ((Component)rigManager).transform;

                if (Mathf.Abs(yawAngle) > 1f)
                {
                    float rotAmount = yawAngle * Time.deltaTime * _turnSpeed;
                    rigTransform.RotateAround(headPos, Vector3.up, rotAmount);
                }

                // ── Pitch (vertical rotation) ──
                float currentPitch = Mathf.Asin(Mathf.Clamp(head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                float targetPitch = Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float pitchDelta = targetPitch - currentPitch;

                if (Mathf.Abs(pitchDelta) > 0.5f)
                {
                    float pitchAmount = pitchDelta * Time.deltaTime * _turnSpeed;
                    Vector3 rightAxis = Vector3.Cross(Vector3.up, flatDir.normalized).normalized;
                    if (rightAxis.sqrMagnitude > 0.001f)
                    {
                        rigTransform.RotateAround(headPos, rightAxis, pitchAmount);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Populate a BoneMenu page with players to select as stare target
        /// </summary>
        public static void PlayerSearchToPage(Page resultsPage)
        {
            RefreshPlayerList();

            try { resultsPage?.RemoveAll(); } catch { }

            var localRig = Player.RigManager;
            var head = Player.Head;
            Vector3 fromPos = head != null ? head.position : Vector3.zero;

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
                    var pos = PlayerTargeting.GetTargetPosition(capturedRig);
                    if (pos.HasValue) dist = Vector3.Distance(fromPos, pos.Value);
                }

                resultsPage?.CreateFunction(
                    $"{capturedName} ({dist:0.0}m)",
                    Color.green,
                    () =>
                    {
                        _targetRig = capturedRig;
                        _targetName = capturedName;
                        Main.MelonLog.Msg($"[Stare] Target set: {capturedName}");
                        NotificationHelper.Send(
                            BoneLib.Notifications.NotificationType.Success,
                            $"Staring at: {capturedName}");
                    }
                );
                count++;
            }

            // If no Fusion players, add scene NPCs/RigManagers
            if (count == 0 && localRig != null)
            {
                try
                {
                    var allRigs = UnityEngine.Object.FindObjectsOfType<RigManager>();
                    int npcIdx = 0;
                    foreach (var rig in allRigs)
                    {
                        if (rig == null || rig == localRig) continue;
                        var pos = PlayerTargeting.GetTargetPosition(rig);
                        float npcDist = pos.HasValue ? Vector3.Distance(fromPos, pos.Value) : 0f;
                        var capturedRig = rig;
                        string npcName = $"NPC {++npcIdx}";

                        resultsPage?.CreateFunction(
                            $"{npcName} ({npcDist:0.0}m)",
                            Color.yellow,
                            () =>
                            {
                                _targetRig = capturedRig;
                                _targetName = npcName;
                                Main.MelonLog.Msg($"[Stare] Target set: {npcName}");
                                NotificationHelper.Send(
                                    BoneLib.Notifications.NotificationType.Success,
                                    $"Staring at: {npcName}");
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

            Main.MelonLog.Msg($"[Stare] Found {count} target(s)");
        }

        private static void RefreshPlayerList()
        {
            _cachedPlayers.Clear();

            try
            {
                var playerIdManagerType = FindTypeByName("PlayerIDManager");
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

                    if (smallId == localSmallId) continue; // Skip self

                    string displayName = $"Player {smallId}";
                    try
                    {
                        var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager");
                        if (networkPlayerManagerType != null)
                        {
                            var tryGetMethod = networkPlayerManagerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                                    && m.GetParameters()[0].ParameterType == typeof(byte));
                            if (tryGetMethod != null)
                            {
                                var args = new object[] { smallId, null };
                                bool found = (bool)tryGetMethod.Invoke(null, args);
                                if (found && args[1] != null)
                                {
                                    var np = args[1];
                                    var npType = np.GetType();

                                    // Get username
                                    var usernameProp = npType.GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (usernameProp != null)
                                    {
                                        var username = usernameProp.GetValue(np) as string;
                                        if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                                            displayName = username;
                                    }

                                    // Get RigManager
                                    var hasRigProp = npType.GetProperty("HasRig", BindingFlags.Public | BindingFlags.Instance);
                                    if (hasRigProp != null && (bool)hasRigProp.GetValue(np))
                                    {
                                        var rigRefsProp = npType.GetProperty("RigRefs", BindingFlags.Public | BindingFlags.Instance);
                                        if (rigRefsProp != null)
                                        {
                                            var rigRefs = rigRefsProp.GetValue(np);
                                            if (rigRefs != null)
                                            {
                                                var rigManagerProp = rigRefs.GetType().GetProperty("RigManager", BindingFlags.Public | BindingFlags.Instance);
                                                if (rigManagerProp != null)
                                                {
                                                    var rm = rigManagerProp.GetValue(rigRefs);
                                                    if (rm is RigManager rigManager)
                                                    {
                                                        _cachedPlayers.Add(new PlayerEntry
                                                        {
                                                            SmallID = smallId,
                                                            DisplayName = displayName,
                                                            Rig = rigManager
                                                        });
                                                        continue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // Player found but no rig — still list them
                    _cachedPlayers.Add(new PlayerEntry
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        Rig = null
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Stare] RefreshPlayerList error: {ex.Message}");
            }
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName) return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
