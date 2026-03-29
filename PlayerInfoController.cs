using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
using LabFusion.Player;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Player Info - Displays player SteamIDs and detects username spoofing.
    /// A player is flagged as a potential spoofer if their Fusion username
    /// differs from a previously seen username for the same SteamID.
    /// </summary>
    public static class PlayerInfoController
    {
        public struct PlayerEntry
        {
            public byte SmallID;
            public ulong SteamID;
            public string Username;
            public bool IsLocal;
            public bool IsSuspectedSpoof;
        }

        private static List<PlayerEntry> _players = new List<PlayerEntry>();
        private static float _lastRefreshTime = 0f;
        private static float _refreshInterval = 2f;

        // Tracks first-seen username per SteamID for spoof detection
        private static Dictionary<ulong, string> _knownNames = new Dictionary<ulong, string>();
        // Tracks SteamIDs we've already notified about to avoid spam
        private static HashSet<ulong> _notifiedSpoofs = new HashSet<ulong>();

        public static IReadOnlyList<PlayerEntry> Players => _players;

        public static void Initialize()
        {
            _knownNames.Clear();
            _players.Clear();
        }

        /// <summary>
        /// Call from OnUpdate to periodically refresh the player list.
        /// </summary>
        public static void Update()
        {
            float now = Time.time;
            if (now - _lastRefreshTime < _refreshInterval) return;
            _lastRefreshTime = now;

            RefreshPlayerList();
        }

        /// <summary>
        /// Force a refresh of the player list now (for overlay button use).
        /// </summary>
        public static void ForceRefresh()
        {
            RefreshPlayerList();
        }

        private static void RefreshPlayerList()
        {
            _players.Clear();

            try
            {
                if (!NetworkInfo.HasServer) return;

                foreach (var pid in PlayerIDManager.PlayerIDs)
                {
                    if (pid == null || !pid.IsValid) continue;

                    string username = "";
                    try
                    {
                        username = pid.Metadata?.Username?.GetValue() ?? "";
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(username))
                        username = $"Player{pid.SmallID}";

                    ulong steamId = pid.PlatformID;
                    bool isSuspectedSpoof = false;

                    // Spoof detection: track first username per SteamID
                    if (steamId != 0 && !string.IsNullOrWhiteSpace(username) && username != $"Player{pid.SmallID}")
                    {
                        if (_knownNames.TryGetValue(steamId, out string knownName))
                        {
                            // If we've seen this SteamID before with a different name, flag it
                            if (!string.Equals(knownName, username, StringComparison.OrdinalIgnoreCase))
                            {
                                isSuspectedSpoof = true;
                                Main.MelonLog.Warning($"[PlayerInfo] SPOOF? SteamID {steamId} was '{knownName}', now '{username}'");
                                if (_notifiedSpoofs.Add(steamId))
                                {
                                    NotificationHelper.Send(
                                        BoneLib.Notifications.NotificationType.Warning,
                                        $"SPOOF DETECTED: '{username}' was previously '{knownName}' (SteamID: {steamId})");
                                }
                            }
                        }
                        else
                        {
                            _knownNames[steamId] = username;
                        }
                    }

                    _players.Add(new PlayerEntry
                    {
                        SmallID = pid.SmallID,
                        SteamID = steamId,
                        Username = username,
                        IsLocal = pid.IsMe,
                        IsSuspectedSpoof = isSuspectedSpoof
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[PlayerInfo] Refresh error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset known name tracking (e.g., on level change or new server join).
        /// </summary>
        public static void ResetKnownNames()
        {
            _knownNames.Clear();
            _notifiedSpoofs.Clear();
        }
    }
}
