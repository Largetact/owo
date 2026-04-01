using MelonLoader;
using UnityEngine;
using BoneLib.Notifications;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Auto-queue system: when you join a full server (via Fusion browser or code),
    /// it automatically detects the "Server is full" disconnect and starts polling
    /// to rejoin when a slot opens. Also supports manual queue start from BoneMenu.
    /// </summary>
    public static class ServerQueueController
    {
        private static bool _enabled = true;
        private static bool _isInQueue = false;
        private static bool _isAttemptingJoin = false;
        private static string _lastServerCode = "";
        private static float _pollInterval = 10f;
        private static object _pollCoroutine = null;
        private static int _attemptCount = 0;

        // Pending join tracking — detect "server full" disconnects
        private static string _pendingJoinCode = null;
        private static float _pendingJoinTime = 0f;
        private static bool _joinSucceeded = false;
        private static ulong _pendingLobbyId = 0;
        private const float PENDING_JOIN_WINDOW = 15f; // seconds to wait for full-denial

        // Steamworks reflection cache for lobby code lookup
        private static bool _steamworksCacheBuilt = false;
        private static Type _steamLobbyType;        // Steamworks.Data.Lobby
        private static FieldInfo _lobbyIdField;     // Lobby.Id
        private static MethodInfo _lobbyGetDataMethod; // Lobby.GetData(string)

        // One-time reflection cache
        private static bool _typesResolved = false;
        private static bool _eventsSubscribed = false;
        private static bool _harmonyApplied = false;
        private static Type _networkHelperType;
        private static Type _networkLayerManagerType;
        private static Type _callbackInfoType;
        private static MethodInfo _joinByCodeMethod;
        private static Delegate _lobbyCallback;

        // Cached matchmaker access
        private static PropertyInfo _layerProp;
        private static PropertyInfo _matchmakerProp;
        private static MethodInfo _requestLobbiesMethod;

        // Lobby ID-based rejoin (for browser joins where code lookup fails)
        private static ulong _queueLobbyId = 0;
        private static MethodInfo _joinServerMethod;  // SteamNetworkLayer.JoinServer(SteamId)

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
                if (!_isInQueue)
                    return "Not queued";
                return $"Queued: {_lastServerCode} (attempt #{_attemptCount})";
            }
        }

        // ─── Initialization ───

        public static void Initialize()
        {
            Main.MelonLog.Msg("ServerQueueController initialized");
        }

        /// <summary>
        /// Resolve LabFusion types, subscribe to events, and apply Harmony patches.
        /// Call AFTER LabFusion is loaded.
        /// </summary>
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
                _networkHelperType = FindTypeByName("NetworkHelper");
                _networkLayerManagerType = FindTypeByName("NetworkLayerManager");

                if (_networkHelperType == null)
                {
                    Main.MelonLog.Warning("[Queue] NetworkHelper type not found — LabFusion not loaded?");
                    return;
                }

                _joinByCodeMethod = _networkHelperType.GetMethod("JoinServerByCode",
                    BindingFlags.Public | BindingFlags.Static);

                if (_networkLayerManagerType != null)
                {
                    _layerProp = _networkLayerManagerType.GetProperty("Layer",
                        BindingFlags.Public | BindingFlags.Static);
                }

                // Build matchmaker callback via expression tree
                var matchmakerInterface = FindTypeByName("IMatchmaker");
                if (matchmakerInterface != null)
                {
                    _callbackInfoType = matchmakerInterface.GetNestedType("MatchmakerCallbackInfo");
                    BuildLobbyCallback();
                }

                Main.MelonLog.Msg("[Queue] Types resolved successfully");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[Queue] ResolveTypes failed: {ex.Message}");
            }
        }

        // ─── Harmony Patches ───

        /// <summary>
        /// Patch NetworkHelper.JoinServerByCode to capture the code being joined,
        /// and InternalServerHelpers.OnDisconnect to detect "server full" denials.
        /// </summary>
        private static void ApplyHarmonyPatches()
        {
            if (_harmonyApplied) return;
            _harmonyApplied = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("BonelabUtilityMod.ServerQueue");

                // Patch NetworkHelper.JoinServerByCode — capture server code on code-based join
                if (_networkHelperType != null)
                {
                    var joinTarget = _networkHelperType.GetMethod("JoinServerByCode",
                        BindingFlags.Public | BindingFlags.Static);
                    var joinPrefix = typeof(ServerQueueController).GetMethod(nameof(JoinByCodePrefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (joinTarget != null && joinPrefix != null)
                    {
                        harmony.Patch(joinTarget, prefix: new HarmonyMethod(joinPrefix));
                        Main.MelonLog.Msg("[Queue] Harmony: Patched NetworkHelper.JoinServerByCode");
                    }
                }

                // Patch SteamNetworkLayer.JoinServer — capture browse-based joins (lobby ID)
                var steamNetworkLayerType = FindTypeByName("SteamNetworkLayer");
                if (steamNetworkLayerType != null)
                {
                    var joinServerTarget = steamNetworkLayerType.GetMethod("JoinServer",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (joinServerTarget != null)
                    {
                        var joinServerPrefix = typeof(ServerQueueController).GetMethod(nameof(JoinServerPrefix),
                            BindingFlags.NonPublic | BindingFlags.Static);
                        if (joinServerPrefix != null)
                        {
                            harmony.Patch(joinServerTarget, prefix: new HarmonyMethod(joinServerPrefix));
                            Main.MelonLog.Msg("[Queue] Harmony: Patched SteamNetworkLayer.JoinServer");
                        }
                    }
                    else
                    {
                        Main.MelonLog.Warning("[Queue] SteamNetworkLayer.JoinServer method not found");
                    }
                }
                else
                {
                    Main.MelonLog.Warning("[Queue] SteamNetworkLayer type not found");
                }

                // Patch InternalServerHelpers.OnDisconnect — detect "server full" denial
                var internalHelpersType = FindTypeByName("InternalServerHelpers");
                if (internalHelpersType != null)
                {
                    var disconnectTarget = internalHelpersType.GetMethod("OnDisconnect",
                        BindingFlags.Public | BindingFlags.Static);
                    var disconnectPrefix = typeof(ServerQueueController).GetMethod(nameof(OnDisconnectPrefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (disconnectTarget != null && disconnectPrefix != null)
                    {
                        harmony.Patch(disconnectTarget, prefix: new HarmonyMethod(disconnectPrefix));
                        Main.MelonLog.Msg("[Queue] Harmony: Patched InternalServerHelpers.OnDisconnect");
                    }
                    else
                    {
                        Main.MelonLog.Warning("[Queue] Could not find OnDisconnect target method");
                    }
                }
                else
                {
                    Main.MelonLog.Warning("[Queue] InternalServerHelpers type not found");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] Harmony patches failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Harmony prefix: captures the server code whenever anyone calls JoinServerByCode.
        /// </summary>
        private static void JoinByCodePrefix(string code)
        {
            if (string.IsNullOrEmpty(code)) return;

            // If we're already in queue and this is our own rejoin attempt, don't reset tracking
            if (_isInQueue && code == _lastServerCode) return;

            _pendingJoinCode = code;
            _pendingJoinTime = Time.time;
            _joinSucceeded = false;
            Main.MelonLog.Msg($"[Queue] Code join attempt detected: {code}");
        }

        /// <summary>
        /// Harmony prefix: captures browse-based joins via SteamNetworkLayer.JoinServer(SteamId).
        /// Extracts the lobby code from Steam lobby metadata so auto-queue can use it.
        /// Uses __args injection since SteamId is a value type from Facepunch.Steamworks.
        /// </summary>
        private static void JoinServerPrefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0) return;

                // Don't overwrite a code already captured from JoinByCodePrefix (code-based join)
                if (!string.IsNullOrEmpty(_pendingJoinCode) &&
                    Time.time - _pendingJoinTime < 5f) return;

                // If we're already in queue and polling, don't reset tracking
                if (_isInQueue) return;

                // Extract ulong lobby ID from the SteamId parameter
                ulong lobbyId = ExtractUlongFromSteamId(__args[0]);
                if (lobbyId == 0) return;

                _pendingLobbyId = lobbyId;
                _pendingJoinTime = Time.time;
                _joinSucceeded = false;

                // Try to get the lobby code from Steam lobby metadata
                string code = GetLobbyCode(lobbyId);
                if (!string.IsNullOrEmpty(code))
                {
                    _pendingJoinCode = code;
                    Main.MelonLog.Msg($"[Queue] Browse join detected — lobby {lobbyId}, code: {code}");
                }
                else
                {
                    _pendingJoinCode = null;
                    Main.MelonLog.Msg($"[Queue] Browse join detected — lobby {lobbyId}, code lookup failed");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] JoinServerPrefix error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract a ulong value from a boxed Steamworks.SteamId struct.
        /// SteamId has a public field "Value" of type ulong.
        /// </summary>
        private static ulong ExtractUlongFromSteamId(object steamIdObj)
        {
            if (steamIdObj == null) return 0;
            if (steamIdObj is ulong ul) return ul;
            try
            {
                var valueField = steamIdObj.GetType().GetField("Value",
                    BindingFlags.Public | BindingFlags.Instance);
                if (valueField != null)
                    return (ulong)valueField.GetValue(steamIdObj);
                // Fallback: try converting directly
                return Convert.ToUInt64(steamIdObj);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Build Steamworks reflection cache to call Lobby.GetData("LobbyCode").
        /// </summary>
        private static void BuildSteamworksCache()
        {
            if (_steamworksCacheBuilt) return;
            _steamworksCacheBuilt = true;

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType("Steamworks.Data.Lobby");
                        if (t != null) { _steamLobbyType = t; break; }
                    }
                    catch { }
                }

                if (_steamLobbyType != null)
                {
                    _lobbyIdField = _steamLobbyType.GetField("Id",
                        BindingFlags.Public | BindingFlags.Instance);
                    _lobbyGetDataMethod = _steamLobbyType.GetMethod("GetData",
                        new[] { typeof(string) });
                    Main.MelonLog.Msg($"[Queue] Steamworks cache: Lobby type found, " +
                        $"Id field: {_lobbyIdField != null}, GetData: {_lobbyGetDataMethod != null}");
                }
                else
                {
                    Main.MelonLog.Warning("[Queue] Steamworks.Data.Lobby type not found");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] BuildSteamworksCache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a lobby's server code by reading its Steam metadata via reflection.
        /// Creates a Facepunch Steamworks.Data.Lobby struct with the given ID,
        /// then calls Lobby.GetData("LobbyCode").
        /// </summary>
        private static string GetLobbyCode(ulong lobbyId)
        {
            try
            {
                BuildSteamworksCache();
                if (_steamLobbyType == null || _lobbyIdField == null || _lobbyGetDataMethod == null)
                    return null;

                // Create a boxed Lobby struct
                object lobby = Activator.CreateInstance(_steamLobbyType);

                // Create a SteamId from the ulong
                var steamIdType = _lobbyIdField.FieldType;
                if (steamIdType == typeof(ulong))
                {
                    _lobbyIdField.SetValue(lobby, lobbyId);
                }
                else
                {
                    // SteamId is a struct with a Value field
                    object steamIdObj = Activator.CreateInstance(steamIdType);
                    var valueField = steamIdType.GetField("Value",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (valueField != null)
                    {
                        valueField.SetValue(steamIdObj, lobbyId);
                    }
                    else
                    {
                        // Try implicit conversion operator: SteamId.op_Implicit(ulong)
                        var op = steamIdType.GetMethod("op_Implicit",
                            BindingFlags.Public | BindingFlags.Static,
                            null, new[] { typeof(ulong) }, null);
                        if (op != null)
                            steamIdObj = op.Invoke(null, new object[] { lobbyId });
                    }
                    _lobbyIdField.SetValue(lobby, steamIdObj);
                }

                // Call lobby.GetData("LobbyCode")
                string code = _lobbyGetDataMethod.Invoke(lobby, new object[] { "LobbyCode" }) as string;
                return string.IsNullOrWhiteSpace(code) ? null : code;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] GetLobbyCode({lobbyId}) error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Harmony prefix: intercepts disconnect reason to detect "server full" denials.
        /// </summary>
        private static void OnDisconnectPrefix(string reason)
        {
            try
            {
                // Reset attempt flag for active queue polling
                _isAttemptingJoin = false;

                // Check if this is a "server full" disconnect from a recent join attempt
                if (!_enabled) return;
                if (_joinSucceeded) return;
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

                // We have a "server full" disconnect within the pending window
                string code = _pendingJoinCode;

                // If no code from prefix, try lobby ID lookup as last resort
                if (string.IsNullOrEmpty(code) && _pendingLobbyId != 0)
                {
                    code = GetLobbyCode(_pendingLobbyId);
                    Main.MelonLog.Msg($"[Queue] Fallback lobby code lookup: {code ?? "failed"}");
                }

                ulong lobbyId = _pendingLobbyId;
                _pendingJoinCode = null;
                _pendingLobbyId = 0;

                if (string.IsNullOrEmpty(code) && lobbyId != 0)
                {
                    // No code available but we have the lobby ID — queue by lobby ID directly
                    Main.MelonLog.Msg($"[Queue] Server full detected! Auto-queuing by lobby ID: {lobbyId}");
                    if (_isInQueue) StopQueue();
                    _queueLobbyId = lobbyId;
                    _lastServerCode = $"lobby:{lobbyId}";
                    StartQueue();
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    Main.MelonLog.Msg($"[Queue] Server full detected! Auto-queuing for code: {code}");
                    if (_isInQueue && _lastServerCode == code) return;
                    if (_isInQueue) StopQueue();
                    _queueLobbyId = 0;
                    _lastServerCode = code;
                    StartQueue();
                }
                else
                {
                    Main.MelonLog.Warning("[Queue] Server full detected but could not determine server code or lobby ID");
                    NotificationHelper.Send(NotificationType.Warning,
                        "Server full — enter code manually to queue");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] OnDisconnectPrefix error: {ex.Message}");
            }
        }

        // ─── Event Subscriptions ───

        private static void SubscribeToFusionEvents()
        {
            if (_eventsSubscribed) return;
            _eventsSubscribed = true;

            try
            {
                var hookType = FindTypeByName("MultiplayerHooking");
                if (hookType == null)
                {
                    Main.MelonLog.Warning("[Queue] MultiplayerHooking not found");
                    return;
                }

                SubscribeToEvent(hookType, "OnJoinedServer", nameof(OnJoinedServer));
                SubscribeToEvent(hookType, "OnDisconnected", nameof(OnDisconnected));

                Main.MelonLog.Msg("[Queue] Event subscriptions complete");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] Event subscription failed: {ex.Message}");
            }
        }

        private static void SubscribeToEvent(Type hookType, string eventName, string handlerName)
        {
            try
            {
                var evt = hookType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (evt == null)
                {
                    Main.MelonLog.Warning($"[Queue] Event {eventName} not found on MultiplayerHooking");
                    return;
                }

                var handler = Delegate.CreateDelegate(
                    evt.EventHandlerType,
                    typeof(ServerQueueController).GetMethod(handlerName,
                        BindingFlags.NonPublic | BindingFlags.Static));
                evt.AddEventHandler(null, handler);
                Main.MelonLog.Msg($"[Queue] Subscribed to {eventName}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] Failed to subscribe to {eventName}: {ex.Message}");
            }
        }

        private static void BuildLobbyCallback()
        {
            try
            {
                if (_callbackInfoType == null) return;

                var param = Expression.Parameter(_callbackInfoType, "info");
                var boxed = Expression.Convert(param, typeof(object));
                var handlerMethod = typeof(ServerQueueController).GetMethod(nameof(HandleLobbyResult),
                    BindingFlags.NonPublic | BindingFlags.Static);
                var call = Expression.Call(handlerMethod, boxed);
                var actionType = typeof(Action<>).MakeGenericType(_callbackInfoType);
                var lambda = Expression.Lambda(actionType, call, param);
                _lobbyCallback = lambda.Compile();

                Main.MelonLog.Msg("[Queue] Built lobby callback delegate");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] Failed to build lobby callback: {ex.Message}");
            }
        }

        // ─── Event Handlers ───

        private static void OnJoinedServer()
        {
            _joinSucceeded = true;
            _pendingJoinCode = null;

            if (_isInQueue)
            {
                Main.MelonLog.Msg("[Queue] Successfully joined server — queue complete!");
                NotificationHelper.Send(NotificationType.Success,
                    $"Queue complete! Joined after {_attemptCount} attempt(s)");
                _isInQueue = false;
                _isAttemptingJoin = false;
                _queueLobbyId = 0;
                StopCoroutine();
            }

            // Clear the server code after successful join so it doesn't persist
            _lastServerCode = "";
        }

        private static void OnDisconnected()
        {
            // Note: disconnect reason handling is in OnDisconnectPrefix Harmony patch
            _isAttemptingJoin = false;
        }

        // ─── Queue Management ───

        public static void StartQueueForCode(string code)
        {
            if (!_enabled)
            {
                NotificationHelper.Send(NotificationType.Warning, "Server Queue is disabled");
                return;
            }
            if (string.IsNullOrEmpty(code))
            {
                NotificationHelper.Send(NotificationType.Warning, "Enter a server code first");
                return;
            }
            if (_isInQueue)
                StopQueue();

            _lastServerCode = code;
            StartQueue();
        }

        private static void StartQueue()
        {
            _isInQueue = true;
            _isAttemptingJoin = false;
            _attemptCount = 0;
            NotificationHelper.Send(NotificationType.Information,
                $"Queued for: {_lastServerCode}\nPolling every {_pollInterval}s");
            _pollCoroutine = MelonCoroutines.Start(QueuePollCoroutine());
        }

        public static void StopQueue()
        {
            if (_isInQueue)
            {
                _isInQueue = false;
                _isAttemptingJoin = false;
                _queueLobbyId = 0;
                NotificationHelper.Send(NotificationType.Information, "Queue stopped");
                Main.MelonLog.Msg("[Queue] Queue stopped");
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

        private static IEnumerator QueuePollCoroutine()
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
                // If we have a lobby ID, join directly by ID instead of going through matchmaker
                if (_queueLobbyId != 0)
                {
                    JoinByLobbyId(_queueLobbyId);
                    return;
                }

                if (_layerProp == null)
                {
                    Main.MelonLog.Warning("[Queue] NetworkLayerManager.Layer not resolved");
                    StopQueue();
                    return;
                }

                var layer = _layerProp.GetValue(null);
                if (layer == null)
                {
                    Main.MelonLog.Warning("[Queue] Network layer is null");
                    StopQueue();
                    return;
                }

                if (_matchmakerProp == null)
                {
                    _matchmakerProp = layer.GetType().GetProperty("Matchmaker",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                object mm = null;
                if (_matchmakerProp != null)
                {
                    mm = _matchmakerProp.GetValue(layer);
                }
                else
                {
                    var matchmakerField = layer.GetType().GetField("_matchmaker",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (matchmakerField != null)
                        mm = matchmakerField.GetValue(layer);
                }

                if (mm == null)
                {
                    Main.MelonLog.Warning("[Queue] Matchmaker is null");
                    _isAttemptingJoin = false;
                    return;
                }

                InvokeRequestLobbies(mm);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] CheckAndJoin error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        private static void InvokeRequestLobbies(object matchmaker)
        {
            try
            {
                if (_requestLobbiesMethod == null)
                {
                    _requestLobbiesMethod = matchmaker.GetType().GetMethod("RequestLobbiesByCode",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_requestLobbiesMethod == null)
                {
                    Main.MelonLog.Warning("[Queue] RequestLobbiesByCode method not found");
                    _isAttemptingJoin = false;
                    return;
                }

                if (_lobbyCallback == null)
                {
                    Main.MelonLog.Warning("[Queue] Lobby callback delegate not built");
                    _isAttemptingJoin = false;
                    return;
                }

                _requestLobbiesMethod.Invoke(matchmaker, new object[] { _lastServerCode, _lobbyCallback });
                Main.MelonLog.Msg($"[Queue] Checking server {_lastServerCode} (attempt #{_attemptCount})...");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] InvokeRequestLobbies error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        /// <summary>
        /// Join a server directly by its Steam lobby ID, bypassing the matchmaker/code path.
        /// Uses reflection to call SteamNetworkLayer.JoinServer(SteamId).
        /// </summary>
        private static void JoinByLobbyId(ulong lobbyId)
        {
            try
            {
                Main.MelonLog.Msg($"[Queue] Attempting direct lobby join: {lobbyId} (attempt #{_attemptCount})");

                if (_layerProp == null)
                {
                    Main.MelonLog.Warning("[Queue] NetworkLayerManager.Layer not resolved");
                    _isAttemptingJoin = false;
                    StopQueue();
                    return;
                }

                var layer = _layerProp.GetValue(null);
                if (layer == null)
                {
                    Main.MelonLog.Warning("[Queue] Network layer is null");
                    _isAttemptingJoin = false;
                    StopQueue();
                    return;
                }

                // Resolve JoinServer method if not yet cached
                if (_joinServerMethod == null)
                {
                    _joinServerMethod = layer.GetType().GetMethod("JoinServer",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_joinServerMethod == null)
                {
                    Main.MelonLog.Warning("[Queue] JoinServer method not found on network layer");
                    _isAttemptingJoin = false;
                    StopQueue();
                    return;
                }

                // Build a SteamId from the ulong lobby ID
                var paramType = _joinServerMethod.GetParameters()[0].ParameterType;
                object steamIdArg;

                if (paramType == typeof(ulong))
                {
                    steamIdArg = lobbyId;
                }
                else
                {
                    // SteamId is a struct — construct it and set .Value
                    steamIdArg = Activator.CreateInstance(paramType);
                    var valueField = paramType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                    if (valueField != null)
                    {
                        steamIdArg = Activator.CreateInstance(paramType);
                        valueField.SetValue(steamIdArg, lobbyId);
                    }
                    else
                    {
                        // Try implicit conversion from ulong
                        var op = paramType.GetMethod("op_Implicit",
                            BindingFlags.Public | BindingFlags.Static,
                            null, new[] { typeof(ulong) }, null);
                        if (op != null)
                            steamIdArg = op.Invoke(null, new object[] { lobbyId });
                    }
                }

                _joinServerMethod.Invoke(layer, new object[] { steamIdArg });
                _isAttemptingJoin = false;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] JoinByLobbyId error: {ex.Message}");
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
                    Main.MelonLog.Warning("[Queue] Server not found in lobby search");
                    NotificationHelper.Send(NotificationType.Warning,
                        "Server not found — stopping queue");
                    _isAttemptingJoin = false;
                    StopQueue();
                    return;
                }

                var firstLobby = lobbies.GetValue(0);
                var metadataField = firstLobby.GetType().GetField("Metadata");
                var metadata = metadataField?.GetValue(firstLobby);
                if (metadata == null)
                {
                    Main.MelonLog.Warning("[Queue] Lobby metadata is null");
                    _isAttemptingJoin = false;
                    return;
                }

                var lobbyInfoProp = metadata.GetType().GetProperty("LobbyInfo");
                var lobbyInfo = lobbyInfoProp?.GetValue(metadata);
                if (lobbyInfo == null)
                {
                    Main.MelonLog.Warning("[Queue] LobbyInfo is null");
                    _isAttemptingJoin = false;
                    return;
                }

                var playerCountProp = lobbyInfo.GetType().GetProperty("PlayerCount");
                var maxPlayersProp = lobbyInfo.GetType().GetProperty("MaxPlayers");

                int playerCount = (int)(playerCountProp?.GetValue(lobbyInfo) ?? 0);
                int maxPlayers = (int)(maxPlayersProp?.GetValue(lobbyInfo) ?? 0);

                Main.MelonLog.Msg($"[Queue] Server status: {playerCount}/{maxPlayers}");

                if (playerCount < maxPlayers)
                {
                    Main.MelonLog.Msg($"[Queue] Slot open! Joining {_lastServerCode}...");
                    NotificationHelper.Send(NotificationType.Success,
                        $"Slot open ({playerCount}/{maxPlayers})! Joining...");

                    _isAttemptingJoin = false;

                    if (_joinByCodeMethod != null)
                    {
                        _joinByCodeMethod.Invoke(null, new object[] { _lastServerCode });
                    }
                }
                else
                {
                    NotificationHelper.Send(NotificationType.Information,
                        $"Still full ({playerCount}/{maxPlayers}) — retrying in {_pollInterval}s");
                    _isAttemptingJoin = false;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Queue] HandleLobbyResult error: {ex.Message}");
                _isAttemptingJoin = false;
            }
        }

        // ─── Helpers ───

        private static Type FindTypeByName(string typeName)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.Name == typeName);
            }
            catch { return null; }
        }
    }
}
