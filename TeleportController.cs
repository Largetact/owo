using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace BonelabUtilityMod
{
    public static class TeleportController
    {
        private static Vector3? savedPosition = null;
        private static Quaternion? savedRotation = null;
        private static List<SavedLocation> savedLocations = new List<SavedLocation>();
        private static int maxSavedLocations = 10;
        private static int currentPlayerIndex = 0;
        private static List<PlayerInfo> cachedPlayers = new List<PlayerInfo>();

        private struct SavedLocation
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public struct PlayerInfo
        {
            public byte SmallID;
            public string DisplayName;
            public object PlayerIDObj; // The actual PlayerID object from LabFusion
        }

        public static bool HasSavedPosition => savedPosition.HasValue;
        public static string SavedPositionText => savedPosition.HasValue
            ? $"({savedPosition.Value.x:F1}, {savedPosition.Value.y:F1}, {savedPosition.Value.z:F1})"
            : "None";

        public static string CurrentPlayerName
        {
            get
            {
                if (cachedPlayers.Count == 0)
                    return "No Players";
                if (currentPlayerIndex >= cachedPlayers.Count)
                    currentPlayerIndex = 0;
                return cachedPlayers[currentPlayerIndex].DisplayName;
            }
        }

        public static int PlayerCount => cachedPlayers.Count;

        public static IReadOnlyList<PlayerInfo> GetCachedPlayers() => cachedPlayers;

        public static void Initialize()
        {
            Main.MelonLog.Msg("Teleport controller initialized");
        }

        public static void SaveCurrentPosition()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null)
                {
                    Main.MelonLog.Warning("Cannot save position: RigManager not found");
                    SendNotification("Failed", "RigManager not found");
                    return;
                }

                // Get the player's physical position (pelvis/root position)
                var physicsRig = rigManager.physicsRig;
                if (physicsRig != null && physicsRig.m_pelvis != null)
                {
                    savedPosition = physicsRig.m_pelvis.position;
                    savedRotation = physicsRig.m_pelvis.rotation;
                }
                else
                {
                    // Fallback to RigManager transform
                    savedPosition = rigManager.transform.position;
                    savedRotation = rigManager.transform.rotation;
                }

                Main.MelonLog.Msg($"Position saved: {SavedPositionText}");
                SendNotification("Position Saved", SavedPositionText);
            }
            catch (System.Exception ex)
            {
                Main.MelonLog.Error($"Failed to save position: {ex.Message}");
                SendNotification("Error", "Failed to save position");
            }
        }

        public static void TeleportToSavedPosition()
        {
            try
            {
                if (!savedPosition.HasValue)
                {
                    Main.MelonLog.Warning("No saved position to teleport to");
                    SendNotification("No Position", "Save a position first");
                    return;
                }

                var rigManager = Player.RigManager;
                if (rigManager == null)
                {
                    Main.MelonLog.Warning("Cannot teleport: RigManager not found");
                    SendNotification("Failed", "RigManager not found");
                    return;
                }

                // Teleport the player using position and forward vector
                Vector3 forward = savedRotation.HasValue ? savedRotation.Value * Vector3.forward : Vector3.forward;
                rigManager.Teleport(savedPosition.Value, forward);

                Main.MelonLog.Msg($"Teleported to: {SavedPositionText}");
                SendNotification("Teleported", SavedPositionText);
            }
            catch (System.Exception ex)
            {
                Main.MelonLog.Error($"Failed to teleport: {ex.Message}");
                SendNotification("Error", "Failed to teleport");
            }
        }

        public static void ClearSavedPosition()
        {
            savedPosition = null;
            savedRotation = null;
            Main.MelonLog.Msg("Saved position cleared");
            SendNotification("Cleared", "Position cleared");
        }

        // ===== Player Teleport Functions =====

        public static void RefreshPlayerList()
        {
            cachedPlayers.Clear();
            currentPlayerIndex = 0;

            try
            {
                // Find PlayerIDManager type from LabFusion
                var playerIdManagerType = FindTypeByName("PlayerIDManager", "LabFusion.Player");
                if (playerIdManagerType == null)
                {
                    Main.MelonLog.Warning("LabFusion PlayerIDManager not found - multiplayer not active?");
                    SendNotification("No Players", "Not in multiplayer");
                    return;
                }

                // Get PlayerIDs HashSet
                var playerIdsField = playerIdManagerType.GetField("PlayerIDs", BindingFlags.Public | BindingFlags.Static);
                if (playerIdsField == null)
                {
                    Main.MelonLog.Warning("PlayerIDs field not found");
                    return;
                }

                var playerIds = playerIdsField.GetValue(null) as System.Collections.IEnumerable;
                if (playerIds == null)
                {
                    Main.MelonLog.Warning("PlayerIDs is null");
                    return;
                }

                // Get LocalSmallID to filter out self
                var localSmallIdProp = playerIdManagerType.GetProperty("LocalSmallID", BindingFlags.Public | BindingFlags.Static);
                byte localSmallId = 255;
                if (localSmallIdProp != null)
                {
                    localSmallId = (byte)localSmallIdProp.GetValue(null);
                }

                foreach (var playerIdObj in playerIds)
                {
                    if (playerIdObj == null) continue;

                    var playerIdType = playerIdObj.GetType();

                    // Get SmallID
                    var smallIdProp = playerIdType.GetProperty("SmallID", BindingFlags.Public | BindingFlags.Instance);
                    if (smallIdProp == null) continue;
                    byte smallId = (byte)smallIdProp.GetValue(playerIdObj);

                    // Skip self
                    if (smallId == localSmallId) continue;

                    // Try to get display name - multiple approaches
                    string displayName = $"Player {smallId}";

                    // Approach 1: Try to get from NetworkPlayer.Username directly
                    try
                    {
                        var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager", "LabFusion.Entities");
                        if (networkPlayerManagerType != null)
                        {
                            var tryGetPlayerMethod = networkPlayerManagerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                                    && m.GetParameters()[0].ParameterType == typeof(byte));

                            if (tryGetPlayerMethod != null)
                            {
                                var args = new object[] { smallId, null };
                                bool found = (bool)tryGetPlayerMethod.Invoke(null, args);
                                if (found && args[1] != null)
                                {
                                    var networkPlayer = args[1];
                                    var usernameProp = networkPlayer.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (usernameProp != null)
                                    {
                                        var username = usernameProp.GetValue(networkPlayer) as string;
                                        if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                                        {
                                            displayName = username;
                                            Main.MelonLog.Msg($"Got username from NetworkPlayer: {displayName}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"NetworkPlayer username lookup failed: {ex.Message}");
                    }

                    // Approach 2: Try PlayerID.Metadata if NetworkPlayer didn't work
                    if (displayName == $"Player {smallId}")
                    {
                        try
                        {
                            var metadataProp = playerIdType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Instance);
                            if (metadataProp != null)
                            {
                                var metadata = metadataProp.GetValue(playerIdObj);
                                if (metadata != null)
                                {
                                    var metadataType = metadata.GetType();

                                    // Try Username property
                                    var usernameProp = metadataType.GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (usernameProp != null)
                                    {
                                        var usernameMetadata = usernameProp.GetValue(metadata);
                                        if (usernameMetadata != null)
                                        {
                                            // GetValue() with no parameters returns string
                                            var getValueMethods = usernameMetadata.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod)
                                                .ToArray();

                                            if (getValueMethods.Length > 0)
                                            {
                                                var username = getValueMethods[0].Invoke(usernameMetadata, null) as string;
                                                if (!string.IsNullOrWhiteSpace(username))
                                                {
                                                    displayName = username;
                                                    Main.MelonLog.Msg($"Got username from Metadata: {displayName}");
                                                }
                                            }
                                        }
                                    }

                                    // Also check Nickname
                                    var nicknameProp = metadataType.GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                                    if (nicknameProp != null)
                                    {
                                        var nicknameMetadata = nicknameProp.GetValue(metadata);
                                        if (nicknameMetadata != null)
                                        {
                                            var getValueMethods = nicknameMetadata.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod)
                                                .ToArray();

                                            if (getValueMethods.Length > 0)
                                            {
                                                var nickname = getValueMethods[0].Invoke(nicknameMetadata, null) as string;
                                                if (!string.IsNullOrWhiteSpace(nickname))
                                                {
                                                    displayName = nickname;
                                                    Main.MelonLog.Msg($"Got nickname from Metadata: {displayName}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Main.MelonLog.Warning($"Metadata username lookup failed for player {smallId}: {ex.Message}");
                        }
                    }

                    cachedPlayers.Add(new PlayerInfo
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        PlayerIDObj = playerIdObj
                    });

                    Main.MelonLog.Msg($"Added player: SmallID={smallId}, DisplayName={displayName}");
                }

                Main.MelonLog.Msg($"Found {cachedPlayers.Count} other player(s)");
                SendNotification("Players Found", $"{cachedPlayers.Count} player(s)");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to refresh player list: {ex.Message}");
                SendNotification("Error", "Failed to get players");
            }
        }

        public static void NextPlayer()
        {
            if (cachedPlayers.Count == 0)
            {
                RefreshPlayerList();
                return;
            }

            currentPlayerIndex = (currentPlayerIndex + 1) % cachedPlayers.Count;
            SendNotification("Selected", CurrentPlayerName);
        }

        public static void PreviousPlayer()
        {
            if (cachedPlayers.Count == 0)
            {
                RefreshPlayerList();
                return;
            }

            currentPlayerIndex--;
            if (currentPlayerIndex < 0)
                currentPlayerIndex = cachedPlayers.Count - 1;
            SendNotification("Selected", CurrentPlayerName);
        }

        public static void TeleportToSelectedPlayer()
        {
            if (cachedPlayers.Count == 0)
            {
                SendNotification("No Players", "Refresh player list first");
                return;
            }

            if (currentPlayerIndex >= cachedPlayers.Count)
                currentPlayerIndex = 0;

            var targetPlayer = cachedPlayers[currentPlayerIndex];
            TeleportToPlayer(targetPlayer);
        }

        private static void TeleportToPlayer(PlayerInfo targetPlayer)
        {
            try
            {
                // Get NetworkPlayerManager
                var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager", "LabFusion.Entities");
                if (networkPlayerManagerType == null)
                {
                    Main.MelonLog.Warning("NetworkPlayerManager not found");
                    SendNotification("Error", "NetworkPlayerManager not found");
                    return;
                }

                // Call TryGetPlayer(byte playerID, out NetworkPlayer player)
                var tryGetPlayerMethod = networkPlayerManagerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(byte));

                if (tryGetPlayerMethod == null)
                {
                    Main.MelonLog.Warning("TryGetPlayer method not found");
                    SendNotification("Error", "TryGetPlayer not found");
                    return;
                }

                var args = new object[] { targetPlayer.SmallID, null };
                bool found = (bool)tryGetPlayerMethod.Invoke(null, args);

                if (!found || args[1] == null)
                {
                    Main.MelonLog.Warning($"Could not get NetworkPlayer for {targetPlayer.DisplayName}");
                    SendNotification("Error", $"Player not found: {targetPlayer.DisplayName}");
                    return;
                }

                var networkPlayer = args[1];
                var networkPlayerType = networkPlayer.GetType();

                // Check HasRig
                var hasRigProp = networkPlayerType.GetProperty("HasRig", BindingFlags.Public | BindingFlags.Instance);
                if (hasRigProp != null && !(bool)hasRigProp.GetValue(networkPlayer))
                {
                    Main.MelonLog.Warning($"Player {targetPlayer.DisplayName} has no rig loaded");
                    SendNotification("Error", $"{targetPlayer.DisplayName} not loaded");
                    return;
                }

                // Get RigRefs
                var rigRefsProp = networkPlayerType.GetProperty("RigRefs", BindingFlags.Public | BindingFlags.Instance);
                if (rigRefsProp == null)
                {
                    Main.MelonLog.Warning("RigRefs property not found");
                    SendNotification("Error", "RigRefs not found");
                    return;
                }

                var rigRefs = rigRefsProp.GetValue(networkPlayer);
                if (rigRefs == null)
                {
                    Main.MelonLog.Warning("RigRefs is null");
                    SendNotification("Error", "RigRefs is null");
                    return;
                }

                // Get RigManager from RigRefs
                var rigManagerProp = rigRefs.GetType().GetProperty("RigManager", BindingFlags.Public | BindingFlags.Instance);
                if (rigManagerProp == null)
                {
                    Main.MelonLog.Warning("RigManager property not found on RigRefs");
                    SendNotification("Error", "RigManager not found");
                    return;
                }

                var targetRigManager = rigManagerProp.GetValue(rigRefs);
                if (targetRigManager == null)
                {
                    Main.MelonLog.Warning("Target RigManager is null");
                    SendNotification("Error", "Target RigManager null");
                    return;
                }

                // Try to get the Head transform from RigRefs for accurate position
                Vector3 targetPos = Vector3.zero;
                Vector3 targetForward = Vector3.forward;

                var headProp = rigRefs.GetType().GetProperty("Head", BindingFlags.Public | BindingFlags.Instance);
                if (headProp != null)
                {
                    var headTransform = headProp.GetValue(rigRefs) as Transform;
                    if (headTransform != null)
                    {
                        targetPos = headTransform.position;
                        targetForward = headTransform.forward;
                        Main.MelonLog.Msg($"Got player head position: {targetPos}");
                    }
                }

                // Fallback to RigManager transform if Head not available
                if (targetPos == Vector3.zero)
                {
                    var targetTransform = (targetRigManager as Component)?.transform;
                    if (targetTransform == null)
                    {
                        Main.MelonLog.Warning("Could not get target transform");
                        SendNotification("Error", "No transform");
                        return;
                    }
                    targetPos = targetTransform.position;
                    targetForward = targetTransform.forward;
                    Main.MelonLog.Msg($"Using RigManager position: {targetPos}");
                }

                // Teleport our player
                var myRigManager = Player.RigManager;
                if (myRigManager == null)
                {
                    Main.MelonLog.Warning("Local RigManager not found");
                    SendNotification("Error", "Local RigManager not found");
                    return;
                }

                // Offset slightly so we don't spawn inside them, and adjust for head height
                Vector3 spawnPos = targetPos + targetForward * 1.5f;
                spawnPos.y = targetPos.y; // Keep same height level
                myRigManager.Teleport(spawnPos, -targetForward);

                Main.MelonLog.Msg($"Teleported to player: {targetPlayer.DisplayName}");
                SendNotification("Teleported", $"To: {targetPlayer.DisplayName}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to teleport to player: {ex.Message}\n{ex.StackTrace}");
                SendNotification("Error", "Teleport failed");
            }
        }

        private static Type FindTypeByName(string typeName, string namespaceName)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName &&
                        (string.IsNullOrEmpty(namespaceName) || t.Namespace == namespaceName));
            }
            catch
            {
                return null;
            }
        }

        // ===== Player Search (FusionProtector style) =====

        private static string _playerSearchQuery = "";
        private static BoneLib.BoneMenu.Page _playerResultsPage = null;

        public static string PlayerSearchQuery
        {
            get => _playerSearchQuery;
            set => _playerSearchQuery = value ?? "";
        }

        /// <summary>
        /// Populate a BoneMenu page with player search results
        /// Clicking a player name teleports to them
        /// </summary>
        public static void PlayerSearchToPage(BoneLib.BoneMenu.Page resultsPage)
        {
            _playerResultsPage = resultsPage;

            // Refresh player list first
            RefreshPlayerListInternal();

            // Clear page
            try { _playerResultsPage?.RemoveAll(); } catch { }

            if (cachedPlayers.Count == 0)
            {
                SendNotification("No Players", "Not in multiplayer or no other players");
                return;
            }

            string searchLower = (_playerSearchQuery ?? "").ToLower();
            int resultCount = 0;

            foreach (var player in cachedPlayers)
            {
                // Filter by search query if provided
                if (!string.IsNullOrEmpty(searchLower))
                {
                    if (!player.DisplayName.ToLower().Contains(searchLower))
                        continue;
                }

                // Capture for closure
                byte capturedSmallId = player.SmallID;
                string capturedName = player.DisplayName;

                _playerResultsPage?.CreateFunction(capturedName, Color.green, () =>
                {
                    TeleportToPlayerBySmallID(capturedSmallId, capturedName);
                });
                resultCount++;
            }

            Main.MelonLog.Msg($"[Player Search] Found {resultCount} player(s)");
            SendNotification("Players Found", $"{resultCount} player(s)");
        }

        /// <summary>
        /// Internal refresh (no notification spam)
        /// </summary>
        private static void RefreshPlayerListInternal()
        {
            cachedPlayers.Clear();
            currentPlayerIndex = 0;

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
                {
                    localSmallId = (byte)localSmallIdProp.GetValue(null);
                }

                foreach (var playerIdObj in playerIds)
                {
                    if (playerIdObj == null) continue;

                    var playerIdType = playerIdObj.GetType();
                    var smallIdProp = playerIdType.GetProperty("SmallID", BindingFlags.Public | BindingFlags.Instance);
                    if (smallIdProp == null) continue;
                    byte smallId = (byte)smallIdProp.GetValue(playerIdObj);

                    if (smallId == localSmallId) continue;

                    string displayName = GetPlayerDisplayName(playerIdObj, smallId);

                    cachedPlayers.Add(new PlayerInfo
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        PlayerIDObj = playerIdObj
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"RefreshPlayerListInternal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get display name for a player
        /// </summary>
        private static string GetPlayerDisplayName(object playerIdObj, byte smallId)
        {
            string displayName = $"Player {smallId}";

            try
            {
                var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager", "LabFusion.Entities");
                if (networkPlayerManagerType != null)
                {
                    var tryGetPlayerMethod = networkPlayerManagerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                            && m.GetParameters()[0].ParameterType == typeof(byte));

                    if (tryGetPlayerMethod != null)
                    {
                        var args = new object[] { smallId, null };
                        bool found = (bool)tryGetPlayerMethod.Invoke(null, args);
                        if (found && args[1] != null)
                        {
                            var networkPlayer = args[1];
                            var usernameProp = networkPlayer.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                            if (usernameProp != null)
                            {
                                var username = usernameProp.GetValue(networkPlayer) as string;
                                if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                                {
                                    displayName = username;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return displayName;
        }

        /// <summary>
        /// Teleport to a specific player by SmallID
        /// </summary>
        public static void TeleportToPlayerBySmallID(byte smallId, string playerName)
        {
            // Find the player in cache or create temp PlayerInfo
            PlayerInfo? targetPlayer = null;

            foreach (var p in cachedPlayers)
            {
                if (p.SmallID == smallId)
                {
                    targetPlayer = p;
                    break;
                }
            }

            if (!targetPlayer.HasValue)
            {
                // Create a temp PlayerInfo
                targetPlayer = new PlayerInfo
                {
                    SmallID = smallId,
                    DisplayName = playerName,
                    PlayerIDObj = null
                };
            }

            TeleportToPlayer(targetPlayer.Value);
        }

        private static void SendNotification(string title, string message)
        {
            NotificationHelper.Send(NotificationType.Information, message);
        }
    }
}
