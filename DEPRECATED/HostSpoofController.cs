using MelonLoader;
using BoneLib.Notifications;
using System;
using System.Linq;
using System.Reflection;
using LabFusion.Senders;
using LabFusion.Player;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Rank choices matching LabFusion's PermissionLevel enum.
    /// </summary>
    public enum SpoofRank
    {
        GUEST = 0,
        DEFAULT = 1,
        OPERATOR = 2,
        OWNER = 3
    }

    /// <summary>
    /// Continuously sets the local player's PermissionLevel metadata to a chosen rank,
    /// granting elevated permissions for client-side checks (DevTools, Constrainer,
    /// Custom Avatars, etc.) even when connected as a non-host client.
    ///
    /// Note: Server-validated actions (kick/ban) still require actual host authority.
    /// When disabled, reverts permission to the original value stored on the server.
    /// </summary>
    public static class HostSpoofController
    {
        private static bool _enabled = false;
        private static SpoofRank _targetRank = SpoofRank.OWNER;
        private static float _loopInterval = 1f; // seconds between permission refreshes
        private static float _lastRefreshTime = 0f;

        // Cached reflection handles (resolved once, reused every loop)
        private static bool _cacheResolved = false;
        private static Type _networkInfoType;
        private static PropertyInfo _hasServerProp;
        private static PropertyInfo _isHostProp;
        private static Type _playerIdManagerType;
        private static PropertyInfo _localIdProp;
        private static PropertyInfo _metadataPropOnPlayerID;
        private static PropertyInfo _permLevelPropOnMetadata;
        private static MethodInfo _setValueMethod;
        private static MethodInfo[] _getValueMethods;

        // Status tracking
        private static bool _isConnected = false;
        private static bool _isHost = false;
        private static bool _isSpoofActive = false;

        // Original permission saved before spoofing, used for revert
        private static string _originalPermission = "";
        private static bool _hasOriginalPermission = false;

        // ─── Public Properties ───

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                bool wasEnabled = _enabled;
                _enabled = value;
                if (!_enabled && wasEnabled)
                {
                    // Revert to original permission on disable
                    RevertPermission();
                    _isSpoofActive = false;
                }
            }
        }

        public static SpoofRank TargetRank
        {
            get => _targetRank;
            set => _targetRank = value;
        }

        public static bool IsConnected => _isConnected;
        public static bool IsHost => _isHost;
        public static bool IsSpoofActive => _isSpoofActive;

        /// <summary>
        /// The LabFusion permission string for the selected rank.
        /// </summary>
        private static string TargetRankString => _targetRank switch
        {
            SpoofRank.GUEST => "GUEST",
            SpoofRank.DEFAULT => "DEFAULT",
            SpoofRank.OPERATOR => "OPERATOR",
            SpoofRank.OWNER => "OWNER",
            _ => "OWNER"
        };

        public static string StatusText
        {
            get
            {
                if (!_isConnected) return "Not connected";
                if (_isHost) return "You are Host (OWNER)";
                if (_isSpoofActive) return $"Spoofed → {TargetRankString}";
                return $"Client (original: {_originalPermission})";
            }
        }

        // ─── Initialization ───

        public static void Initialize()
        {
            Main.MelonLog.Msg("RankSpoofer initialized");
        }

        // ─── Update Loop ───

        /// <summary>
        /// Call from OnUpdate. Periodically checks host status and applies permission spoof.
        /// </summary>
        public static void Update()
        {
            if (!_enabled) return;

            float now = UnityEngine.Time.time;
            if (now - _lastRefreshTime < _loopInterval) return;
            _lastRefreshTime = now;

            try
            {
                // Resolve reflection cache once
                if (!_cacheResolved)
                    ResolveCache();

                if (!_cacheResolved) return;

                // Check connection status
                _isConnected = false;
                _isHost = false;

                if (_hasServerProp != null)
                    _isConnected = (bool)_hasServerProp.GetValue(null);

                if (!_isConnected)
                {
                    _isSpoofActive = false;
                    return;
                }

                if (_isHostProp != null)
                    _isHost = (bool)_isHostProp.GetValue(null);

                if (_isHost)
                {
                    // Already host — no spoofing needed
                    _isSpoofActive = false;
                    return;
                }

                // We're a client — apply permission spoof
                ApplyPermissionSpoof();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RankSpoof] Update error: {ex.Message}");
            }
        }

        // ─── Core Logic ───

        /// <summary>
        /// Resolve and cache all reflection handles for LabFusion types.
        /// </summary>
        private static void ResolveCache()
        {
            try
            {
                _networkInfoType = FindTypeByName("NetworkInfo");
                if (_networkInfoType == null) return;

                _hasServerProp = _networkInfoType.GetProperty("HasServer",
                    BindingFlags.Public | BindingFlags.Static);
                _isHostProp = _networkInfoType.GetProperty("IsHost",
                    BindingFlags.Public | BindingFlags.Static);

                _playerIdManagerType = FindTypeByName("PlayerIDManager");
                if (_playerIdManagerType == null) return;

                // PlayerIDManager.LocalID → PlayerID
                _localIdProp = _playerIdManagerType.GetProperty("LocalID",
                    BindingFlags.Public | BindingFlags.Static);

                // We'll resolve the rest lazily in ApplyPermissionSpoof
                // since the local PlayerID may not exist at init time
                _cacheResolved = true;
                Main.MelonLog.Msg("[RankSpoof] Reflection cache resolved");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RankSpoof] Cache resolve error: {ex.Message}");
            }
        }

        /// <summary>
        /// Set LocalPlayer's PermissionLevel metadata to the chosen rank.
        /// Saves the original permission on first spoof for later revert.
        /// </summary>
        private static void ApplyPermissionSpoof()
        {
            try
            {
                // Get our local PlayerID
                var localId = _localIdProp?.GetValue(null);
                if (localId == null) return;

                // Resolve Metadata property on PlayerID (cache it)
                if (_metadataPropOnPlayerID == null)
                {
                    _metadataPropOnPlayerID = localId.GetType().GetProperty("Metadata",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_metadataPropOnPlayerID == null) return;

                var metadata = _metadataPropOnPlayerID.GetValue(localId);
                if (metadata == null) return;

                // Resolve PermissionLevel property on Metadata (cache it)
                if (_permLevelPropOnMetadata == null)
                {
                    _permLevelPropOnMetadata = metadata.GetType().GetProperty("PermissionLevel",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_permLevelPropOnMetadata == null) return;

                var permObj = _permLevelPropOnMetadata.GetValue(metadata);
                if (permObj == null) return;

                // Resolve SetValue and GetValue methods (cache them)
                if (_setValueMethod == null)
                {
                    _setValueMethod = permObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == typeof(string));
                }
                if (_getValueMethods == null)
                {
                    _getValueMethods = permObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod)
                        .ToArray();
                }

                if (_setValueMethod != null && _getValueMethods != null && _getValueMethods.Length > 0)
                {
                    string currentVal = _getValueMethods[0].Invoke(permObj, null) as string ?? "";

                    // Save original permission on first spoof (before we change it)
                    if (!_hasOriginalPermission)
                    {
                        _originalPermission = currentVal;
                        _hasOriginalPermission = true;
                        Main.MelonLog.Msg($"[RankSpoof] Saved original permission: {_originalPermission}");
                    }

                    string target = TargetRankString;
                    if (currentVal != target)
                    {
                        // Set local metadata via reflection
                        _setValueMethod.Invoke(permObj, new object[] { target });

                        // Sync over Fusion network — sends to server which broadcasts to all clients
                        try
                        {
                            PlayerSender.SendPlayerMetadataRequest(
                                PlayerIDManager.LocalSmallID,
                                "PermissionLevel",
                                target);
                        }
                        catch (Exception netEx)
                        {
                            Main.MelonLog.Warning($"[RankSpoof] Network sync failed: {netEx.Message}");
                        }

                        if (!_isSpoofActive)
                        {
                            Main.MelonLog.Msg($"[RankSpoof] Permission spoofed to {target} (Fusion synced)");
                            NotificationHelper.Send(NotificationType.Success, $"Permission looped → {target}");
                        }
                    }

                    _isSpoofActive = true;
                }
                else
                {
                    Main.MelonLog.Warning("[RankSpoof] SetValue/GetValue method not found on PermissionLevel metadata");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RankSpoof] Spoof error: {ex.Message}");
                _isSpoofActive = false;
            }
        }

        /// <summary>
        /// Revert permission to the original value that was stored before spoofing.
        /// </summary>
        private static void RevertPermission()
        {
            if (!_hasOriginalPermission) return;

            try
            {
                var localId = _localIdProp?.GetValue(null);
                if (localId == null) return;

                var metadata = _metadataPropOnPlayerID?.GetValue(localId);
                if (metadata == null) return;

                var permObj = _permLevelPropOnMetadata?.GetValue(metadata);
                if (permObj == null) return;

                if (_setValueMethod != null)
                {
                    _setValueMethod.Invoke(permObj, new object[] { _originalPermission });

                    // Also sync revert over network
                    try
                    {
                        PlayerSender.SendPlayerMetadataRequest(
                            PlayerIDManager.LocalSmallID,
                            "PermissionLevel",
                            _originalPermission);
                    }
                    catch { }

                    Main.MelonLog.Msg($"[RankSpoof] Reverted permission to: {_originalPermission}");
                    NotificationHelper.Send(NotificationType.Information,
                        $"Permission reverted → {_originalPermission}");
                }

                _hasOriginalPermission = false;
                _isSpoofActive = false;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RankSpoof] Revert error: {ex.Message}");
            }
        }

        /// <summary>
        /// One-shot check: are we currently the host?
        /// </summary>
        public static bool CheckIsHost()
        {
            try
            {
                if (!_cacheResolved) ResolveCache();
                if (_isHostProp == null) return false;
                if (_hasServerProp == null) return false;

                bool connected = (bool)_hasServerProp.GetValue(null);
                if (!connected) return false;

                return (bool)_isHostProp.GetValue(null);
            }
            catch { return false; }
        }

        /// <summary>
        /// One-shot check: are we connected to a server?
        /// </summary>
        public static bool CheckHasServer()
        {
            try
            {
                if (!_cacheResolved) ResolveCache();
                if (_hasServerProp == null) return false;
                return (bool)_hasServerProp.GetValue(null);
            }
            catch { return false; }
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
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName);
            }
            catch { return null; }
        }
    }
}
