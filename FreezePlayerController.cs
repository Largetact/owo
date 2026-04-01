using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Freeze Player controller — freezes other players' rigs.
    /// Local: overrides bone transforms every LateUpdate (after Fusion sync).
    /// Network (host only): periodically teleports target back to frozen position.
    /// </summary>
    public static class FreezePlayerController
    {
        public struct PlayerEntry
        {
            public byte SmallID;
            public string DisplayName;
            public RigManager Rig;
        }

        private struct FrozenPlayer
        {
            public byte SmallID;
            public string DisplayName;
            public RigManager Rig;
            public Rigidbody[] Rigidbodies;
            public Transform[] Transforms;
            public Vector3[] SavedPositions;
            public Quaternion[] SavedRotations;
            public Vector3 FreezeWorldPosition;
            public float LastTeleportTime;
        }

        private static List<PlayerEntry> _cachedPlayers = new List<PlayerEntry>();
        private static Dictionary<byte, FrozenPlayer> _frozenPlayers = new Dictionary<byte, FrozenPlayer>();
        private static string _playerSearchQuery = "";
        private const float TeleportInterval = 0.05f;

        public static string PlayerSearchQuery
        {
            get => _playerSearchQuery;
            set => _playerSearchQuery = value ?? "";
        }

        public static IReadOnlyList<PlayerEntry> CachedPlayers => _cachedPlayers;
        public static bool IsFrozen(byte smallId) => _frozenPlayers.ContainsKey(smallId);

        public static void RefreshPlayers()
        {
            RefreshPlayerList();
        }

        /// <summary>
        /// Toggle freeze on a specific player by SmallID.
        /// </summary>
        public static void ToggleFreeze(byte smallId, string displayName, RigManager rig)
        {
            if (_frozenPlayers.ContainsKey(smallId))
            {
                UnfreezePlayer(smallId);
            }
            else
            {
                FreezePlayer(smallId, displayName, rig);
            }
        }

        private static void FreezePlayer(byte smallId, string displayName, RigManager rig)
        {
            if (rig == null)
            {
                NotificationHelper.Send(NotificationType.Warning, $"{displayName}: no rig loaded");
                return;
            }

            try
            {
                var rigGo = rig.gameObject;
                var rigidbodies = rigGo.GetComponentsInChildren<Rigidbody>();

                // Save all bone transforms for local override
                var transforms = new Transform[rigidbodies.Length];
                var positions = new Vector3[rigidbodies.Length];
                var rotations = new Quaternion[rigidbodies.Length];

                for (int i = 0; i < rigidbodies.Length; i++)
                {
                    if (rigidbodies[i] == null) continue;
                    transforms[i] = rigidbodies[i].transform;
                    positions[i] = transforms[i].position;
                    rotations[i] = transforms[i].rotation;
                    rigidbodies[i].velocity = Vector3.zero;
                    rigidbodies[i].angularVelocity = Vector3.zero;
                    rigidbodies[i].isKinematic = true;
                }

                // Save world position for network teleport (host only)
                Vector3 freezePos = rig.transform.position;
                try
                {
                    if (rig.physicsRig != null && rig.physicsRig.m_pelvis != null)
                        freezePos = rig.physicsRig.m_pelvis.position;
                }
                catch { }

                _frozenPlayers[smallId] = new FrozenPlayer
                {
                    SmallID = smallId,
                    DisplayName = displayName,
                    Rig = rig,
                    Rigidbodies = rigidbodies,
                    Transforms = transforms,
                    SavedPositions = positions,
                    SavedRotations = rotations,
                    FreezeWorldPosition = freezePos,
                    LastTeleportTime = 0f
                };

                bool isHost = false;
                try { isHost = NetworkInfo.IsHost; } catch { }
                string hostNote = isHost ? " [net-synced]" : " [local only - need host]";
                Main.MelonLog.Msg($"[Freeze] Froze {displayName} (ID={smallId}, {rigidbodies.Length} rbs){hostNote}");
                NotificationHelper.Send(NotificationType.Success, $"Froze: {displayName}{hostNote}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[Freeze] Error freezing {displayName}: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Freeze failed: {displayName}");
            }
        }

        private static void UnfreezePlayer(byte smallId)
        {
            if (!_frozenPlayers.TryGetValue(smallId, out var frozen))
                return;

            try
            {
                if (frozen.Rigidbodies != null)
                {
                    foreach (var rb in frozen.Rigidbodies)
                    {
                        if (rb == null) continue;
                        rb.isKinematic = false;
                    }
                }

                Main.MelonLog.Msg($"[Freeze] Unfroze {frozen.DisplayName} (ID={smallId})");
                NotificationHelper.Send(NotificationType.Success, $"Unfroze: {frozen.DisplayName}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Freeze] Unfreeze error for {frozen.DisplayName}: {ex.Message}");
            }

            _frozenPlayers.Remove(smallId);
        }

        public static void UnfreezeAll()
        {
            var ids = _frozenPlayers.Keys.ToList();
            foreach (var id in ids)
                UnfreezePlayer(id);

            NotificationHelper.Send(NotificationType.Success, "All players unfrozen");
        }

        /// <summary>
        /// Per-frame: re-apply kinematic + zero velocity, and if host,
        /// periodically teleport the target back to their frozen position.
        /// </summary>
        public static void Update()
        {
            if (_frozenPlayers.Count == 0) return;

            var toRemove = new List<byte>();
            float now = Time.time;

            // Copy keys to avoid modification during iteration
            var keys = _frozenPlayers.Keys.ToList();

            foreach (var key in keys)
            {
                if (!_frozenPlayers.TryGetValue(key, out var frozen)) continue;

                if (frozen.Rig == null)
                {
                    toRemove.Add(key);
                    continue;
                }

                try
                {
                    // Re-apply kinematic + zero velocity
                    if (frozen.Rigidbodies != null)
                    {
                        foreach (var rb in frozen.Rigidbodies)
                        {
                            if (rb == null) continue;
                            rb.isKinematic = true;
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }

                    // Network freeze (host only): teleport target back to frozen position
                    // PlayerRepTeleportMessage is ClientsOnly — server blocks relay from non-host.
                    // When we ARE host, SendFromServer goes direct and bypasses server handler.
                    if (now - frozen.LastTeleportTime >= TeleportInterval)
                    {
                        bool isHost = false;
                        try { isHost = NetworkInfo.IsHost; } catch { }
                        if (isHost)
                        {
                            try
                            {
                                MessageRelay.RelayNative(
                                    new PlayerRepTeleportData { Position = frozen.FreezeWorldPosition },
                                    NativeMessageTag.PlayerRepTeleport,
                                    new MessageRoute(frozen.SmallID, NetworkChannel.Reliable));
                            }
                            catch (Exception ex)
                            {
                                Main.MelonLog.Warning($"[Freeze] Teleport send failed: {ex.Message}");
                            }
                        }
                        frozen.LastTeleportTime = now;
                        _frozenPlayers[key] = frozen;
                    }
                }
                catch
                {
                    toRemove.Add(key);
                }
            }

            foreach (var id in toRemove)
                _frozenPlayers.Remove(id);
        }

        /// <summary>
        /// Called in OnLateUpdate — override bone transforms AFTER Fusion sync
        /// to keep the player frozen on our screen.
        /// </summary>
        public static void LateUpdate()
        {
            if (_frozenPlayers.Count == 0) return;

            foreach (var kvp in _frozenPlayers)
            {
                var frozen = kvp.Value;
                if (frozen.Rig == null || frozen.Transforms == null) continue;

                try
                {
                    for (int i = 0; i < frozen.Transforms.Length; i++)
                    {
                        if (frozen.Transforms[i] == null) continue;
                        frozen.Transforms[i].position = frozen.SavedPositions[i];
                        frozen.Transforms[i].rotation = frozen.SavedRotations[i];
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Populate a BoneMenu page with players to freeze/unfreeze.
        /// </summary>
        public static void PlayerSearchToPage(Page resultsPage)
        {
            RefreshPlayerList();

            try { resultsPage?.RemoveAll(); } catch { }

            string searchLower = (_playerSearchQuery ?? "").ToLower();
            int count = 0;

            foreach (var player in _cachedPlayers)
            {
                if (!string.IsNullOrEmpty(searchLower) && !player.DisplayName.ToLower().Contains(searchLower))
                    continue;

                var capturedSmallId = player.SmallID;
                var capturedRig = player.Rig;
                var capturedName = player.DisplayName;
                bool isFrozen = _frozenPlayers.ContainsKey(capturedSmallId);

                resultsPage?.CreateFunction(
                    $"{(isFrozen ? "[FROZEN] " : "")}{capturedName}",
                    isFrozen ? Color.cyan : Color.green,
                    () => ToggleFreeze(capturedSmallId, capturedName, capturedRig)
                );
                count++;
            }

            if (count == 0)
                resultsPage?.CreateFunction("No players found", Color.gray, () => { });

            Main.MelonLog.Msg($"[Freeze] Found {count} player(s)");
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

                    if (smallId == localSmallId) continue;

                    string displayName = $"Player {smallId}";
                    RigManager rigManager = null;

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

                                    var usernameProp = npType.GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (usernameProp != null)
                                    {
                                        var username = usernameProp.GetValue(np) as string;
                                        if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                                            displayName = username;
                                    }

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
                                                    rigManager = rigManagerProp.GetValue(rigRefs) as RigManager;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    _cachedPlayers.Add(new PlayerEntry
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        Rig = rigManager
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Freeze] RefreshPlayerList error: {ex.Message}");
            }
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }
    }
}
