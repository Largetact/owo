using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using BoneLib;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Lobby Browser — queries LabFusion's IMatchmaker.RequestLobbies() to list available servers.
    /// Uses expression trees to build the typed callback delegate at runtime.
    /// </summary>
    public static class LobbyBrowserController
    {
        public static List<LobbyEntry> CachedLobbies = new List<LobbyEntry>();
        public static string StatusText = "No lobbies cached (Refresh first)";
        private static float _lastRefresh = 0f;
        private const float MIN_REFRESH_INTERVAL = 5f;
        private static bool _isRefreshing = false;

        // Reflection caches
        private static bool _reflectionBuilt;
        private static PropertyInfo _layerProp;
        private static MethodInfo _requestLobbiesMethod;
        private static MethodInfo _joinByCodeMethod;
        private static FieldInfo _lobbiesField;         // MatchmakerCallbackInfo.Lobbies
        private static FieldInfo _lobbyMetadataField;   // LobbyInfo.Metadata
        private static PropertyInfo _metaLobbyCodeProp; // LobbyMetadataInfo.LobbyCode
        private static PropertyInfo _metaLobbyInfoProp; // LobbyMetadataInfo.LobbyInfo (data class)

        // LobbyInfo data class properties
        private static PropertyInfo _liLobbyName;
        private static PropertyInfo _liHostName;
        private static PropertyInfo _liPlayerCount;
        private static PropertyInfo _liMaxPlayers;
        private static PropertyInfo _liLevelTitle;
        private static PropertyInfo _liLobbyCode;

        // Pre-built typed callback delegate
        private static Delegate _typedCallback;

        public struct LobbyEntry
        {
            public string LobbyName;
            public string HostName;
            public int PlayerCount;
            public int MaxPlayers;
            public string LevelTitle;
            public string LobbyCode;
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Lobby Browser controller initialized");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        private static bool BuildReflectionCache()
        {
            if (_reflectionBuilt) return _requestLobbiesMethod != null;
            _reflectionBuilt = true;

            try
            {
                var nlmType = FindType("LabFusion.Network.NetworkLayerManager");
                if (nlmType == null) { Log("NetworkLayerManager not found"); return false; }

                _layerProp = nlmType.GetProperty("Layer", BindingFlags.Public | BindingFlags.Static);

                var iMatchmakerType = FindType("LabFusion.Network.IMatchmaker");
                if (iMatchmakerType == null) { Log("IMatchmaker not found"); return false; }

                // Nested structs
                var callbackInfoType = iMatchmakerType.GetNestedType("MatchmakerCallbackInfo");
                if (callbackInfoType == null) { Log("MatchmakerCallbackInfo not found"); return false; }

                _lobbiesField = callbackInfoType.GetField("Lobbies");

                var lobbyInfoStructType = iMatchmakerType.GetNestedType("LobbyInfo");
                if (lobbyInfoStructType != null)
                    _lobbyMetadataField = lobbyInfoStructType.GetField("Metadata");

                // LobbyMetadataInfo
                var metaType = FindType("LabFusion.Network.LobbyMetadataInfo");
                if (metaType != null)
                {
                    _metaLobbyCodeProp = metaType.GetProperty("LobbyCode");
                    _metaLobbyInfoProp = metaType.GetProperty("LobbyInfo");
                }

                // LobbyInfo data class properties (from LobbyMetadataInfo.LobbyInfo property type)
                if (_metaLobbyInfoProp != null)
                {
                    var liType = _metaLobbyInfoProp.PropertyType;
                    _liLobbyName = liType.GetProperty("LobbyName");
                    _liHostName = liType.GetProperty("LobbyHostName");
                    _liPlayerCount = liType.GetProperty("PlayerCount");
                    _liMaxPlayers = liType.GetProperty("MaxPlayers");
                    _liLevelTitle = liType.GetProperty("LevelTitle");
                    _liLobbyCode = liType.GetProperty("LobbyCode");
                }

                // NetworkHelper.JoinServerByCode
                var nhType = FindType("LabFusion.Network.NetworkHelper");
                if (nhType != null)
                    _joinByCodeMethod = nhType.GetMethod("JoinServerByCode", BindingFlags.Public | BindingFlags.Static);

                // Find RequestLobbies(Action<MatchmakerCallbackInfo>)
                foreach (var m in iMatchmakerType.GetMethods())
                {
                    if (m.Name == "RequestLobbies" && m.GetParameters().Length == 1)
                    {
                        var pType = m.GetParameters()[0].ParameterType;
                        if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(Action<>))
                        {
                            _requestLobbiesMethod = m;
                            break;
                        }
                    }
                }

                if (_requestLobbiesMethod == null) { Log("RequestLobbies method not found"); return false; }

                // Build typed delegate: Action<MatchmakerCallbackInfo> that calls our handler
                // using expression trees to bridge the type gap
                var actionType = typeof(Action<>).MakeGenericType(callbackInfoType);
                var param = Expression.Parameter(callbackInfoType, "info");
                // Box the struct to object, then call our static handler
                var handler = typeof(LobbyBrowserController).GetMethod(nameof(OnLobbiesReceived),
                    BindingFlags.NonPublic | BindingFlags.Static);
                var call = Expression.Call(handler, Expression.Convert(param, typeof(object)));
                _typedCallback = Expression.Lambda(actionType, call, param).Compile();

                Log($"Reflection OK — RequestLobbies: found, JoinByCode: {(_joinByCodeMethod != null ? "found" : "MISS")}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Reflection error: {ex.Message}");
                return false;
            }
        }

        public static void RefreshLobbies()
        {
            if (Time.time - _lastRefresh < MIN_REFRESH_INTERVAL) return;
            if (_isRefreshing) return;
            _lastRefresh = Time.time;
            _isRefreshing = true;
            StatusText = "Refreshing...";

            if (!BuildReflectionCache())
            {
                StatusText = "LabFusion matchmaker not available";
                _isRefreshing = false;
                return;
            }

            try
            {
                var layer = _layerProp?.GetValue(null);
                if (layer == null)
                {
                    StatusText = "Not connected to Fusion";
                    _isRefreshing = false;
                    return;
                }

                var matchmakerProp = layer.GetType().GetProperty("Matchmaker",
                    BindingFlags.Public | BindingFlags.Instance);
                var matchmaker = matchmakerProp?.GetValue(layer);
                if (matchmaker == null)
                {
                    StatusText = "Matchmaker is null (Fusion not started?)";
                    _isRefreshing = false;
                    return;
                }

                _requestLobbiesMethod.Invoke(matchmaker, new object[] { _typedCallback });
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                Log($"RefreshLobbies error: {ex.Message}");
                _isRefreshing = false;
            }
        }

        private static void OnLobbiesReceived(object callbackInfo)
        {
            try
            {
                var newLobbies = new List<LobbyEntry>();
                var lobbiesArray = _lobbiesField?.GetValue(callbackInfo) as Array;

                if (lobbiesArray == null || lobbiesArray.Length == 0)
                {
                    CachedLobbies = newLobbies;
                    StatusText = "No lobbies found";
                    _isRefreshing = false;
                    return;
                }

                foreach (var lobbyObj in lobbiesArray)
                {
                    try
                    {
                        var metadata = _lobbyMetadataField?.GetValue(lobbyObj);
                        if (metadata == null) continue;

                        string lobbyCode = _metaLobbyCodeProp?.GetValue(metadata)?.ToString() ?? "";

                        var lobbyData = _metaLobbyInfoProp?.GetValue(metadata);
                        if (lobbyData == null) continue;

                        string name = _liLobbyName?.GetValue(lobbyData)?.ToString() ?? "Unknown";
                        string host = _liHostName?.GetValue(lobbyData)?.ToString() ?? "Unknown";
                        string level = _liLevelTitle?.GetValue(lobbyData)?.ToString() ?? "";
                        int players = 0, max = 0;
                        try { players = Convert.ToInt32(_liPlayerCount?.GetValue(lobbyData) ?? 0); } catch { }
                        try { max = Convert.ToInt32(_liMaxPlayers?.GetValue(lobbyData) ?? 0); } catch { }

                        if (string.IsNullOrEmpty(lobbyCode))
                            lobbyCode = _liLobbyCode?.GetValue(lobbyData)?.ToString() ?? "";
                        if (string.IsNullOrEmpty(lobbyCode)) continue;

                        newLobbies.Add(new LobbyEntry
                        {
                            LobbyName = name,
                            HostName = host,
                            PlayerCount = players,
                            MaxPlayers = max,
                            LevelTitle = level,
                            LobbyCode = lobbyCode
                        });
                    }
                    catch { }
                }

                CachedLobbies = newLobbies;
                StatusText = $"Found {newLobbies.Count} lobbies";
                Log($"Cached {newLobbies.Count} lobbies");
            }
            catch (Exception ex)
            {
                StatusText = $"Parse error: {ex.Message}";
                Log($"OnLobbiesReceived error: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public static void JoinLobby(string lobbyCode)
        {
            if (string.IsNullOrEmpty(lobbyCode))
            {
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning, "No lobby code");
                return;
            }

            BuildReflectionCache();

            try
            {
                if (_joinByCodeMethod != null)
                {
                    _joinByCodeMethod.Invoke(null, new object[] { lobbyCode });
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Joining lobby...");
                }
                else
                {
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error, "Join method not found");
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error, $"Join failed: {ex.Message}");
            }
        }

        private static void Log(string msg) => Main.MelonLog.Msg($"[LobbyBrowser] {msg}");
    }
}
