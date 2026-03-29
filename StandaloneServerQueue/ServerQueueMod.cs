using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

[assembly: MelonInfo(typeof(StandaloneServerQueue.ServerQueueMod), "Standalone Server Queue", "1.0.0", "DOOBER")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace StandaloneServerQueue
{
    public class ServerQueueMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        private static MelonPreferences_Category _prefs;
        private static MelonPreferences_Entry<bool> _prefEnabled;
        private static MelonPreferences_Entry<float> _prefPollInterval;
        private static MelonPreferences_Entry<string> _prefLastCode;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            _prefs = MelonPreferences.CreateCategory("StandaloneServerQueue");
            _prefEnabled = _prefs.CreateEntry("Enabled", true);
            _prefPollInterval = _prefs.CreateEntry("PollInterval", 10f);
            _prefLastCode = _prefs.CreateEntry("LastServerCode", "");

            ServerQueue.Enabled = _prefEnabled.Value;
            ServerQueue.PollInterval = _prefPollInterval.Value;
            ServerQueue.LastServerCode = _prefLastCode.Value;

            Hooking.OnLevelLoaded += (info) => SetupBoneMenu();

            // Apply Harmony patches + Fusion event hooks on first level load
            // (LabFusion needs to be loaded first)
            bool patchedOnce = false;
            Hooking.OnLevelLoaded += (info) =>
            {
                if (!patchedOnce)
                {
                    patchedOnce = true;
                    ServerQueue.ApplyPatches();
                }
            };

            Log.Msg("Standalone Server Queue initialized");
        }

        public override void OnApplicationQuit()
        {
            SavePrefs();
        }

        private void SavePrefs()
        {
            _prefEnabled.Value = ServerQueue.Enabled;
            _prefPollInterval.Value = ServerQueue.PollInterval;
            _prefLastCode.Value = ServerQueue.LastServerCode ?? "";
            _prefs.SaveToFile(false);
        }

        private static bool _menuCreated = false;

        private void SetupBoneMenu()
        {
            if (_menuCreated) return;
            _menuCreated = true;

            var page = Page.Root.CreatePage("Server Queue", Color.magenta);

            page.CreateBool(
                "Auto-Queue on Full",
                Color.green,
                ServerQueue.Enabled,
                (value) => { ServerQueue.Enabled = value; }
            );

            page.CreateFloat(
                "Poll Interval (s)",
                Color.cyan,
                ServerQueue.PollInterval,
                5f,
                5f,
                60f,
                (value) => { ServerQueue.PollInterval = value; }
            );

            page.CreateFunction(
                "Start Queue (last code)",
                Color.green,
                () => ServerQueue.StartQueueForCode(ServerQueue.LastServerCode)
            );

            page.CreateFunction(
                "Stop Queue",
                Color.red,
                ServerQueue.StopQueue
            );
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ServerQueue — all queue logic, self-contained
    // ═══════════════════════════════════════════════════════════════

    internal static class ServerQueue
    {
        private static bool _enabled = true;
        private static bool _isInQueue = false;
        private static bool _isAttemptingJoin = false;
        private static string _lastServerCode = "";
        private static float _pollInterval = 10f;
        private static object _pollCoroutine = null;
        private static int _attemptCount = 0;

        // Pending join tracking
        private static string _pendingJoinCode = null;
        private static float _pendingJoinTime = 0f;
        private static bool _joinSucceeded = false;
        private static ulong _pendingLobbyId = 0;
        private const float PENDING_JOIN_WINDOW = 15f;

        // Steamworks reflection cache
        private static bool _steamworksCacheBuilt = false;
        private static Type _steamLobbyType;
        private static FieldInfo _lobbyIdField;
        private static MethodInfo _lobbyGetDataMethod;

        // Fusion reflection cache
        private static bool _typesResolved = false;
        private static bool _eventsSubscribed = false;
        private static bool _harmonyApplied = false;
        private static Type _networkHelperType;
        private static Type _networkLayerManagerType;
        private static Type _callbackInfoType;
        private static MethodInfo _joinByCodeMethod;
        private static Delegate _lobbyCallback;

        private static PropertyInfo _layerProp;
        private static PropertyInfo _matchmakerProp;
        private static MethodInfo _requestLobbiesMethod;

        // Lobby ID-based rejoin
        private static ulong _queueLobbyId = 0;
        private static MethodInfo _joinServerMethod;

        // ─── Public Properties ───

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!_enabled && _isInQueue)
                    StopQueue();
            }
        }

        public static bool IsInQueue => _isInQueue;

        public static string LastServerCode
        {
            get => _lastServerCode;
            set { if (!string.IsNullOrEmpty(value)) _lastServerCode = value; }
        }

        public static int AttemptCount => _attemptCount;

        public static float PollInterval
        {
            get => _pollInterval;
            set => _pollInterval = Math.Max(5f, value);
        }

        public static string StatusText
        {
            get
            {
                if (!_isInQueue) return "Not queued";
                return $"Queued: {_lastServerCode} (attempt #{_attemptCount})";
            }
        }

        // ─── Initialization ───

        public static void ApplyPatches()
        {
            ResolveTypes();
            SubscribeToFusionEvents();
            ApplyHarmonyPatches();
        }

        private static void ResolveTypes()
        {
            if (_typesResolved) return;
            _typesResolved = true;

            try
            {
                _networkHelperType = FindType("NetworkHelper");
                _networkLayerManagerType = FindType("NetworkLayerManager");

                if (_networkHelperType == null)
                {
                    ServerQueueMod.Log.Warning("[Queue] NetworkHelper not found — LabFusion not loaded?");
                    return;
                }

                _joinByCodeMethod = _networkHelperType.GetMethod("JoinServerByCode",
                    BindingFlags.Public | BindingFlags.Static);

                if (_networkLayerManagerType != null)
                {
                    _layerProp = _networkLayerManagerType.GetProperty("Layer",
                        BindingFlags.Public | BindingFlags.Static);
                }

                var matchmakerInterface = FindType("IMatchmaker");
                if (matchmakerInterface != null)
                {
                    _callbackInfoType = matchmakerInterface.GetNestedType("MatchmakerCallbackInfo");
                    BuildLobbyCallback();
                }

                ServerQueueMod.Log.Msg("[Queue] Types resolved");
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Error($"[Queue] ResolveTypes failed: {ex.Message}");
            }
        }

        // ─── Harmony Patches ───

        private static void ApplyHarmonyPatches()
        {
            if (_harmonyApplied) return;
            _harmonyApplied = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("StandaloneServerQueue");

                // Patch JoinServerByCode — capture code joins
                if (_networkHelperType != null)
                {
                    var target = _networkHelperType.GetMethod("JoinServerByCode", BindingFlags.Public | BindingFlags.Static);
                    var prefix = typeof(ServerQueue).GetMethod(nameof(JoinByCodePrefix), BindingFlags.NonPublic | BindingFlags.Static);
                    if (target != null && prefix != null)
                    {
                        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                        ServerQueueMod.Log.Msg("[Queue] Patched NetworkHelper.JoinServerByCode");
                    }
                }

                // Patch SteamNetworkLayer.JoinServer — capture browser joins
                var steamLayerType = FindType("SteamNetworkLayer");
                if (steamLayerType != null)
                {
                    var target = steamLayerType.GetMethod("JoinServer", BindingFlags.Public | BindingFlags.Instance);
                    var prefix = typeof(ServerQueue).GetMethod(nameof(JoinServerPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                    if (target != null && prefix != null)
                    {
                        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                        ServerQueueMod.Log.Msg("[Queue] Patched SteamNetworkLayer.JoinServer");
                    }
                }

                // Patch InternalServerHelpers.OnDisconnect — detect "server full"
                var helpersType = FindType("InternalServerHelpers");
                if (helpersType != null)
                {
                    var target = helpersType.GetMethod("OnDisconnect", BindingFlags.Public | BindingFlags.Static);
                    var prefix = typeof(ServerQueue).GetMethod(nameof(OnDisconnectPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                    if (target != null && prefix != null)
                    {
                        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                        ServerQueueMod.Log.Msg("[Queue] Patched InternalServerHelpers.OnDisconnect");
                    }
                }
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] Harmony patches failed: {ex.Message}");
            }
        }

        // ─── Harmony Prefix Methods ───

        private static void JoinByCodePrefix(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            if (_isInQueue && code == _lastServerCode) return;

            _pendingJoinCode = code;
            _pendingJoinTime = Time.time;
            _joinSucceeded = false;
            ServerQueueMod.Log.Msg($"[Queue] Code join detected: {code}");
        }

        private static void JoinServerPrefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0) return;
                if (!string.IsNullOrEmpty(_pendingJoinCode) && Time.time - _pendingJoinTime < 5f) return;
                if (_isInQueue) return;

                ulong lobbyId = ExtractUlongFromSteamId(__args[0]);
                if (lobbyId == 0) return;

                _pendingLobbyId = lobbyId;
                _pendingJoinTime = Time.time;
                _joinSucceeded = false;

                string code = GetLobbyCode(lobbyId);
                if (!string.IsNullOrEmpty(code))
                {
                    _pendingJoinCode = code;
                    ServerQueueMod.Log.Msg($"[Queue] Browse join — lobby {lobbyId}, code: {code}");
                }
                else
                {
                    _pendingJoinCode = null;
                    ServerQueueMod.Log.Msg($"[Queue] Browse join — lobby {lobbyId}, no code");
                }
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] JoinServerPrefix error: {ex.Message}");
            }
        }

        private static void OnDisconnectPrefix(string reason)
        {
            try
            {
                _isAttemptingJoin = false;
                if (!_enabled || _joinSucceeded) return;
                if (Time.time - _pendingJoinTime > PENDING_JOIN_WINDOW) return;

                bool isFull = !string.IsNullOrEmpty(reason) &&
                    (reason.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     reason.IndexOf("ran out of space", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isFull)
                {
                    _pendingJoinCode = null;
                    _pendingLobbyId = 0;
                    return;
                }

                string code = _pendingJoinCode;
                if (string.IsNullOrEmpty(code) && _pendingLobbyId != 0)
                {
                    code = GetLobbyCode(_pendingLobbyId);
                }

                ulong lobbyId = _pendingLobbyId;
                _pendingJoinCode = null;
                _pendingLobbyId = 0;

                if (string.IsNullOrEmpty(code) && lobbyId != 0)
                {
                    ServerQueueMod.Log.Msg($"[Queue] Server full! Queuing by lobby ID: {lobbyId}");
                    if (_isInQueue) StopQueue();
                    _queueLobbyId = lobbyId;
                    _lastServerCode = $"lobby:{lobbyId}";
                    StartQueue();
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    ServerQueueMod.Log.Msg($"[Queue] Server full! Queuing for code: {code}");
                    if (_isInQueue && _lastServerCode == code) return;
                    if (_isInQueue) StopQueue();
                    _queueLobbyId = 0;
                    _lastServerCode = code;
                    StartQueue();
                }
                else
                {
                    Notify(NotificationType.Warning, "Server full — enter code manually to queue");
                }
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] OnDisconnectPrefix error: {ex.Message}");
            }
        }

        // ─── Event Subscriptions ───

        private static void SubscribeToFusionEvents()
        {
            if (_eventsSubscribed) return;
            _eventsSubscribed = true;

            try
            {
                var hookType = FindType("MultiplayerHooking");
                if (hookType == null) return;

                SubscribeEvent(hookType, "OnJoinedServer", nameof(OnJoinedServer));
                SubscribeEvent(hookType, "OnDisconnected", nameof(OnDisconnected));
                ServerQueueMod.Log.Msg("[Queue] Fusion event subscriptions complete");
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] Event subscription failed: {ex.Message}");
            }
        }

        private static void SubscribeEvent(Type hookType, string eventName, string handlerName)
        {
            try
            {
                var evt = hookType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (evt == null) return;
                var handler = Delegate.CreateDelegate(evt.EventHandlerType,
                    typeof(ServerQueue).GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Static));
                evt.AddEventHandler(null, handler);
            }
            catch { }
        }

        private static void BuildLobbyCallback()
        {
            try
            {
                if (_callbackInfoType == null) return;
                var param = Expression.Parameter(_callbackInfoType, "info");
                var boxed = Expression.Convert(param, typeof(object));
                var method = typeof(ServerQueue).GetMethod(nameof(HandleLobbyResult), BindingFlags.NonPublic | BindingFlags.Static);
                var call = Expression.Call(method, boxed);
                var actionType = typeof(Action<>).MakeGenericType(_callbackInfoType);
                _lobbyCallback = Expression.Lambda(actionType, call, param).Compile();
            }
            catch { }
        }

        // ─── Event Handlers ───

        private static void OnJoinedServer()
        {
            _joinSucceeded = true;
            _pendingJoinCode = null;

            if (_isInQueue)
            {
                ServerQueueMod.Log.Msg("[Queue] Joined! Queue complete.");
                Notify(NotificationType.Success, $"Queue complete! Joined after {_attemptCount} attempt(s)");
                _isInQueue = false;
                _isAttemptingJoin = false;
                _queueLobbyId = 0;
                StopCoroutine();
            }
        }

        private static void OnDisconnected()
        {
            _isAttemptingJoin = false;
        }

        // ─── Queue Management ───

        public static void StartQueueForCode(string code)
        {
            if (!_enabled)
            {
                Notify(NotificationType.Warning, "Server Queue is disabled");
                return;
            }
            if (string.IsNullOrEmpty(code))
            {
                Notify(NotificationType.Warning, "Enter a server code first");
                return;
            }
            if (_isInQueue) StopQueue();
            _lastServerCode = code;
            _queueLobbyId = 0;
            StartQueue();
        }

        private static void StartQueue()
        {
            _isInQueue = true;
            _isAttemptingJoin = false;
            _attemptCount = 0;
            Notify(NotificationType.Information, $"Queued for: {_lastServerCode}\nPolling every {_pollInterval}s");
            _pollCoroutine = MelonCoroutines.Start(PollCoroutine());
        }

        public static void StopQueue()
        {
            if (_isInQueue)
            {
                _isInQueue = false;
                _isAttemptingJoin = false;
                _queueLobbyId = 0;
                Notify(NotificationType.Information, "Queue stopped");
            }
            StopCoroutine();
        }

        private static void StopCoroutine()
        {
            if (_pollCoroutine != null)
            {
                MelonCoroutines.Stop(_pollCoroutine);
                _pollCoroutine = null;
            }
        }

        private static IEnumerator PollCoroutine()
        {
            yield return new WaitForSecondsRealtime(3f);
            while (_isInQueue)
            {
                if (!_isAttemptingJoin)
                {
                    _attemptCount++;
                    CheckAndJoin();
                }
                yield return new WaitForSecondsRealtime(_pollInterval);
            }
        }

        private static void CheckAndJoin()
        {
            if (_isAttemptingJoin) return;
            _isAttemptingJoin = true;

            try
            {
                if (_queueLobbyId != 0)
                {
                    JoinByLobbyId(_queueLobbyId);
                    return;
                }

                if (_layerProp == null) { StopQueue(); return; }
                var layer = _layerProp.GetValue(null);
                if (layer == null) { StopQueue(); return; }

                if (_matchmakerProp == null)
                    _matchmakerProp = layer.GetType().GetProperty("Matchmaker", BindingFlags.Public | BindingFlags.Instance);

                object mm = _matchmakerProp?.GetValue(layer);
                if (mm == null)
                {
                    var field = layer.GetType().GetField("_matchmaker", BindingFlags.NonPublic | BindingFlags.Instance);
                    mm = field?.GetValue(layer);
                }
                if (mm == null) { _isAttemptingJoin = false; return; }

                InvokeRequestLobbies(mm);
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] CheckAndJoin error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        private static void InvokeRequestLobbies(object matchmaker)
        {
            try
            {
                if (_requestLobbiesMethod == null)
                    _requestLobbiesMethod = matchmaker.GetType().GetMethod("RequestLobbiesByCode", BindingFlags.Public | BindingFlags.Instance);

                if (_requestLobbiesMethod == null || _lobbyCallback == null)
                {
                    _isAttemptingJoin = false;
                    return;
                }

                _requestLobbiesMethod.Invoke(matchmaker, new object[] { _lastServerCode, _lobbyCallback });
                ServerQueueMod.Log.Msg($"[Queue] Checking {_lastServerCode} (attempt #{_attemptCount})...");
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] RequestLobbies error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        private static void JoinByLobbyId(ulong lobbyId)
        {
            try
            {
                ServerQueueMod.Log.Msg($"[Queue] Direct lobby join: {lobbyId} (attempt #{_attemptCount})");

                if (_layerProp == null) { _isAttemptingJoin = false; StopQueue(); return; }
                var layer = _layerProp.GetValue(null);
                if (layer == null) { _isAttemptingJoin = false; StopQueue(); return; }

                if (_joinServerMethod == null)
                    _joinServerMethod = layer.GetType().GetMethod("JoinServer", BindingFlags.Public | BindingFlags.Instance);

                if (_joinServerMethod == null) { _isAttemptingJoin = false; StopQueue(); return; }

                var paramType = _joinServerMethod.GetParameters()[0].ParameterType;
                object steamIdArg;

                if (paramType == typeof(ulong))
                {
                    steamIdArg = lobbyId;
                }
                else
                {
                    steamIdArg = Activator.CreateInstance(paramType);
                    var valueField = paramType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                    if (valueField != null)
                    {
                        steamIdArg = Activator.CreateInstance(paramType);
                        valueField.SetValue(steamIdArg, lobbyId);
                    }
                    else
                    {
                        var op = paramType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ulong) }, null);
                        if (op != null) steamIdArg = op.Invoke(null, new object[] { lobbyId });
                    }
                }

                _joinServerMethod.Invoke(layer, new object[] { steamIdArg });
                _isAttemptingJoin = false;
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] JoinByLobbyId error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        private static void HandleLobbyResult(object callbackInfo)
        {
            try
            {
                var lobbiesField = callbackInfo.GetType().GetField("Lobbies");
                var lobbies = lobbiesField?.GetValue(callbackInfo) as Array;

                if (lobbies == null || lobbies.Length == 0)
                {
                    Notify(NotificationType.Warning, "Server not found — stopping queue");
                    _isAttemptingJoin = false;
                    StopQueue();
                    return;
                }

                var firstLobby = lobbies.GetValue(0);
                var metadata = firstLobby.GetType().GetField("Metadata")?.GetValue(firstLobby);
                if (metadata == null) { _isAttemptingJoin = false; return; }

                var lobbyInfo = metadata.GetType().GetProperty("LobbyInfo")?.GetValue(metadata);
                if (lobbyInfo == null) { _isAttemptingJoin = false; return; }

                int playerCount = (int)(lobbyInfo.GetType().GetProperty("PlayerCount")?.GetValue(lobbyInfo) ?? 0);
                int maxPlayers = (int)(lobbyInfo.GetType().GetProperty("MaxPlayers")?.GetValue(lobbyInfo) ?? 0);

                ServerQueueMod.Log.Msg($"[Queue] Server: {playerCount}/{maxPlayers}");

                if (playerCount < maxPlayers)
                {
                    Notify(NotificationType.Success, $"Slot open ({playerCount}/{maxPlayers})! Joining...");
                    _isAttemptingJoin = false;
                    _joinByCodeMethod?.Invoke(null, new object[] { _lastServerCode });
                }
                else
                {
                    Notify(NotificationType.Information, $"Still full ({playerCount}/{maxPlayers}) — retrying in {_pollInterval}s");
                    _isAttemptingJoin = false;
                }
            }
            catch (Exception ex)
            {
                ServerQueueMod.Log.Warning($"[Queue] HandleLobbyResult error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        // ─── Helpers ───

        private static ulong ExtractUlongFromSteamId(object obj)
        {
            if (obj == null) return 0;
            if (obj is ulong ul) return ul;
            try
            {
                var f = obj.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return (ulong)f.GetValue(obj);
                return Convert.ToUInt64(obj);
            }
            catch { return 0; }
        }

        private static void BuildSteamworksCache()
        {
            if (_steamworksCacheBuilt) return;
            _steamworksCacheBuilt = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { var t = asm.GetType("Steamworks.Data.Lobby"); if (t != null) { _steamLobbyType = t; break; } } catch { }
                }
                if (_steamLobbyType != null)
                {
                    _lobbyIdField = _steamLobbyType.GetField("Id", BindingFlags.Public | BindingFlags.Instance);
                    _lobbyGetDataMethod = _steamLobbyType.GetMethod("GetData", new[] { typeof(string) });
                }
            }
            catch { }
        }

        private static string GetLobbyCode(ulong lobbyId)
        {
            try
            {
                BuildSteamworksCache();
                if (_steamLobbyType == null || _lobbyIdField == null || _lobbyGetDataMethod == null) return null;

                object lobby = Activator.CreateInstance(_steamLobbyType);
                var fieldType = _lobbyIdField.FieldType;
                if (fieldType == typeof(ulong))
                {
                    _lobbyIdField.SetValue(lobby, lobbyId);
                }
                else
                {
                    object steamId = Activator.CreateInstance(fieldType);
                    var vf = fieldType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                    if (vf != null) vf.SetValue(steamId, lobbyId);
                    else
                    {
                        var op = fieldType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ulong) }, null);
                        if (op != null) steamId = op.Invoke(null, new object[] { lobbyId });
                    }
                    _lobbyIdField.SetValue(lobby, steamId);
                }

                string code = _lobbyGetDataMethod.Invoke(lobby, new object[] { "LobbyCode" }) as string;
                return string.IsNullOrWhiteSpace(code) ? null : code;
            }
            catch { return null; }
        }

        private static Type FindType(string name)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == name);
            }
            catch { return null; }
        }

        private static void Notify(NotificationType type, string message)
        {
            try
            {
                Notifier.Send(new Notification
                {
                    Title = "Server Queue",
                    Message = message,
                    Type = type,
                    PopupLength = 1.5f,
                    ShowTitleOnPopup = false
                });
            }
            catch { }
        }
    }
}
