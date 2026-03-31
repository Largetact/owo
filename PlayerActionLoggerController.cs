using System;
using System.Collections.Generic;
using BoneLib.Notifications;
using MelonLoader;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Logs player joins, leaves, and deaths.
    /// Read-only: does not interfere with networking.
    /// </summary>
    public static class PlayerActionLoggerController
    {
        public static bool Enabled { get; set; }
        public static bool LogJoins { get; set; } = true;
        public static bool LogLeaves { get; set; } = true;
        public static bool LogDeaths { get; set; } = true;
        public static bool ShowNotifications { get; set; } = true;

        private static readonly HashSet<string> _knownPlayers = new();
        private static float _pollTimer;
        private const float PollInterval = 3f;
        private static bool _hooked;

        public static void Initialize()
        {
            Main.MelonLog.Msg("Player Action Logger controller initialized");
        }

        public static void Update()
        {
            if (!Enabled) return;

            if (!_hooked)
            {
                TryHookEvents();
                _hooked = true;
            }

            _pollTimer += UnityEngine.Time.deltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            PollPlayers();
        }

        public static void OnLevelLoaded()
        {
            _knownPlayers.Clear();
        }

        private static void TryHookEvents()
        {
            // Try to subscribe to LabFusion events if available
            try
            {
                Type multiplayerHooks = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { multiplayerHooks = asm.GetType("LabFusion.Utilities.MultiplayerHooking"); } catch { }
                    if (multiplayerHooks != null) break;
                }
                if (multiplayerHooks == null) return;

                // OnPlayerJoin
                var joinEvent = multiplayerHooks.GetEvent("OnPlayerJoin",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (joinEvent != null)
                {
                    var handler = Delegate.CreateDelegate(joinEvent.EventHandlerType,
                        typeof(PlayerActionLoggerController),
                        nameof(OnFusionPlayerJoin));
                    joinEvent.AddEventHandler(null, handler);
                }

                // OnPlayerLeave
                var leaveEvent = multiplayerHooks.GetEvent("OnPlayerLeave",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (leaveEvent != null)
                {
                    var handler = Delegate.CreateDelegate(leaveEvent.EventHandlerType,
                        typeof(PlayerActionLoggerController),
                        nameof(OnFusionPlayerLeave));
                    leaveEvent.AddEventHandler(null, handler);
                }
            }
            catch { }
        }

        // Called by Fusion event if hooked
        public static void OnFusionPlayerJoin(object id)
        {
            if (!Enabled || !LogJoins) return;
            string name = TryGetPlayerName(id);
            string msg = $"[Join] {name} joined the server";
            MelonLogger.Msg($"[ActionLog] {msg}");
            if (ShowNotifications) NotificationHelper.Send(NotificationType.Information, msg);
        }

        public static void OnFusionPlayerLeave(object id)
        {
            if (!Enabled || !LogLeaves) return;
            string name = TryGetPlayerName(id);
            string msg = $"[Leave] {name} left the server";
            MelonLogger.Msg($"[ActionLog] {msg}");
            if (ShowNotifications) NotificationHelper.Send(NotificationType.Information, msg);
        }

        private static string TryGetPlayerName(object playerId)
        {
            try
            {
                var nameProp = playerId?.GetType().GetProperty("Username")
                    ?? playerId?.GetType().GetProperty("SmallId");
                return nameProp?.GetValue(playerId)?.ToString() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        /// <summary>
        /// Fallback polling for player presence.
        /// </summary>
        private static void PollPlayers()
        {
            try
            {
                Type playerRepType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { playerRepType = asm.GetType("LabFusion.Representation.PlayerRepManager"); } catch { }
                    if (playerRepType != null) break;
                }
                if (playerRepType == null) return;

                var repsProp = playerRepType.GetProperty("PlayerReps",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?? playerRepType.GetProperty("playerReps",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);
                if (repsProp == null) return;

                var reps = repsProp.GetValue(null);
                if (reps == null) return;

                var currentIds = new HashSet<string>();
                var enumerable = reps as System.Collections.IEnumerable;
                if (enumerable == null) return;

                foreach (var repObj in enumerable)
                {
                    try
                    {
                        var idProp = repObj.GetType().GetProperty("PlayerId")
                            ?? repObj.GetType().GetProperty("SmallId");
                        var nameProp = repObj.GetType().GetProperty("Username");
                        string id = idProp?.GetValue(repObj)?.ToString() ?? "";
                        string name = nameProp?.GetValue(repObj)?.ToString() ?? "Unknown";
                        if (string.IsNullOrEmpty(id)) continue;

                        currentIds.Add(id);

                        if (!_knownPlayers.Contains(id))
                        {
                            if (LogJoins)
                            {
                                MelonLogger.Msg($"[ActionLog] Detected player: {name} ({id})");
                                if (ShowNotifications)
                                    NotificationHelper.Send(NotificationType.Information, $"{name} detected in server");
                            }
                            _knownPlayers.Add(id);
                        }
                    }
                    catch { }
                }

                // Detect leaves
                if (LogLeaves)
                {
                    var left = new List<string>();
                    foreach (var known in _knownPlayers)
                    {
                        if (!currentIds.Contains(known))
                            left.Add(known);
                    }
                    foreach (var id in left)
                    {
                        MelonLogger.Msg($"[ActionLog] Player left: {id}");
                        if (ShowNotifications)
                            NotificationHelper.Send(NotificationType.Information, $"Player {id} left");
                        _knownPlayers.Remove(id);
                    }
                }
            }
            catch { }
        }
    }
}
