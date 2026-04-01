using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Data;
using LabFusion.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Central notification helper with global toggle.
    /// All controllers should call NotificationHelper.Send() instead of Notifier.Send() directly.
    /// </summary>
    public static class NotificationHelper
    {
        private static bool _notificationsEnabled = true;
        private static bool _suppressNextNotification = false;

        /// <summary>
        /// Suppress the next toggle notification (used during settings load on boot).
        /// </summary>
        public static void SuppressNextToggleNotification()
        {
            _suppressNextNotification = true;
        }

        public static bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set
            {
                _notificationsEnabled = value;
                // Show toggle confirmation, unless suppressed (boot load)
                if (_suppressNextNotification)
                {
                    _suppressNextNotification = false;
                    return;
                }
                try
                {
                    Notifier.Send(new Notification
                    {
                        Title = "DOOBER UTILS",
                        Message = value ? "Notifications ON" : "Notifications OFF",
                        Type = NotificationType.Success,
                        PopupLength = 1.5f,
                        ShowTitleOnPopup = true
                    });
                }
                catch { }
            }
        }

        public static void Send(NotificationType type, string message, string title = "DOOBER UTILS", float duration = 1.5f, bool showTitle = false)
        {
            if (!_notificationsEnabled) return;
            try
            {
                Notifier.Send(new Notification
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    PopupLength = duration,
                    ShowTitleOnPopup = showTitle
                });
            }
            catch { }
        }
    }

    public class Main : MelonMod
    {
        internal const string Name = "OwO";
        internal const string Description = "Bullshit Client for people with schizophrenia";
        internal const string Author = "XI";
        internal const string Version = "4.4.1";

        private static readonly string[] _emoticons = { "UwU", "QwQ", ".w.", "^w^", ";w;", "=w=", "-w-", "0w0", "7w7", "XwX" };
        private static int _emoticonIndex = 0;
        private static float _lastEmoticonSwap = 0f;
        private const float EMOTICON_CYCLE_INTERVAL = 2f;
        private static bool _shownUpdateNotif = false;
        private static string _remoteVersion = null;

        /// <summary>
        /// Advances the emoticon index if enough time has elapsed. Call once per frame from OnUpdate.
        /// Returns true if the emoticon changed this frame.
        /// </summary>
        private static bool AdvanceEmoticonIfNeeded()
        {
            if (Time.time - _lastEmoticonSwap >= EMOTICON_CYCLE_INTERVAL)
            {
                _lastEmoticonSwap = Time.time;
                _emoticonIndex = (_emoticonIndex + 1) % _emoticons.Length;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the current display name with cycling emoticon, e.g. "OwO UwU"
        /// </summary>
        internal static string GetCyclingName()
        {
            return "OwO " + _emoticons[_emoticonIndex];
        }

        /// <summary>
        /// Call from OnUpdate — advances the emoticon for the client display name.
        /// Does NOT touch the LabFusion nickname — only the local mod title cycles.
        /// </summary>
        internal static void UpdateCyclingNickname()
        {
            AdvanceEmoticonIfNeeded();
        }

        internal static MelonLogger.Instance MelonLog;

        // ── Remote Whitelist ──
#if !NO_WHITELIST
        // XOR-encoded whitelist URL (plaintext never appears in compiled DLL)
        private const byte XOR_KEY = 0xC7;
        private static readonly byte[] _encodedWhitelistUrl = { 0xAF, 0xB2, 0xB1, 0xB4, 0xB0, 0xF8, 0xEE, 0xEF, 0xA8, 0xA7, 0xBE, 0xB8, 0xE5, 0xAD, 0xA0, 0xBC, 0xBF, 0xA3, 0xB7, 0xA1, 0xA0, 0xB7, 0xA3, 0xB3, 0xB0, 0xB0, 0xA9, 0xB9, 0xB5, 0xAE, 0xF7, 0xBB, 0xA8, 0xAB, 0xEA, 0x88, 0xA2, 0xB0, 0xA6, 0xA5, 0xBB, 0xAF, 0xAE, 0xB8, 0xE4, 0xF3, 0xFE, 0xFD, 0xE4, 0xB5, 0xE2, 0xE6, 0xE3, 0xB6, 0xB0, 0xB4, 0xE7, 0xBB, 0xE5, 0xB8, 0xEE, 0xEE, 0xBB, 0xBE, 0xA4, 0xA0, 0xF3, 0xFD, 0xA5, 0xA6, 0xF9, 0xF9, 0xFF, 0xF7, 0xF5, 0xAF, 0xA8, 0xE5, 0xBB, 0xA9, 0xA0, 0xF9, 0xB2, 0xBD, 0xA0, 0xA6, 0xB7, 0xB9, 0xB3, 0xBB, 0xEC, 0xF2, 0xAF, 0xA2, 0xAD };

        private static string DecodeXor(byte[] data)
        {
            char[] c = new char[data.Length];
            for (int i = 0; i < data.Length; i++)
                c[i] = (char)(data[i] ^ XOR_KEY ^ (byte)(i & 0x1F));
            return new string(c);
        }

        // Remote whitelist (populated from URL fetch)
        private static HashSet<ulong> _remoteWhitelist = null;
        private static bool _whitelistFetched = false;
        private static bool _whitelistFetchInProgress = false;
        private static int _fetchRetryCount = 0;
        private const int MAX_FETCH_RETRIES = 3;

        private static bool _whitelistPassed = false;
        private static bool _steamTypesLogged = false;

        // Cached type/property lookups for SteamID detection — avoids repeated heavy reflection
        private static bool _steamIdCacheBuilt = false;
        private static Type _cachedPlayerIdManagerType = null;
        private static System.Reflection.PropertyInfo _cachedLocalPlatformIdProp = null;
        private static System.Reflection.PropertyInfo _cachedLocalLongIdProp = null;
        private static bool _triedIl2CppSteamClient = false;
        private static Type _cachedIl2CppSteamClientType = null;
        private static Type _cachedFusionSteamClientType = null;
        private static ulong _cachedSteamId = 0;
#endif

        public override void OnInitializeMelon()
        {
            MelonLog = LoggerInstance;

            MelonLog.Msg("Initializing DOOBER UTILS V3");

            // Initialize settings (creates preference entries)
            SettingsManager.Initialize();

            // Initialize controllers
            GodModeController.Initialize();
            FullAutoController.Initialize();
            ObjectLauncherController.Initialize();
            TeleportController.Initialize();
            AvatarCopierController.Initialize();
            DashController.Initialize();
            FlightController.Initialize();
            ExplosivePunchController.Initialize();
            AntiConstraintController.Initialize();
            AntiKnockoutController.Initialize();
            PlayerSpawnController.Initialize();
            UnbreakableGripController.Initialize();
            ForceGrabController.Initialize();
            WaypointController.Initialize();
            DespawnAllController.Initialize();
            MapChangeController.Initialize();
            ServerQueueController.Initialize();
            PlayerInfoController.Initialize();
            RagdollController.Initialize();
            BlockController.Initialize();
            KeybindManager.Initialize();
            PlayerTargeting.Initialize();
            ScreenShareController.Initialize();
            RandomExplodeController.Initialize();
            ForceSpawnerController.Initialize();
            BodylogPresetController.Initialize();
            RemoveWindSFXController.Initialize();
            WeepingAngelController.Initialize();
            AutoRunController.Initialize();
            DefaultWorldController.Initialize();
            AutoHostController.Initialize();
            ExplosiveImpactController.Initialize();
            XYZScaleController.Initialize();
            DisableAvatarFXController.Initialize();
            HolsterHiderController.Initialize();
            QuickMenuController.Initialize();
            GhostModeController.Initialize();
            AntiSlowmoController.Initialize();
            AntiTeleportController.Initialize();
            AntiRagdollController.Initialize();
            LobbyBrowserController.Initialize();
            SpawnLoggerController.Initialize();
            DamageMultiplierController.Initialize();
            AINpcController.Initialize();
            AvatarLoggerController.Initialize();
            PlayerActionLoggerController.Initialize();

            // Load saved settings and apply to controllers
            // Suppress the notification that would fire on boot when loading saved toggle
            NotificationHelper.SuppressNextToggleNotification();
            SettingsManager.LoadAll();

            // Start remote whitelist fetch, then check whitelist
#if !NO_WHITELIST
            FetchRemoteWhitelistAsync();

            // Try whitelist check immediately (may fail if Steam not ready or fetch pending)
            CheckWhitelistAndSetup();

            // Also hook to level loaded event as backup for whitelist + BoneMenu
            Hooking.OnLevelLoaded += (info) => { CheckWhitelistAndSetup(); ScreenShareController.OnLevelLoaded(); WaypointController.OnLevelLoaded(); AutoHostController.OnFirstLevelLoaded(); InfiniteAmmoController.OnLevelLoaded(); HolsterHiderController.OnLevelLoaded(); if (BodyLogColorController.Enabled) BodyLogColorController.ApplyAll(); OverlayMenu.InvalidateStyles(); ShowUpdateNotification(); GhostModeController.OnLevelLoaded(); AntiTeleportController.NotifyIntentionalTeleport(); AvatarLoggerController.OnLevelLoaded(); PlayerActionLoggerController.OnLevelLoaded(); };

            // Clean up cached Unity references on level unload (RigManager.OnDestroy)
            Hooking.OnLevelUnloaded += OnLevelUnloaded;
#else
            // No whitelist — set up BoneMenu directly
            BoneMenuSetup.Setup();
            ServerQueueController.ApplyPatches();
            TryAutoLoginFusion();
            Hooking.OnLevelLoaded += (info) => { BoneMenuSetup.Setup(); ScreenShareController.OnLevelLoaded(); WaypointController.OnLevelLoaded(); AutoHostController.OnFirstLevelLoaded(); InfiniteAmmoController.OnLevelLoaded(); HolsterHiderController.OnLevelLoaded(); if (BodyLogColorController.Enabled) BodyLogColorController.ApplyAll(); OverlayMenu.InvalidateStyles(); ShowUpdateNotification(); GhostModeController.OnLevelLoaded(); AntiTeleportController.NotifyIntentionalTeleport(); AvatarLoggerController.OnLevelLoaded(); PlayerActionLoggerController.OnLevelLoaded(); };

            // Clean up cached Unity references on level unload (RigManager.OnDestroy)
            Hooking.OnLevelUnloaded += OnLevelUnloaded;
#endif

            MelonLog.Msg("DOOBER UTILS V3 loaded successfully!");

            // Fetch latest version from GitHub (non-blocking)
            FetchRemoteVersionAsync();
        }

        private static void ShowUpdateNotification()
        {
            if (_shownUpdateNotif) return;
            _shownUpdateNotif = true;
            try
            {
                string displayVersion = _remoteVersion ?? Version;
                string message = _remoteVersion != null && _remoteVersion != Version
                    ? $"New update available! (v{_remoteVersion})"
                    : $"Updated! (v{displayVersion})";

                Notifier.Send(new Notification
                {
                    Title = "owo",
                    Message = message,
                    Type = NotificationType.Success,
                    PopupLength = 3f,
                    ShowTitleOnPopup = true
                });
            }
            catch { }
        }

        private static void FetchRemoteVersionAsync()
        {
            // Run on a plain Thread to avoid async/await thread pool continuations
            // which crash IL2CPP's GC ("Collecting from unknown thread")
            var thread = new Thread(() =>
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    string url = "https://raw.githubusercontent.com/Largetact/owo/main/BonelabUtilityMod.csproj?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string csproj = client.GetStringAsync(url).GetAwaiter().GetResult();

                    var match = Regex.Match(csproj, @"<Version>([^<]+)</Version>");
                    if (match.Success)
                    {
                        _remoteVersion = match.Groups[1].Value.Trim();
                    }
                }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Fetch the whitelist from a remote URL on a background thread.
        /// Avoids async/await which crashes IL2CPP's GC on thread pool threads.
        /// </summary>
#if !NO_WHITELIST
        private void FetchRemoteWhitelistAsync()
        {
            if (_whitelistFetchInProgress) return;
            _whitelistFetchInProgress = true;

            var thread = new Thread(() =>
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(15);
                    string url = DecodeXor(_encodedWhitelistUrl) + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string response = client.GetStringAsync(url).GetAwaiter().GetResult();

                    var ids = new HashSet<ulong>();
                    foreach (string line in response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                            continue;
                        string idPart = trimmed.Split(new[] { ' ', '\t', '/', '#' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (ulong.TryParse(idPart, out ulong id))
                            ids.Add(id);
                    }

                    _remoteWhitelist = ids;
                    _whitelistFetched = true;
                    _whitelistFetchInProgress = false;
                }
                catch
                {
                    _whitelistFetchInProgress = false;
                    _fetchRetryCount++;

                    if (_fetchRetryCount < MAX_FETCH_RETRIES)
                    {
                        Thread.Sleep(_fetchRetryCount * 2000);
                        FetchRemoteWhitelistAsync();
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Check SteamID whitelist. Only makes a decision when BOTH the fetch has completed
        /// AND the SteamID is available. Defers otherwise so OnUpdate/OnLevelLoaded can retry.
        /// </summary>
        private void CheckWhitelistAndSetup()
        {
            // Already passed — just ensure menu is set up
            if (_whitelistPassed)
            {
                BoneMenuSetup.Setup();
                return;
            }

            // Don't decide until the remote list is fetched — avoids the race condition
            // where SteamID is ready but fetch hasn't completed yet
            if (!_whitelistFetched)
                return;

            ulong steamId = GetLocalSteamID();
            if (steamId == 0)
            {
                // Steam/Fusion not ready yet, will retry on next update/level load
                return;
            }

            // Both pieces ready — make the decision
            if (_remoteWhitelist != null && _remoteWhitelist.Contains(steamId))
            {
                _whitelistPassed = true;
                MelonLog.Msg($"Whitelist check PASSED (SteamID: {steamId})");
                BoneMenuSetup.Setup();
                ServerQueueController.ApplyPatches();
                TryAutoLoginFusion();
            }
            else
            {
                MelonLog.Msg($"Whitelist check failed (SteamID: {steamId}) - BoneMenu hidden");
            }
        }

        /// <summary>
        /// Get local SteamID via LabFusion's PlayerIDManager.LocalPlatformID (reflection).
        /// Uses cached type/property lookups to avoid repeated heavy assembly enumeration.
        /// </summary>
        private static ulong GetLocalSteamID()
        {
            // Return cached result if we already found it
            if (_cachedSteamId != 0) return _cachedSteamId;

            try
            {
                // Build cache once — find the types/properties we need
                if (!_steamIdCacheBuilt)
                {
                    _steamIdCacheBuilt = true;
                    try
                    {
                        _cachedPlayerIdManagerType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                            .FirstOrDefault(t => t.Name == "PlayerIDManager");

                        if (_cachedPlayerIdManagerType != null)
                        {
                            _cachedLocalPlatformIdProp = _cachedPlayerIdManagerType.GetProperty(
                                "LocalPlatformID", BindingFlags.Public | BindingFlags.Static);
                            _cachedLocalLongIdProp = _cachedPlayerIdManagerType.GetProperty(
                                "LocalLongID", BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                    catch { }
                }

                // Try LabFusion PlayerIDManager first (safe managed reflection)
                if (_cachedLocalPlatformIdProp != null)
                {
                    try
                    {
                        var val = _cachedLocalPlatformIdProp.GetValue(null);
                        if (val is ulong id && id != 0) { _cachedSteamId = id; return id; }
                    }
                    catch { }
                }

                if (_cachedLocalLongIdProp != null)
                {
                    try
                    {
                        var val = _cachedLocalLongIdProp.GetValue(null);
                        if (val is ulong id && id != 0) { _cachedSteamId = id; return id; }
                    }
                    catch { }
                }

                // IL2CPP SteamClient fallback — cache type lookup but retry value reads
                if (!_triedIl2CppSteamClient)
                {
                    _triedIl2CppSteamClient = true;
                    try
                    {
                        _cachedIl2CppSteamClientType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                            .FirstOrDefault(t => t.FullName == "Il2CppSteamworks.SteamClient");

                        _cachedFusionSteamClientType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                            .FirstOrDefault(t => t.FullName == "Steamworks.SteamClient");
                    }
                    catch { }
                }

                // Retry value reads on cached types each frame (lightweight)
                if (_cachedIl2CppSteamClientType != null)
                {
                    try
                    {
                        ulong result = TrySteamClientType(_cachedIl2CppSteamClientType);
                        if (result != 0) { _cachedSteamId = result; return result; }
                    }
                    catch { }
                }

                if (_cachedFusionSteamClientType != null)
                {
                    try
                    {
                        ulong result = TrySteamClientType(_cachedFusionSteamClientType);
                        if (result != 0) { _cachedSteamId = result; return result; }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MelonLog?.Warning($"SteamID check error: {ex.Message}");
            }
            return 0;
        }

        private static bool _steamIdLogOnce = false;
        private static int _steamIdLogCount = 0;
        private const int STEAM_ID_LOG_MAX = 3;

        private static ulong TrySteamClientType(Type steamClientType)
        {
            bool shouldLog = _steamIdLogCount < STEAM_ID_LOG_MAX;
            if (shouldLog)
                MelonLog?.Msg($"[SteamID] Trying type: {steamClientType.FullName} (Assembly: {steamClientType.Assembly.GetName().Name})");

            // Try IsValid first
            var isValidProp = steamClientType.GetProperty("IsValid", BindingFlags.Public | BindingFlags.Static);
            if (isValidProp != null)
            {
                bool isValid = (bool)isValidProp.GetValue(null);
                if (!isValid)
                {
                    if (shouldLog)
                        MelonLog?.Msg($"[SteamID] {steamClientType.FullName}.IsValid = False, skipping");
                    return 0;
                }
                if (shouldLog)
                    MelonLog?.Msg($"[SteamID] {steamClientType.FullName}.IsValid = True!");
            }

            // Try SteamId property
            var steamIdProp = steamClientType.GetProperty("SteamId", BindingFlags.Public | BindingFlags.Static);
            if (steamIdProp != null)
            {
                var steamIdObj = steamIdProp.GetValue(null);
                if (steamIdObj != null)
                {
                    // Try .Value field, then property, then direct cast
                    var vf = steamIdObj.GetType().GetField("Value");
                    if (vf != null) { var r = (ulong)vf.GetValue(steamIdObj); if (r != 0) { LogSteamIdFound(r, steamClientType); return r; } }
                    var vp = steamIdObj.GetType().GetProperty("Value");
                    if (vp != null) { var r = (ulong)vp.GetValue(steamIdObj); if (r != 0) { LogSteamIdFound(r, steamClientType); return r; } }
                    try { ulong direct = Convert.ToUInt64(steamIdObj); if (direct != 0) { LogSteamIdFound(direct, steamClientType); return direct; } } catch { }
                }
            }

            // If no SteamId property, list all static properties for diagnostics (once)
            if (steamIdProp == null && shouldLog)
            {
                MelonLog?.Warning($"[SteamID] No SteamId property on {steamClientType.FullName}. Static properties:");
                foreach (var prop in steamClientType.GetProperties(BindingFlags.Public | BindingFlags.Static))
                    MelonLog?.Msg($"[SteamID]   {prop.Name} ({prop.PropertyType.Name})");
            }

            if (shouldLog)
                _steamIdLogCount++;

            return 0;
        }

        private static void LogSteamIdFound(ulong id, Type source)
        {
            if (!_steamIdLogOnce)
            {
                _steamIdLogOnce = true;
                MelonLog?.Msg($"[SteamID] Got SteamID {id} from {source.FullName}");
            }
        }

#endif

        private static bool _fusionAutoLoginAttempted = false;
        private static bool _fusionLoggedIn = false;

        /// <summary>
        /// Automatically trigger Fusion network layer login so AutoHost and other
        /// Fusion-dependent features work without manually clicking Log In.
        /// Called each frame from OnUpdate until successful.
        /// </summary>
        private static void TryAutoLoginFusion()
        {
            if (_fusionLoggedIn) return;

            try
            {
                if (NetworkLayerManager.LoggedIn)
                {
                    if (!_fusionLoggedIn)
                    {
                        _fusionLoggedIn = true;
                        MelonLog?.Msg("[AutoLogin] Fusion is logged in");
                    }
                    return;
                }

                // Only attempt the actual login call once
                if (_fusionAutoLoginAttempted) return;
                _fusionAutoLoginAttempted = true;

                var targetLayer = NetworkLayerManager.GetTargetLayer();
                if (targetLayer != null)
                {
                    NetworkLayerManager.LogIn(targetLayer);
                    MelonLog?.Msg("[AutoLogin] Fusion login triggered automatically");

                    // Check if it worked immediately (synchronous login)
                    if (NetworkLayerManager.LoggedIn)
                    {
                        _fusionLoggedIn = true;
                        MelonLog?.Msg("[AutoLogin] Fusion login confirmed");
                    }
                }
                else
                {
                    // GetTargetLayer returned null — allow retry next time
                    _fusionAutoLoginAttempted = false;
                    MelonLog?.Warning("[AutoLogin] No target network layer found, will retry");
                }
            }
            catch (Exception ex)
            {
                MelonLog?.Warning($"[AutoLogin] Failed: {ex.Message}");
                // Allow retry
                _fusionAutoLoginAttempted = false;
            }
        }

        public override void OnUpdate()
        {
#if !NO_WHITELIST
            // Keep retrying until whitelist passes — SteamID or fetch may not be ready yet
            if (!_whitelistPassed)
            {
                TryAutoLoginFusion();
                CheckWhitelistAndSetup();
                return;
            }
#endif
            TryAutoLoginFusion();
            GodModeController.Update();
            FullAutoController.Update();
            ObjectLauncherController.Update();
            DashController.Update();
            FlightController.Update();
            ExplosivePunchController.Update();
            GroundPoundController.Update();
            AntiConstraintController.Update();
            PlayerSpawnController.Update();
            UnbreakableGripController.Update();
            ForceGrabController.Update();
            WaypointController.Update();
            DespawnAllController.Update();
            PlayerInfoController.Update();
            RagdollController.Update();
            BodyLogColorController.Update();
            KeybindManager.Update();
            BlockController.Update();
            FreezePlayerController.Update();
            HolsterHiderController.Update();
            ScreenShareController.Update();
            RandomExplodeController.Update();
            WeepingAngelController.Update();
            AutoRunController.Update();
            AutoHostController.Update();
            ChaosGunController.Update();
            ExplosiveImpactController.Update();
            AntiGravityChangeController.Update();
            SpinbotController.Update();
            BunnyHopController.Update();
            GhostModeController.Update();
            AntiSlowmoController.Update();
            AntiTeleportController.Update();
            ForceSpawnerController.Update();
            AntiRagdollController.Update();
            DamageMultiplierController.Update();
            AvatarLoggerController.Update();
            PlayerActionLoggerController.Update();
            OverlayMenu.CheckInput();
            QuickMenuController.CheckInput();

            UpdateCyclingNickname();

            SettingsManager.Update();
        }

        public override void OnLateUpdate()
        {
            FreezePlayerController.LateUpdate();
        }

        /// <summary>
        /// Called when the local RigManager is destroyed (level unload).
        /// Clears cached Unity references that become stale between levels.
        /// </summary>
        private static void OnLevelUnloaded()
        {
            try { GroundPoundController.OnLevelUnloaded(); } catch { }
            try { GravityBootsController.OnLevelUnloaded(); } catch { }
            try { FlightController.OnLevelUnloaded(); } catch { }
            try { DashController.OnLevelUnloaded(); } catch { }
            try { WeepingAngelController.OnLevelUnloaded(); } catch { }
            try { ForceGrabController.OnLevelUnloaded(); } catch { }
            try { ObjectLauncherController.OnLevelUnloaded(); } catch { }
            try { ChaosGunController.OnLevelUnloaded(); } catch { }
            try { ExplosiveImpactController.OnLevelUnloaded(); } catch { }
            try { XYZScaleController.OnLevelUnloaded(); } catch { }
            try { BunnyHopController.OnLevelUnloaded(); } catch { }
            try { SpinbotController.OnLevelUnloaded(); } catch { }
            try { AntiTeleportController.OnLevelUnloaded(); } catch { }
        }

        public override void OnGUI()
        {
            OverlayMenu.Draw();
            QuickMenuController.Draw();
        }

        public override void OnDeinitializeMelon()
        {
            ScreenShareController.Shutdown();
            // Force a final save on game exit so no settings are lost
            SettingsManager.ForceSave();
        }
    }

    public static class BoneMenuSetup
    {
        private static Page _mainPage;
        private static bool _isSetup = false;

        public static void Setup()
        {
            try
            {
                // Prevent duplicate setups — set flag IMMEDIATELY to prevent race conditions
                // (async whitelist callback + OnLevelLoaded can both call Setup concurrently)
                if (_isSetup)
                {
                    Main.MelonLog.Msg("BoneMenu already set up, skipping...");
                    return;
                }
                _isSetup = true;

                // Check if Page.Root is available
                if (Page.Root == null)
                {
                    Main.MelonLog.Warning("Page.Root is null, BoneMenu setup deferred");
                    _isSetup = false;
                    return;
                }

                Main.MelonLog.Msg("Setting up BoneMenu...");

                // Create main page - #3dd2ff = RGB(61, 210, 255)
                _mainPage = Page.Root.CreatePage("! OwO !", new Color(61f / 255f, 210f / 255f, 255f / 255f));
                if (_mainPage == null)
                {
                    Main.MelonLog.Error("Failed to create main BoneMenu page");
                    return;
                }

                // Move our page link to the front of the root so it appears first
                MovePageToFront(Page.Root);

                // ============================================
                // Global Settings
                // ============================================
                _mainPage.CreateBool(
                    "Notifications",
                    Color.white,
                    NotificationHelper.NotificationsEnabled,
                    (value) => { NotificationHelper.NotificationsEnabled = value; SettingsManager.MarkDirty(); }
                );

                // ============================================
                // PLAYER submenu (God Mode, Anti-Constraint, Anti-Knockout, Unbreakable Grip)
                // ============================================
                var playerPage = _mainPage.CreatePage("Player", Color.green);

                // ── Simple toggles (alphabetical) ──
                playerPage.CreateBool(
                    "Anti-Constraint",
                    Color.yellow,
                    AntiConstraintController.IsEnabled,
                    (value) => { AntiConstraintController.IsEnabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateFunction(
                    "Clear Constraints Now",
                    Color.green,
                    AntiConstraintController.ClearConstraints
                );
                playerPage.CreateBool(
                    "Anti-Grab",
                    Color.cyan,
                    AntiGrabController.Enabled,
                    (value) => { AntiGrabController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Anti-Gravity Change [Earth Loop]",
                    Color.cyan,
                    AntiGravityChangeController.Enabled,
                    (value) => { AntiGravityChangeController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Anti-Knockout",
                    Color.magenta,
                    AntiKnockoutController.IsEnabled,
                    (value) => { AntiKnockoutController.IsEnabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Anti-Ragdoll",
                    Color.yellow,
                    AntiRagdollController.Enabled,
                    (value) => { AntiRagdollController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Anti-Slowmo",
                    Color.yellow,
                    AntiSlowmoController.Enabled,
                    (value) => { AntiSlowmoController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Anti-Teleport",
                    Color.yellow,
                    AntiTeleportController.Enabled,
                    (value) => { AntiTeleportController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Auto Run",
                    Color.green,
                    AutoRunController.Enabled,
                    (value) => { AutoRunController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Ghost Mode",
                    Color.magenta,
                    GhostModeController.Enabled,
                    (value) => { GhostModeController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "God Mode (Inf Vitality)",
                    Color.white,
                    GodModeController.IsGodModeEnabled,
                    (value) => { GodModeController.IsGodModeEnabled = value; SettingsManager.MarkDirty(); }
                );
                playerPage.CreateBool(
                    "Unbreakable Grip",
                    Color.cyan,
                    UnbreakableGripController.IsEnabled,
                    (value) => { UnbreakableGripController.IsEnabled = value; SettingsManager.MarkDirty(); }
                );

                // ── Sub-pages (alphabetical) ──
                var defaultWorldPage = playerPage.CreatePage("Default World", Color.yellow);
                defaultWorldPage.CreateBool(
                    "Enabled",
                    Color.white,
                    DefaultWorldController.Enabled,
                    (value) => { DefaultWorldController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                defaultWorldPage.CreateFunction(
                    $"Current: {(string.IsNullOrEmpty(DefaultWorldController.LevelName) ? "(none)" : DefaultWorldController.LevelName)}",
                    Color.white,
                    () => { }
                );
                defaultWorldPage.CreateFunction(
                    "Set Current Level as Default",
                    Color.green,
                    () => DefaultWorldController.SetCurrentLevelAsDefault()
                );
                defaultWorldPage.CreateFunction(
                    "Clear Default",
                    Color.red,
                    () => DefaultWorldController.ClearDefault()
                );

                // Auto Host Friends-Only submenu inside Player
                var autoHostPage = playerPage.CreatePage("Auto Host", Color.green);
                autoHostPage.CreateBool(
                    "Enabled",
                    Color.white,
                    AutoHostController.Enabled,
                    (value) => { AutoHostController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                autoHostPage.CreateFunction(
                    "Hosts a friends-only lobby on game launch",
                    Color.gray,
                    () => { }
                );

                // Dash submenu inside Player
                var dashPage = playerPage.CreatePage("Dash", Color.cyan);
                dashPage.CreateBool(
                    "Enabled",
                    Color.white,
                    DashController.IsDashEnabled,
                    (value) => { DashController.IsDashEnabled = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateFloat(
                    "Dash Force",
                    Color.cyan,
                    DashController.DashForce,
                    5f,
                    0f,
                    500f,
                    (value) => { DashController.DashForce = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Instantaneous",
                    Color.cyan,
                    DashController.IsDashInstantaneous,
                    (value) => { DashController.IsDashInstantaneous = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Continuous (Hold)",
                    Color.cyan,
                    DashController.IsDashContinuous,
                    (value) => { DashController.IsDashContinuous = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Hand Oriented",
                    Color.yellow,
                    DashController.IsHandOriented,
                    (value) => { DashController.IsHandOriented = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Kill Velocity On Land",
                    Color.green,
                    DashController.KillVelocityOnLand,
                    (value) => { DashController.KillVelocityOnLand = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Use Left Hand",
                    Color.yellow,
                    DashController.UseLeftHand,
                    (value) => { DashController.UseLeftHand = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Lock-On",
                    Color.red,
                    DashController.LockOnEnabled,
                    (value) => { DashController.LockOnEnabled = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateEnum(
                    "Lock-On Filter",
                    Color.red,
                    DashController.LockOnFilter,
                    (value) => { DashController.LockOnFilter = (TargetFilter)value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Look At Target",
                    Color.red,
                    DashController.LookAtTarget,
                    (value) => { DashController.LookAtTarget = value; SettingsManager.MarkDirty(); }
                );
                dashPage.CreateBool(
                    "Look At Head (Yaw+Pitch)",
                    Color.red,
                    DashController.LookAtHead,
                    (value) => { DashController.LookAtHead = value; SettingsManager.MarkDirty(); }
                );
                BuildSelectPlayerSubMenu(dashPage);

                // ── Dash Effects submenu ──
                var dashEffectsPage = dashPage.CreatePage("Effects", Color.green);
                // Custom effect
                dashEffectsPage.CreateBool("Custom Effect", Color.cyan, DashController.EffectEnabled,
                    (value) => { DashController.EffectEnabled = value; SettingsManager.MarkDirty(); });
                dashEffectsPage.CreateString("Effect Barcode", Color.cyan, DashController.EffectBarcode,
                    (value) => { DashController.EffectBarcode = value; SettingsManager.MarkDirty(); });
                var dashEffectSearchResults = dashEffectsPage.CreatePage("+ Search Effect", Color.yellow);
                dashEffectsPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                dashEffectsPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(dashEffectSearchResults, (barcode) =>
                    {
                        DashController.EffectBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Dash effect set: {barcode}");
                    }));
                // SmashBone
                dashEffectsPage.CreateBool("SmashBone Effect", Color.red, DashController.SmashBoneEnabled,
                    (value) => { DashController.SmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                dashEffectsPage.CreateFloat("SmashBone Count", Color.red, DashController.SmashBoneCount, 1f, 1f, 20f,
                    (value) => { DashController.SmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                dashEffectsPage.CreateBool("SmashBone Flip", Color.yellow, DashController.SmashBoneFlip,
                    (value) => { DashController.SmashBoneFlip = value; SettingsManager.MarkDirty(); });
                // Cosmetic
                var dashCosmeticPage = dashEffectsPage.CreatePage("Cosmetic Effect", Color.green);
                dashCosmeticPage.CreateBool("Enabled", Color.white, DashController.CosmeticEnabled,
                    (value) => { DashController.CosmeticEnabled = value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateString("Barcode", Color.cyan, DashController.CosmeticBarcode,
                    (value) => { DashController.CosmeticBarcode = value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateFloat("Count", Color.magenta, DashController.CosmeticCount, 1f, 1f, 20f,
                    (value) => { DashController.CosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateBool("Flip Effect", Color.yellow, DashController.CosmeticFlip,
                    (value) => { DashController.CosmeticFlip = value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateFloat("Matrix Count", Color.magenta, DashController.EffectMatrixCount, 1f, 1f, 25f,
                    (value) => { DashController.EffectMatrixCount = (int)value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateFloat("Matrix Spacing", Color.magenta, DashController.EffectMatrixSpacing, 0.1f, 0.1f, 10f,
                    (value) => { DashController.EffectMatrixSpacing = value; SettingsManager.MarkDirty(); });
                dashCosmeticPage.CreateEnum("Matrix Mode", Color.magenta, DashController.EffectMatrixMode,
                    (value) => { DashController.EffectMatrixMode = (MatrixMode)value; SettingsManager.MarkDirty(); });
                var dashCosSearchResults = dashCosmeticPage.CreatePage("+ Search", Color.yellow);
                dashCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                dashCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                dashCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(dashCosSearchResults, (barcode) =>
                    {
                        DashController.CosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Dash cosmetic set: {barcode}");
                    }));
                // Timing
                dashEffectsPage.CreateFloat("Spawn Delay", Color.cyan, DashController.EffectSpawnDelay, 0.05f, 0f, 2f,
                    (value) => { DashController.EffectSpawnDelay = value; SettingsManager.MarkDirty(); });
                dashEffectsPage.CreateFloat("Spawn Interval", Color.cyan, DashController.EffectSpawnInterval, 0.1f, 0f, 5f,
                    (value) => { DashController.EffectSpawnInterval = value; SettingsManager.MarkDirty(); });
                // Offset
                var dashOffsetPage = dashEffectsPage.CreatePage("Transform Offset", Color.yellow);
                dashOffsetPage.CreateFloat("Offset X", Color.red, DashController.EffectOffsetX, 0.1f, -2f, 2f,
                    (value) => { DashController.EffectOffsetX = value; SettingsManager.MarkDirty(); });
                dashOffsetPage.CreateFloat("Offset Y", Color.green, DashController.EffectOffsetY, 0.1f, -2f, 2f,
                    (value) => { DashController.EffectOffsetY = value; SettingsManager.MarkDirty(); });
                dashOffsetPage.CreateFloat("Offset Z", Color.blue, DashController.EffectOffsetZ, 0.1f, -2f, 2f,
                    (value) => { DashController.EffectOffsetZ = value; SettingsManager.MarkDirty(); });

                // Flight submenu inside Player
                var flightPage = playerPage.CreatePage("Flight", Color.yellow);
                flightPage.CreateBool(
                    "Enabled",
                    Color.white,
                    FlightController.Enabled,
                    (value) => { FlightController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateFloat(
                    "Speed Multiplier",
                    Color.yellow,
                    FlightController.SpeedMultiplier,
                    0.5f,
                    0.5f,
                    20f,
                    (value) => { FlightController.SpeedMultiplier = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateBool(
                    "Acceleration",
                    Color.cyan,
                    FlightController.AccelerationEnabled,
                    (value) => { FlightController.AccelerationEnabled = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateFloat(
                    "Acceleration Rate",
                    Color.cyan,
                    FlightController.AccelerationRate,
                    0.5f,
                    0.1f,
                    10f,
                    (value) => { FlightController.AccelerationRate = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateBool(
                    "Momentum",
                    Color.green,
                    FlightController.MomentumEnabled,
                    (value) => { FlightController.MomentumEnabled = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateBool(
                    "Lock-On",
                    Color.red,
                    FlightController.LockOnEnabled,
                    (value) => { FlightController.LockOnEnabled = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateEnum(
                    "Lock-On Filter",
                    Color.red,
                    FlightController.LockOnFilter,
                    (value) => { FlightController.LockOnFilter = (TargetFilter)value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateBool(
                    "Look At Target",
                    Color.red,
                    FlightController.LookAtTarget,
                    (value) => { FlightController.LookAtTarget = value; SettingsManager.MarkDirty(); }
                );
                flightPage.CreateBool(
                    "Look At Head (Yaw+Pitch)",
                    Color.red,
                    FlightController.LookAtHead,
                    (value) => { FlightController.LookAtHead = value; SettingsManager.MarkDirty(); }
                );
                BuildSelectPlayerSubMenu(flightPage);

                // ── Flight Effects submenu ──
                var flightEffectsPage = flightPage.CreatePage("Effects", Color.green);
                // Effect orientation
                flightEffectsPage.CreateBool("Effect Hand Oriented", Color.yellow, FlightController.EffectHandOriented,
                    (value) => { FlightController.EffectHandOriented = value; SettingsManager.MarkDirty(); });
                flightEffectsPage.CreateBool("Effect Use Left Hand", Color.yellow, FlightController.EffectUseLeftHand,
                    (value) => { FlightController.EffectUseLeftHand = value; SettingsManager.MarkDirty(); });
                // Custom effect
                flightEffectsPage.CreateBool("Custom Effect", Color.cyan, FlightController.EffectEnabled,
                    (value) => { FlightController.EffectEnabled = value; SettingsManager.MarkDirty(); });
                flightEffectsPage.CreateString("Effect Barcode", Color.cyan, FlightController.EffectBarcode,
                    (value) => { FlightController.EffectBarcode = value; SettingsManager.MarkDirty(); });
                var flightEffectSearchResults = flightEffectsPage.CreatePage("+ Search Effect", Color.yellow);
                flightEffectsPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                flightEffectsPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(flightEffectSearchResults, (barcode) =>
                    {
                        FlightController.EffectBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Flight effect set: {barcode}");
                    }));
                // SmashBone
                flightEffectsPage.CreateBool("SmashBone Effect", Color.red, FlightController.SmashBoneEnabled,
                    (value) => { FlightController.SmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                flightEffectsPage.CreateFloat("SmashBone Count", Color.red, FlightController.SmashBoneCount, 1f, 1f, 20f,
                    (value) => { FlightController.SmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                flightEffectsPage.CreateBool("SmashBone Flip", Color.yellow, FlightController.SmashBoneFlip,
                    (value) => { FlightController.SmashBoneFlip = value; SettingsManager.MarkDirty(); });
                // Cosmetic
                var flightCosmeticPage = flightEffectsPage.CreatePage("Cosmetic Effect", Color.green);
                flightCosmeticPage.CreateBool("Enabled", Color.white, FlightController.CosmeticEnabled,
                    (value) => { FlightController.CosmeticEnabled = value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateString("Barcode", Color.cyan, FlightController.CosmeticBarcode,
                    (value) => { FlightController.CosmeticBarcode = value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateFloat("Count", Color.magenta, FlightController.CosmeticCount, 1f, 1f, 20f,
                    (value) => { FlightController.CosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateBool("Flip Effect", Color.yellow, FlightController.CosmeticFlip,
                    (value) => { FlightController.CosmeticFlip = value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateFloat("Matrix Count", Color.magenta, FlightController.EffectMatrixCount, 1f, 1f, 25f,
                    (value) => { FlightController.EffectMatrixCount = (int)value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateFloat("Matrix Spacing", Color.magenta, FlightController.EffectMatrixSpacing, 0.1f, 0.1f, 10f,
                    (value) => { FlightController.EffectMatrixSpacing = value; SettingsManager.MarkDirty(); });
                flightCosmeticPage.CreateEnum("Matrix Mode", Color.magenta, FlightController.EffectMatrixMode,
                    (value) => { FlightController.EffectMatrixMode = (MatrixMode)value; SettingsManager.MarkDirty(); });
                var flightCosSearchResults = flightCosmeticPage.CreatePage("+ Search", Color.yellow);
                flightCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                flightCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                flightCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(flightCosSearchResults, (barcode) =>
                    {
                        FlightController.CosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Flight cosmetic set: {barcode}");
                    }));
                // Timing
                flightEffectsPage.CreateFloat("Spawn Delay", Color.cyan, FlightController.EffectSpawnDelay, 0.05f, 0f, 2f,
                    (value) => { FlightController.EffectSpawnDelay = value; SettingsManager.MarkDirty(); });
                flightEffectsPage.CreateFloat("Spawn Interval", Color.cyan, FlightController.EffectSpawnInterval, 0.1f, 0f, 5f,
                    (value) => { FlightController.EffectSpawnInterval = value; SettingsManager.MarkDirty(); });
                // Offset
                var flightOffsetPage = flightEffectsPage.CreatePage("Transform Offset", Color.yellow);
                flightOffsetPage.CreateFloat("Offset X", Color.red, FlightController.EffectOffsetX, 0.1f, -2f, 2f,
                    (value) => { FlightController.EffectOffsetX = value; SettingsManager.MarkDirty(); });
                flightOffsetPage.CreateFloat("Offset Y", Color.green, FlightController.EffectOffsetY, 0.1f, -2f, 2f,
                    (value) => { FlightController.EffectOffsetY = value; SettingsManager.MarkDirty(); });
                flightOffsetPage.CreateFloat("Offset Z", Color.blue, FlightController.EffectOffsetZ, 0.1f, -2f, 2f,
                    (value) => { FlightController.EffectOffsetZ = value; SettingsManager.MarkDirty(); });

                // ── Bunny Hop submenu inside Player ──
                var bhopPage = playerPage.CreatePage("Bunny Hop", Color.cyan);
                bhopPage.CreateBool(
                    "Enabled",
                    Color.white,
                    BunnyHopController.Enabled,
                    (value) => { BunnyHopController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Hop Boost",
                    Color.cyan,
                    BunnyHopController.HopBoost,
                    0.5f,
                    0f,
                    20f,
                    (value) => { BunnyHopController.HopBoost = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Max Speed",
                    Color.yellow,
                    BunnyHopController.MaxSpeed,
                    5f,
                    5f,
                    200f,
                    (value) => { BunnyHopController.MaxSpeed = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateEnum(
                    "Air Strafe Mode",
                    Color.green,
                    BunnyHopController.StrafeMode,
                    (value) => { BunnyHopController.StrafeMode = (AirStrafeMode)value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Air Strafe Force",
                    Color.green,
                    BunnyHopController.AirStrafeForce,
                    1f,
                    0f,
                    50f,
                    (value) => { BunnyHopController.AirStrafeForce = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Jump Force",
                    Color.white,
                    BunnyHopController.JumpForce,
                    0.5f,
                    1f,
                    20f,
                    (value) => { BunnyHopController.JumpForce = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Standable Normal",
                    Color.cyan,
                    BunnyHopController.StandableNormal,
                    0.05f,
                    0f,
                    1f,
                    (value) => { BunnyHopController.StandableNormal = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateBool(
                    "Auto Hop",
                    Color.magenta,
                    BunnyHopController.AutoHop,
                    (value) => { BunnyHopController.AutoHop = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateBool(
                    "Trimping",
                    Color.yellow,
                    BunnyHopController.TrimpEnabled,
                    (value) => { BunnyHopController.TrimpEnabled = value; SettingsManager.MarkDirty(); }
                );
                bhopPage.CreateFloat(
                    "Trimp Multiplier",
                    Color.yellow,
                    BunnyHopController.TrimpMultiplier,
                    0.1f,
                    0f,
                    3f,
                    (value) => { BunnyHopController.TrimpMultiplier = value; SettingsManager.MarkDirty(); }
                );

                // ── Spinbot submenu inside Player ──
                var spinbotPage = playerPage.CreatePage("Spinbot", Color.magenta);
                spinbotPage.CreateBool(
                    "Enabled",
                    Color.white,
                    SpinbotController.Enabled,
                    (value) => { SpinbotController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                spinbotPage.CreateFloat(
                    "Speed (°/s)",
                    Color.cyan,
                    SpinbotController.Speed,
                    90f,
                    10f,
                    7200f,
                    (value) => { SpinbotController.Speed = value; SettingsManager.MarkDirty(); }
                );
                spinbotPage.CreateEnum(
                    "Direction",
                    Color.green,
                    SpinbotController.Direction,
                    (value) => { SpinbotController.Direction = (SpinDirection)value; SettingsManager.MarkDirty(); }
                );

                // Ragdoll submenu inside Player
                var ragdollPage = playerPage.CreatePage("Ragdoll", Color.red);
                ragdollPage.CreateBool(
                    "Enabled",
                    Color.white,
                    RagdollController.Enabled,
                    (value) => { RagdollController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollPage.CreateBool(
                    "Grab Ragdoll",
                    Color.yellow,
                    RagdollController.GrabEnabled,
                    (value) => { RagdollController.GrabEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollPage.CreateEnum(
                    "Mode",
                    Color.cyan,
                    RagdollController.Mode,
                    (value) => { RagdollController.Mode = (RagdollMode)value; SettingsManager.MarkDirty(); }
                );
                ragdollPage.CreateBool(
                    "Tantrum Mode",
                    Color.red,
                    RagdollController.TantrumMode,
                    (value) => { RagdollController.TantrumMode = value; SettingsManager.MarkDirty(); }
                );
                ragdollPage.CreateEnum(
                    "Keybind",
                    Color.green,
                    RagdollController.Binding,
                    (value) => { RagdollController.Binding = (RagdollBinding)value; SettingsManager.MarkDirty(); }
                );
                ragdollPage.CreateEnum(
                    "Keybind Hand",
                    Color.green,
                    RagdollController.KeybindHand,
                    (value) => { RagdollController.KeybindHand = (RagdollHand)value; SettingsManager.MarkDirty(); }
                );

                // Grab settings sub-page
                var grabSettingsPage = ragdollPage.CreatePage("Grab Settings", Color.yellow);
                grabSettingsPage.CreateBool(
                    "Neck Grab Disables Arms",
                    Color.red,
                    RagdollController.NeckGrabDisablesArms,
                    (value) => { RagdollController.NeckGrabDisablesArms = value; SettingsManager.MarkDirty(); }
                );
                grabSettingsPage.CreateBool(
                    "Arm Grab (2.5x Mass)",
                    Color.magenta,
                    RagdollController.ArmGrabEnabled,
                    (value) => { RagdollController.ArmGrabEnabled = value; SettingsManager.MarkDirty(); }
                );

                // Fall settings inside Ragdoll
                var ragdollFallPage = ragdollPage.CreatePage("Fall", Color.yellow);
                ragdollFallPage.CreateBool(
                    "Fall Enabled",
                    Color.yellow,
                    RagdollController.FallEnabled,
                    (value) => { RagdollController.FallEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollFallPage.CreateFloat(
                    "Fall Velocity Threshold",
                    Color.yellow,
                    RagdollController.FallVelocityThreshold,
                    0.5f,
                    1f,
                    50f,
                    (value) => { RagdollController.FallVelocityThreshold = value; SettingsManager.MarkDirty(); }
                );

                // Impact settings inside Ragdoll
                var ragdollImpactPage = ragdollPage.CreatePage("Impact", Color.cyan);
                ragdollImpactPage.CreateBool(
                    "Impact Enabled",
                    Color.cyan,
                    RagdollController.ImpactEnabled,
                    (value) => { RagdollController.ImpactEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollImpactPage.CreateFloat(
                    "Impact Threshold",
                    Color.cyan,
                    RagdollController.ImpactThreshold,
                    1f,
                    1f,
                    50f,
                    (value) => { RagdollController.ImpactThreshold = value; SettingsManager.MarkDirty(); }
                );

                // Launch settings inside Ragdoll
                var ragdollLaunchPage = ragdollPage.CreatePage("Launch", Color.magenta);
                ragdollLaunchPage.CreateBool(
                    "Launch Enabled",
                    Color.magenta,
                    RagdollController.LaunchEnabled,
                    (value) => { RagdollController.LaunchEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollLaunchPage.CreateFloat(
                    "Launch Threshold",
                    Color.magenta,
                    RagdollController.LaunchThreshold,
                    1f,
                    1f,
                    50f,
                    (value) => { RagdollController.LaunchThreshold = value; SettingsManager.MarkDirty(); }
                );

                // Slip settings inside Ragdoll
                var ragdollSlipPage = ragdollPage.CreatePage("Slip", Color.blue);
                ragdollSlipPage.CreateBool(
                    "Slip Enabled",
                    Color.blue,
                    RagdollController.SlipEnabled,
                    (value) => { RagdollController.SlipEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollSlipPage.CreateFloat(
                    "Friction Threshold",
                    Color.blue,
                    RagdollController.SlipFrictionThreshold,
                    0.01f,
                    0.01f,
                    1f,
                    (value) => { RagdollController.SlipFrictionThreshold = value; SettingsManager.MarkDirty(); }
                );
                ragdollSlipPage.CreateFloat(
                    "Velocity Threshold",
                    Color.blue,
                    RagdollController.SlipVelocityThreshold,
                    0.5f,
                    0.5f,
                    20f,
                    (value) => { RagdollController.SlipVelocityThreshold = value; SettingsManager.MarkDirty(); }
                );

                // Wall Push settings inside Ragdoll
                var ragdollWallPage = ragdollPage.CreatePage("Wall Push", Color.gray);
                ragdollWallPage.CreateBool(
                    "Wall Push Enabled",
                    Color.gray,
                    RagdollController.WallPushEnabled,
                    (value) => { RagdollController.WallPushEnabled = value; SettingsManager.MarkDirty(); }
                );
                ragdollWallPage.CreateFloat(
                    "Push Velocity Threshold",
                    Color.gray,
                    RagdollController.WallPushVelocityThreshold,
                    0.1f,
                    0.1f,
                    20f,
                    (value) => { RagdollController.WallPushVelocityThreshold = value; SettingsManager.MarkDirty(); }
                );

                ragdollPage.CreateFunction(
                    "Unragdoll Now",
                    Color.green,
                    () =>
                    {
                        try
                        {
                            var physRig = BoneLib.Player.RigManager?.physicsRig;
                            if (physRig != null) { RagdollController.UnragdollPlayer(physRig); }
                        }
                        catch { }
                    }
                );

                // ============================================
                // COMBAT submenu (Full Auto, Ammo, Explosive Punch)
                // ============================================
                var combatPage = _mainPage.CreatePage("Combat", Color.red);

                // ── Gun Modifier submenu inside Combat ──
                var gunModifierPage = combatPage.CreatePage("Gun Modifier", Color.cyan);

                // ── Damage Multiplier submenu inside Combat ──
                var damageMultPage = combatPage.CreatePage("Damage Multiplier", Color.red);
                damageMultPage.CreateFloat(
                    "Gun Multiplier",
                    Color.yellow,
                    DamageMultiplierController.GunMultiplier,
                    0.5f,
                    0.1f,
                    100f,
                    (value) => { DamageMultiplierController.GunMultiplier = value; SettingsManager.MarkDirty(); }
                );
                damageMultPage.CreateFloat(
                    "Melee Multiplier",
                    Color.red,
                    DamageMultiplierController.MeleeMultiplier,
                    0.5f,
                    0.1f,
                    100f,
                    (value) => { DamageMultiplierController.MeleeMultiplier = value; SettingsManager.MarkDirty(); }
                );
                damageMultPage.CreateFunction(
                    "Apply Now",
                    Color.green,
                    () => DamageMultiplierController.ApplyMultipliersNow()
                );
                damageMultPage.CreateFunction(
                    "Reset to 1x",
                    Color.white,
                    () =>
                    {
                        DamageMultiplierController.GunMultiplier = 1f;
                        DamageMultiplierController.MeleeMultiplier = 1f;
                        SettingsManager.MarkDirty();
                    }
                );

                combatPage.CreateBool(
                    "Full Auto Guns",
                    Color.yellow,
                    FullAutoController.IsFullAutoEnabled,
                    (value) => { FullAutoController.IsFullAutoEnabled = value; SettingsManager.MarkDirty(); }
                );
                combatPage.CreateBool(
                    "Infinite Ammo",
                    Color.green,
                    InfiniteAmmoController.IsEnabled,
                    (value) => { InfiniteAmmoController.IsEnabled = value; SettingsManager.MarkDirty(); }
                );
                gunModifierPage.CreateBool("Glow Blue Guns", Color.blue, ChaosGunController.PurpleGuns,
                    (value) => { ChaosGunController.PurpleGuns = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("Insane Damage!", Color.red, ChaosGunController.InsaneDamage,
                    (value) => { ChaosGunController.InsaneDamage = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("No Recoil!", Color.red, ChaosGunController.NoRecoil,
                    (value) => { ChaosGunController.NoRecoil = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("Insane Firerate", Color.red, ChaosGunController.InsaneFirerate,
                    (value) => { ChaosGunController.InsaneFirerate = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("No Weight!", Color.red, ChaosGunController.NoWeight,
                    (value) => { ChaosGunController.NoWeight = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("Guns Bounce!", Color.red, ChaosGunController.GunsBounce,
                    (value) => { ChaosGunController.GunsBounce = value; SettingsManager.MarkDirty(); });
                gunModifierPage.CreateBool("No Reload", Color.red, ChaosGunController.NoReload,
                    (value) => { ChaosGunController.NoReload = value; SettingsManager.MarkDirty(); });

                // ============================================
                // EXPLOSIVE PUNCH
                // ============================================
                var punchPage = combatPage.CreatePage("Explosive Punch", Color.magenta);

                // ── Hand Mode at the top — determines which submenus are relevant ──
                punchPage.CreateEnum("Hand Mode", Color.cyan, ExplosivePunchController.PunchMode,
                    (value) => { ExplosivePunchController.PunchMode = (PunchHandMode)value; SettingsManager.MarkDirty(); });

                // ── Settings (tuning) ──
                var punchSettingsPage = punchPage.CreatePage("Settings", Color.white);
                punchSettingsPage.CreateFloat("Punch Speed", Color.magenta, ExplosivePunchController.PunchVelocityThreshold, 1f, 1f, 15f,
                    (value) => { ExplosivePunchController.PunchVelocityThreshold = value; SettingsManager.MarkDirty(); });
                punchSettingsPage.CreateFloat("Cooldown", Color.magenta, ExplosivePunchController.PunchCooldown, 0.05f, 0.05f, 1f,
                    (value) => { ExplosivePunchController.PunchCooldown = value; SettingsManager.MarkDirty(); });
                punchSettingsPage.CreateFloat("Spawn Delay", Color.cyan, ExplosivePunchController.SpawnDelay, 0.05f, 0f, 0.5f,
                    (value) => { ExplosivePunchController.SpawnDelay = value; SettingsManager.MarkDirty(); });
                punchSettingsPage.CreateBool("Rig Check (Skip World)", Color.yellow, ExplosivePunchController.RigCheckOnly,
                    (value) => { ExplosivePunchController.RigCheckOnly = value; SettingsManager.MarkDirty(); });
                punchSettingsPage.CreateBool("Face Target", Color.green, ExplosivePunchController.FaceTarget,
                    (value) => { ExplosivePunchController.FaceTarget = value; SettingsManager.MarkDirty(); });
                punchSettingsPage.CreateBool("Legacy Punch", Color.green, ExplosivePunchController.IsLegacyPunchEnabled,
                    (value) => { ExplosivePunchController.IsLegacyPunchEnabled = value; SettingsManager.MarkDirty(); });

                // ── Spawn Matrix ──
                var punchMatrixPage = punchPage.CreatePage("Spawn Matrix", Color.yellow);
                punchMatrixPage.CreateFloat("Count", Color.yellow, ExplosivePunchController.PunchSpawnCount, 1f, 1f, 25f,
                    (value) => { ExplosivePunchController.PunchSpawnCount = (int)value; SettingsManager.MarkDirty(); });
                punchMatrixPage.CreateFloat("Spacing", Color.yellow, ExplosivePunchController.PunchSpacing, 0.1f, 0.1f, 10f,
                    (value) => { ExplosivePunchController.PunchSpacing = value; SettingsManager.MarkDirty(); });
                punchMatrixPage.CreateEnum("Pattern", Color.yellow, ExplosivePunchController.PunchMatrixMode,
                    (value) => { ExplosivePunchController.PunchMatrixMode = (MatrixMode)value; SettingsManager.MarkDirty(); });

                // ════════════════════════════════════════════
                // BOTH HANDS (Hand Mode = BOTH)
                // ════════════════════════════════════════════
                var bothHandsPage = punchPage.CreatePage("Both Hands", Color.magenta);

                // Explosion type toggles
                bothHandsPage.CreateBool("Explosive Punch", Color.magenta, ExplosivePunchController.IsExplosivePunchEnabled,
                    (value) => { ExplosivePunchController.IsExplosivePunchEnabled = value; SettingsManager.MarkDirty(); });
                bothHandsPage.CreateBool("Super Explosive", Color.red, ExplosivePunchController.IsSuperExplosivePunchEnabled,
                    (value) => { ExplosivePunchController.IsSuperExplosivePunchEnabled = value; SettingsManager.MarkDirty(); });
                bothHandsPage.CreateBool("BLACKFLASH", Color.black, ExplosivePunchController.IsBlackFlashEnabled,
                    (value) => { ExplosivePunchController.IsBlackFlashEnabled = value; SettingsManager.MarkDirty(); });
                bothHandsPage.CreateBool("Tiny Explosive", Color.yellow, ExplosivePunchController.IsTinyExplosiveEnabled,
                    (value) => { ExplosivePunchController.IsTinyExplosiveEnabled = value; SettingsManager.MarkDirty(); });
                bothHandsPage.CreateBool("BOOM", Color.red, ExplosivePunchController.IsBoomEnabled,
                    (value) => { ExplosivePunchController.IsBoomEnabled = value; SettingsManager.MarkDirty(); });

                // Custom Punch
                var customPunchPage = bothHandsPage.CreatePage("Custom Punch", Color.cyan);
                customPunchPage.CreateBool("Enabled", Color.white, ExplosivePunchController.IsCustomPunchEnabled,
                    (value) => { ExplosivePunchController.IsCustomPunchEnabled = value; SettingsManager.MarkDirty(); });
                customPunchPage.CreateString("Barcode", Color.cyan, ExplosivePunchController.CustomPunchBarcode,
                    (value) => { ExplosivePunchController.CustomPunchBarcode = value; SettingsManager.MarkDirty(); });
                customPunchPage.CreateFunction("Set From Left Hand", Color.yellow, () => SetCustomPunchFromHand(true));
                customPunchPage.CreateFunction("Set From Right Hand", Color.yellow, () => SetCustomPunchFromHand(false));
                var customPunchSearchResults = customPunchPage.CreatePage("Search Results", Color.yellow);
                customPunchPage.CreateEnum("Search Action", Color.cyan, SpawnableSearcher.CurrentSpawnType,
                    (value) => SpawnableSearcher.CurrentSpawnType = (SpawnableSearcher.SpawnableSearchType)value);
                customPunchPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                customPunchPage.CreateString("Match", Color.white, "",
                    (value) => SpawnableSearcher.SearchQuery = value);
                customPunchPage.CreateFunction("Find Results", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(customPunchSearchResults, (barcode) =>
                    {
                        ExplosivePunchController.CustomPunchBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Custom Punch set: {barcode}");
                    }));
                customPunchPage.CreateBool("Exclude Ammo/Mags", Color.yellow, SpawnableSearcher.ExcludeAmmo,
                    (value) => SpawnableSearcher.ExcludeAmmo = value);

                // SmashBone
                var bothSmashPage = bothHandsPage.CreatePage("SmashBone", Color.red);
                bothSmashPage.CreateBool("Enabled", Color.white, ExplosivePunchController.IsSmashBoneEnabled,
                    (value) => { ExplosivePunchController.IsSmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                bothSmashPage.CreateFloat("Count", Color.red, ExplosivePunchController.SmashBoneCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.SmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                bothSmashPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.SmashBoneFlip,
                    (value) => { ExplosivePunchController.SmashBoneFlip = value; SettingsManager.MarkDirty(); });

                // Cosmetic
                var bothCosmeticPage = bothHandsPage.CreatePage("Cosmetic", Color.green);
                bothCosmeticPage.CreateBool("Enabled", Color.white, ExplosivePunchController.IsCosmeticEnabled,
                    (value) => { ExplosivePunchController.IsCosmeticEnabled = value; SettingsManager.MarkDirty(); });
                bothCosmeticPage.CreateString("Barcode", Color.cyan, ExplosivePunchController.CosmeticBarcode,
                    (value) => { ExplosivePunchController.CosmeticBarcode = value; SettingsManager.MarkDirty(); });
                bothCosmeticPage.CreateFloat("Count", Color.magenta, ExplosivePunchController.CosmeticCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.CosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                bothCosmeticPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.CosmeticFlip,
                    (value) => { ExplosivePunchController.CosmeticFlip = value; SettingsManager.MarkDirty(); });
                var bothCosSearchResults = bothCosmeticPage.CreatePage("Search", Color.yellow);
                bothCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                bothCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                bothCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(bothCosSearchResults, (barcode) =>
                    {
                        ExplosivePunchController.CosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Cosmetic set: {barcode}");
                    }));

                // ════════════════════════════════════════════
                // LEFT HAND (Hand Mode = SEPARATE)
                // ════════════════════════════════════════════
                var leftHandPage = punchPage.CreatePage("Left Hand", Color.blue);
                leftHandPage.CreateEnum("Explosion Type", Color.magenta, ExplosivePunchController.LeftExplosionType,
                    (value) => { ExplosivePunchController.LeftExplosionType = (ExplosionType)value; SettingsManager.MarkDirty(); });
                leftHandPage.CreateString("Custom Barcode", Color.cyan, ExplosivePunchController.LeftCustomBarcode,
                    (value) => { ExplosivePunchController.LeftCustomBarcode = value; SettingsManager.MarkDirty(); });

                // Left SmashBone
                var leftSmashPage = leftHandPage.CreatePage("SmashBone", Color.red);
                leftSmashPage.CreateBool("Enabled", Color.white, ExplosivePunchController.LeftSmashBoneEnabled,
                    (value) => { ExplosivePunchController.LeftSmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                leftSmashPage.CreateFloat("Count", Color.red, ExplosivePunchController.LeftSmashBoneCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.LeftSmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                leftSmashPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.LeftSmashBoneFlip,
                    (value) => { ExplosivePunchController.LeftSmashBoneFlip = value; SettingsManager.MarkDirty(); });

                // Left Cosmetic
                var leftCosmeticPage = leftHandPage.CreatePage("Cosmetic", Color.green);
                leftCosmeticPage.CreateBool("Enabled", Color.white, ExplosivePunchController.LeftCosmeticEnabled,
                    (value) => { ExplosivePunchController.LeftCosmeticEnabled = value; SettingsManager.MarkDirty(); });
                leftCosmeticPage.CreateString("Barcode", Color.cyan, ExplosivePunchController.LeftCosmeticBarcode,
                    (value) => { ExplosivePunchController.LeftCosmeticBarcode = value; SettingsManager.MarkDirty(); });
                leftCosmeticPage.CreateFloat("Count", Color.magenta, ExplosivePunchController.LeftCosmeticCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.LeftCosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                leftCosmeticPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.LeftCosmeticFlip,
                    (value) => { ExplosivePunchController.LeftCosmeticFlip = value; SettingsManager.MarkDirty(); });
                var leftCosSearchResults = leftCosmeticPage.CreatePage("Search", Color.yellow);
                leftCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                leftCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                leftCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(leftCosSearchResults, (barcode) =>
                    {
                        ExplosivePunchController.LeftCosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Left cosmetic set: {barcode}");
                    }));

                // ════════════════════════════════════════════
                // RIGHT HAND (Hand Mode = SEPARATE)
                // ════════════════════════════════════════════
                var rightHandPage = punchPage.CreatePage("Right Hand", Color.red);
                rightHandPage.CreateEnum("Explosion Type", Color.magenta, ExplosivePunchController.RightExplosionType,
                    (value) => { ExplosivePunchController.RightExplosionType = (ExplosionType)value; SettingsManager.MarkDirty(); });
                rightHandPage.CreateString("Custom Barcode", Color.cyan, ExplosivePunchController.RightCustomBarcode,
                    (value) => { ExplosivePunchController.RightCustomBarcode = value; SettingsManager.MarkDirty(); });

                // Right SmashBone
                var rightSmashPage = rightHandPage.CreatePage("SmashBone", Color.red);
                rightSmashPage.CreateBool("Enabled", Color.white, ExplosivePunchController.RightSmashBoneEnabled,
                    (value) => { ExplosivePunchController.RightSmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                rightSmashPage.CreateFloat("Count", Color.red, ExplosivePunchController.RightSmashBoneCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.RightSmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                rightSmashPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.RightSmashBoneFlip,
                    (value) => { ExplosivePunchController.RightSmashBoneFlip = value; SettingsManager.MarkDirty(); });

                // Right Cosmetic
                var rightCosmeticPage = rightHandPage.CreatePage("Cosmetic", Color.green);
                rightCosmeticPage.CreateBool("Enabled", Color.white, ExplosivePunchController.RightCosmeticEnabled,
                    (value) => { ExplosivePunchController.RightCosmeticEnabled = value; SettingsManager.MarkDirty(); });
                rightCosmeticPage.CreateString("Barcode", Color.cyan, ExplosivePunchController.RightCosmeticBarcode,
                    (value) => { ExplosivePunchController.RightCosmeticBarcode = value; SettingsManager.MarkDirty(); });
                rightCosmeticPage.CreateFloat("Count", Color.magenta, ExplosivePunchController.RightCosmeticCount, 1f, 1f, 20f,
                    (value) => { ExplosivePunchController.RightCosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                rightCosmeticPage.CreateBool("Flip", Color.yellow, ExplosivePunchController.RightCosmeticFlip,
                    (value) => { ExplosivePunchController.RightCosmeticFlip = value; SettingsManager.MarkDirty(); });
                var rightCosSearchResults = rightCosmeticPage.CreatePage("Search", Color.yellow);
                rightCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                rightCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                rightCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(rightCosSearchResults, (barcode) =>
                    {
                        ExplosivePunchController.RightCosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Right cosmetic set: {barcode}");
                    }));

                // ============================================
                // RANDOM EXPLODE submenu (inside Combat)
                // ============================================
                var randomExplodePage = combatPage.CreatePage("Random Explode", Color.red);
                randomExplodePage.CreateBool("Enabled", Color.white, RandomExplodeController.Enabled,
                    (value) => { RandomExplodeController.Enabled = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateEnum("Explosion Type", Color.magenta, RandomExplodeController.SelectedExplosion,
                    (value) => { RandomExplodeController.SelectedExplosion = (ExplosionType)value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateString("Custom Barcode", Color.cyan, RandomExplodeController.CustomBarcode,
                    (value) => { RandomExplodeController.CustomBarcode = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateFloat("Interval (sec)", Color.yellow, RandomExplodeController.Interval,
                    0.1f, 0.1f, 60f,
                    (value) => { RandomExplodeController.Interval = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateInt("Chance (1 in N)", Color.yellow, RandomExplodeController.ChanceDenominator,
                    50, 1, 1000000,
                    (value) => { RandomExplodeController.ChanceDenominator = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateFloat("Launch Force", Color.red, RandomExplodeController.LaunchForce,
                    10f, 0f, 1000f,
                    (value) => { RandomExplodeController.LaunchForce = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateEnum("Launch Direction", Color.cyan, RandomExplodeController.LaunchDirection,
                    (value) => { RandomExplodeController.LaunchDirection = (ExplodeLaunchDir)value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateBool("Ragdoll on Explode", Color.yellow, RandomExplodeController.RagdollOnExplode,
                    (value) => { RandomExplodeController.RagdollOnExplode = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateEnum("Target", Color.magenta, RandomExplodeController.Target,
                    (value) => { RandomExplodeController.Target = (ExplodeTarget)value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateBool("Controller Shortcut (B+Y)", Color.cyan, RandomExplodeController.ControllerShortcut,
                    (value) => { RandomExplodeController.ControllerShortcut = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateFloat("Hold Duration (sec)", Color.cyan, RandomExplodeController.HoldDuration,
                    0.1f, 0.1f, 5f,
                    (value) => { RandomExplodeController.HoldDuration = value; SettingsManager.MarkDirty(); });
                randomExplodePage.CreateFunction("Test Explosion Now", Color.red,
                    () => RandomExplodeController.TriggerExplosion());
                var reSearchResults = randomExplodePage.CreatePage("+ Search Custom", Color.green);
                randomExplodePage.CreateString("Search Query", Color.white, RandomExplodeController.SearchQuery,
                    (value) => { RandomExplodeController.SearchQuery = value; });
                randomExplodePage.CreateFunction("Search", Color.yellow,
                    () => RandomExplodeController.SearchSpawnables(reSearchResults));

                // ============================================
                // GROUND SLAM submenu (inside Combat)
                // ============================================
                var groundPoundPage = combatPage.CreatePage("Ground Slam", Color.yellow);
                groundPoundPage.CreateBool("Enabled", Color.white, GroundPoundController.Enabled,
                    (value) => { GroundPoundController.Enabled = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("Velocity Threshold", Color.cyan, GroundPoundController.VelocityThreshold,
                    1f, 1f, 50f,
                    (value) => { GroundPoundController.VelocityThreshold = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("Cooldown", Color.cyan, GroundPoundController.Cooldown,
                    0.05f, 0.05f, 5f,
                    (value) => { GroundPoundController.Cooldown = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("Spawn Delay", Color.cyan, GroundPoundController.SpawnDelay,
                    0.05f, 0f, 2f,
                    (value) => { GroundPoundController.SpawnDelay = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateEnum("Explosion Type", Color.magenta, GroundPoundController.SelectedExplosion,
                    (value) => { GroundPoundController.SelectedExplosion = (ExplosionType)value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateString("Custom Barcode", Color.cyan, GroundPoundController.CustomBarcode,
                    (value) => { GroundPoundController.CustomBarcode = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("Matrix Count", Color.yellow, GroundPoundController.MatrixCount,
                    1f, 1f, 25f,
                    (value) => { GroundPoundController.MatrixCount = (int)value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("Matrix Spacing", Color.yellow, GroundPoundController.MatrixSpacing,
                    0.1f, 0.1f, 10f,
                    (value) => { GroundPoundController.MatrixSpacing = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateEnum("Matrix Mode", Color.yellow, GroundPoundController.SelectedMatrixMode,
                    (value) => { GroundPoundController.SelectedMatrixMode = (MatrixMode)value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateBool("SmashBone Effect", Color.red, GroundPoundController.SmashBoneEnabled,
                    (value) => { GroundPoundController.SmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateFloat("SmashBone Count", Color.red, GroundPoundController.SmashBoneCount,
                    1f, 1f, 20f,
                    (value) => { GroundPoundController.SmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                groundPoundPage.CreateBool("SmashBone Flip", Color.yellow, GroundPoundController.SmashBoneFlip,
                    (value) => { GroundPoundController.SmashBoneFlip = value; SettingsManager.MarkDirty(); });
                var gpCosmeticPage = groundPoundPage.CreatePage("Cosmetic Effect", Color.green);
                gpCosmeticPage.CreateBool("Enabled", Color.white, GroundPoundController.CosmeticEnabled,
                    (value) => { GroundPoundController.CosmeticEnabled = value; SettingsManager.MarkDirty(); });
                gpCosmeticPage.CreateString("Barcode", Color.cyan, GroundPoundController.CosmeticBarcode,
                    (value) => { GroundPoundController.CosmeticBarcode = value; SettingsManager.MarkDirty(); });
                gpCosmeticPage.CreateFloat("Count", Color.magenta, GroundPoundController.CosmeticCount,
                    1f, 1f, 20f,
                    (value) => { GroundPoundController.CosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                gpCosmeticPage.CreateBool("Flip Effect", Color.yellow, GroundPoundController.CosmeticFlip,
                    (value) => { GroundPoundController.CosmeticFlip = value; SettingsManager.MarkDirty(); });
                var gpCosSearchResults = gpCosmeticPage.CreatePage("+ Search", Color.yellow);
                gpCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                gpCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                gpCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(gpCosSearchResults, (barcode) =>
                    {
                        GroundPoundController.CosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"GP Cosmetic set: {barcode}");
                    }));
                var gpCustomSearchResults = groundPoundPage.CreatePage("+ Search Custom", Color.yellow);
                groundPoundPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                groundPoundPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                groundPoundPage.CreateFunction("Find Custom", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(gpCustomSearchResults, (barcode) =>
                    {
                        GroundPoundController.CustomBarcode = barcode;
                        GroundPoundController.SelectedExplosion = ExplosionType.Custom;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"GP Custom set: {barcode}");
                    }));

                // ============================================
                // EXPLOSIVE IMPACT submenu (inside Combat)
                // ============================================
                var explosiveImpactPage = combatPage.CreatePage("Explosive Impact", Color.red);
                explosiveImpactPage.CreateBool("Enabled", Color.white, ExplosiveImpactController.Enabled,
                    (value) => { ExplosiveImpactController.Enabled = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("Velocity Threshold", Color.cyan, ExplosiveImpactController.VelocityThreshold,
                    1f, 1f, 50f,
                    (value) => { ExplosiveImpactController.VelocityThreshold = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("Cooldown", Color.cyan, ExplosiveImpactController.Cooldown,
                    0.05f, 0.05f, 5f,
                    (value) => { ExplosiveImpactController.Cooldown = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("Spawn Delay", Color.cyan, ExplosiveImpactController.SpawnDelay,
                    0.05f, 0f, 2f,
                    (value) => { ExplosiveImpactController.SpawnDelay = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateEnum("Explosion Type", Color.magenta, ExplosiveImpactController.SelectedExplosion,
                    (value) => { ExplosiveImpactController.SelectedExplosion = (ExplosionType)value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateString("Custom Barcode", Color.cyan, ExplosiveImpactController.CustomBarcode,
                    (value) => { ExplosiveImpactController.CustomBarcode = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("Matrix Count", Color.yellow, ExplosiveImpactController.MatrixCount,
                    1f, 1f, 25f,
                    (value) => { ExplosiveImpactController.MatrixCount = (int)value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("Matrix Spacing", Color.yellow, ExplosiveImpactController.MatrixSpacing,
                    0.1f, 0.1f, 10f,
                    (value) => { ExplosiveImpactController.MatrixSpacing = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateEnum("Matrix Mode", Color.yellow, ExplosiveImpactController.SelectedMatrixMode,
                    (value) => { ExplosiveImpactController.SelectedMatrixMode = (MatrixMode)value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateBool("SmashBone Effect", Color.red, ExplosiveImpactController.SmashBoneEnabled,
                    (value) => { ExplosiveImpactController.SmashBoneEnabled = value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateFloat("SmashBone Count", Color.red, ExplosiveImpactController.SmashBoneCount,
                    1f, 1f, 20f,
                    (value) => { ExplosiveImpactController.SmashBoneCount = (int)value; SettingsManager.MarkDirty(); });
                explosiveImpactPage.CreateBool("SmashBone Flip", Color.yellow, ExplosiveImpactController.SmashBoneFlip,
                    (value) => { ExplosiveImpactController.SmashBoneFlip = value; SettingsManager.MarkDirty(); });
                var eiCosmeticPage = explosiveImpactPage.CreatePage("Cosmetic Effect", Color.green);
                eiCosmeticPage.CreateBool("Enabled", Color.white, ExplosiveImpactController.CosmeticEnabled,
                    (value) => { ExplosiveImpactController.CosmeticEnabled = value; SettingsManager.MarkDirty(); });
                eiCosmeticPage.CreateString("Barcode", Color.cyan, ExplosiveImpactController.CosmeticBarcode,
                    (value) => { ExplosiveImpactController.CosmeticBarcode = value; SettingsManager.MarkDirty(); });
                eiCosmeticPage.CreateFloat("Count", Color.magenta, ExplosiveImpactController.CosmeticCount,
                    1f, 1f, 20f,
                    (value) => { ExplosiveImpactController.CosmeticCount = (int)value; SettingsManager.MarkDirty(); });
                eiCosmeticPage.CreateBool("Flip Effect", Color.yellow, ExplosiveImpactController.CosmeticFlip,
                    (value) => { ExplosiveImpactController.CosmeticFlip = value; SettingsManager.MarkDirty(); });
                var eiCosSearchResults = eiCosmeticPage.CreatePage("+ Search", Color.yellow);
                eiCosmeticPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                eiCosmeticPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                eiCosmeticPage.CreateFunction("Find Items", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(eiCosSearchResults, (barcode) =>
                    {
                        ExplosiveImpactController.CosmeticBarcode = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"EI Cosmetic set: {barcode}");
                    }));
                var eiCustomSearchResults = explosiveImpactPage.CreatePage("+ Search Custom", Color.yellow);
                explosiveImpactPage.CreateEnum("Search Method", Color.magenta, SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value);
                explosiveImpactPage.CreateString("Search Query", Color.white, "",
                    (value) => { SpawnableSearcher.SearchQuery = value; });
                explosiveImpactPage.CreateFunction("Find Custom", Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(eiCustomSearchResults, (barcode) =>
                    {
                        ExplosiveImpactController.CustomBarcode = barcode;
                        ExplosiveImpactController.SelectedExplosion = ExplosionType.Custom;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"EI Custom set: {barcode}");
                    }));

                // ============================================
                // OBJECT LAUNCHER submenu
                // ============================================
                var launcherPage = combatPage.CreatePage("Object Launcher", Color.cyan);
                launcherPage.CreateBool(
                    "Enabled",
                    Color.white,
                    ObjectLauncherController.IsLauncherEnabled,
                    (value) => { ObjectLauncherController.IsLauncherEnabled = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Safety (Grip+Trigger)",
                    Color.red,
                    ObjectLauncherController.SafetyEnabled,
                    (value) => { ObjectLauncherController.SafetyEnabled = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Left Hand Mode",
                    Color.yellow,
                    ObjectLauncherController.UseLeftHand,
                    (value) => { ObjectLauncherController.UseLeftHand = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Full-Auto Mode",
                    Color.yellow,
                    ObjectLauncherController.IsFullAuto,
                    (value) => { ObjectLauncherController.IsFullAuto = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Show Trajectory",
                    Color.white,
                    ObjectLauncherController.ShowTrajectory,
                    (value) => { ObjectLauncherController.ShowTrajectory = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Full-Auto Delay",
                    Color.yellow,
                    ObjectLauncherController.FullAutoDelay,
                    0.01f,
                    0.01f,
                    1f,
                    (value) => { ObjectLauncherController.FullAutoDelay = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Launch Force",
                    Color.yellow,
                    ObjectLauncherController.LaunchForce,
                    10f,
                    10f,
                    10000f,
                    (value) => { ObjectLauncherController.LaunchForce = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Spawn Distance",
                    Color.cyan,
                    ObjectLauncherController.SpawnDistance,
                    0.5f,
                    0.5f,
                    10f,
                    (value) => { ObjectLauncherController.SpawnDistance = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Spawn Offset X",
                    Color.cyan,
                    ObjectLauncherController.SpawnOffsetX,
                    0.5f,
                    -10f,
                    10f,
                    (value) => { ObjectLauncherController.SpawnOffsetX = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Spawn Offset Y",
                    Color.cyan,
                    ObjectLauncherController.SpawnOffsetY,
                    0.5f,
                    -10f,
                    10f,
                    (value) => { ObjectLauncherController.SpawnOffsetY = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Projectile Count",
                    Color.magenta,
                    ObjectLauncherController.ProjectileCount,
                    1f,
                    1f,
                    25f,
                    (value) => { ObjectLauncherController.ProjectileCount = (int)value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Projectile Spacing",
                    Color.magenta,
                    ObjectLauncherController.ProjectileSpacing,
                    0.1f,
                    0.1f,
                    100f,
                    (value) => { ObjectLauncherController.ProjectileSpacing = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Spin Velocity",
                    Color.green,
                    ObjectLauncherController.SpinVelocity,
                    5f,
                    0f,
                    5000f,
                    (value) => { ObjectLauncherController.SpinVelocity = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateFloat(
                    "Spawn Scale",
                    Color.magenta,
                    ObjectLauncherController.SpawnScale,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { ObjectLauncherController.SpawnScale = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Aim Rotation (Face Launch Dir)",
                    Color.green,
                    ObjectLauncherController.AimRotationEnabled,
                    (value) => { ObjectLauncherController.AimRotationEnabled = value; SettingsManager.MarkDirty(); }
                );
                launcherPage.CreateBool(
                    "Pre-Activate (Menu Tap)",
                    Color.magenta,
                    ObjectLauncherController.PreActivateMenuTap,
                    (value) => { ObjectLauncherController.PreActivateMenuTap = value; SettingsManager.MarkDirty(); }
                );

                // Homing submenu
                var homingPage = launcherPage.CreatePage("Homing", Color.red);
                homingPage.CreateBool(
                    "Enabled",
                    Color.white,
                    ObjectLauncherController.HomingEnabled,
                    (value) => { ObjectLauncherController.HomingEnabled = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateEnum(
                    "Filter",
                    Color.red,
                    ObjectLauncherController.HomingFilter,
                    (value) => { ObjectLauncherController.HomingFilter = (TargetFilter)value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Strength",
                    Color.yellow,
                    ObjectLauncherController.HomingStrength,
                    1f,
                    1f,
                    50f,
                    (value) => { ObjectLauncherController.HomingStrength = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Duration (0=unlimited)",
                    Color.cyan,
                    ObjectLauncherController.HomingDuration,
                    0.5f,
                    0f,
                    30f,
                    (value) => { ObjectLauncherController.HomingDuration = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateBool(
                    "Rotation Lock",
                    Color.green,
                    ObjectLauncherController.HomingRotationLock,
                    (value) => { ObjectLauncherController.HomingRotationLock = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Speed (0=auto)",
                    Color.yellow,
                    ObjectLauncherController.HomingSpeed,
                    5f,
                    0f,
                    500f,
                    (value) => { ObjectLauncherController.HomingSpeed = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateBool(
                    "Acceleration",
                    Color.cyan,
                    ObjectLauncherController.HomingAccelEnabled,
                    (value) => { ObjectLauncherController.HomingAccelEnabled = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Accel Rate",
                    Color.cyan,
                    ObjectLauncherController.HomingAccelRate,
                    0.5f,
                    0.1f,
                    10f,
                    (value) => { ObjectLauncherController.HomingAccelRate = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateBool(
                    "Target Head",
                    Color.magenta,
                    ObjectLauncherController.HomingTargetHead,
                    (value) => { ObjectLauncherController.HomingTargetHead = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateBool(
                    "Momentum",
                    Color.green,
                    ObjectLauncherController.HomingMomentum,
                    (value) => { ObjectLauncherController.HomingMomentum = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Stay Duration",
                    Color.yellow,
                    ObjectLauncherController.HomingStayDuration,
                    0.5f,
                    0f,
                    30f,
                    (value) => { ObjectLauncherController.HomingStayDuration = value; SettingsManager.MarkDirty(); }
                );
                homingPage.CreateFloat(
                    "Force Delay",
                    Color.cyan,
                    ObjectLauncherController.ForceDelay,
                    0.01f,
                    0f,
                    1f,
                    (value) => { ObjectLauncherController.ForceDelay = value; SettingsManager.MarkDirty(); }
                );
                BuildSelectPlayerSubMenu(homingPage);

                // Cleanup submenu for Object Launcher
                var cleanupPage = launcherPage.CreatePage("Cleanup", Color.red);
                cleanupPage.CreateFunction(
                    "Despawn All Launched",
                    Color.red,
                    () => ObjectLauncherController.DespawnLaunchedObjects()
                );
                cleanupPage.CreateBool(
                    "Auto Cleanup",
                    Color.yellow,
                    ObjectLauncherController.AutoCleanupEnabled,
                    (value) => { ObjectLauncherController.AutoCleanupEnabled = value; SettingsManager.MarkDirty(); }
                );
                cleanupPage.CreateFloat(
                    "Cleanup Interval (s)",
                    Color.cyan,
                    ObjectLauncherController.AutoCleanupInterval,
                    5f,
                    1f,
                    300f,
                    (value) => { ObjectLauncherController.AutoCleanupInterval = value; SettingsManager.MarkDirty(); }
                );
                cleanupPage.CreateFloat(
                    "Spawn Force Delay",
                    Color.green,
                    ObjectLauncherController.SpawnForceDelay,
                    0.01f,
                    0f,
                    2f,
                    (value) => { ObjectLauncherController.SpawnForceDelay = value; SettingsManager.MarkDirty(); }
                );
                cleanupPage.CreateBool(
                    "Auto Despawn",
                    Color.magenta,
                    ObjectLauncherController.AutoDespawnEnabled,
                    (value) => { ObjectLauncherController.AutoDespawnEnabled = value; SettingsManager.MarkDirty(); }
                );
                cleanupPage.CreateFloat(
                    "Despawn Delay (s)",
                    Color.magenta,
                    ObjectLauncherController.AutoDespawnDelay,
                    1f,
                    1f,
                    300f,
                    (value) => { ObjectLauncherController.AutoDespawnDelay = value; SettingsManager.MarkDirty(); }
                );

                // Rotation submenu for Object Launcher
                var rotationPage = launcherPage.CreatePage("Object Rotation", Color.green);
                rotationPage.CreateFloat(
                    "Rotation X",
                    Color.red,
                    ObjectLauncherController.RotationX,
                    15f,
                    -180f,
                    180f,
                    (value) => { ObjectLauncherController.RotationX = value; SettingsManager.MarkDirty(); }
                );
                rotationPage.CreateFloat(
                    "Rotation Y",
                    Color.green,
                    ObjectLauncherController.RotationY,
                    15f,
                    -180f,
                    180f,
                    (value) => { ObjectLauncherController.RotationY = value; SettingsManager.MarkDirty(); }
                );
                rotationPage.CreateFloat(
                    "Rotation Z",
                    Color.blue,
                    ObjectLauncherController.RotationZ,
                    15f,
                    -180f,
                    180f,
                    (value) => { ObjectLauncherController.RotationZ = value; SettingsManager.MarkDirty(); }
                );
                rotationPage.CreateFunction(
                    "Reset Rotation",
                    Color.white,
                    () =>
                    {
                        ObjectLauncherController.RotationX = 0f;
                        ObjectLauncherController.RotationY = 0f;
                        ObjectLauncherController.RotationZ = 0f;
                        SettingsManager.MarkDirty();
                    }
                );

                // Spawn Search submenu for Object Launcher
                var olSpawnSearchPage = launcherPage.CreatePage("Spawn Search", Color.yellow);
                var olSearchResults = olSpawnSearchPage.CreatePage("+ Results", Color.yellow);
                olSpawnSearchPage.CreateString(
                    "Search Query",
                    Color.white,
                    "",
                    (value) => { SpawnableSearcher.SearchQuery = value; }
                );
                olSpawnSearchPage.CreateFunction(
                    "Find Items",
                    Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(olSearchResults, (barcode) =>
                    {
                        ObjectLauncherController.CurrentBarcodeID = barcode;
                        SettingsManager.MarkDirty();
                        NotificationHelper.Send(NotificationType.Success, $"Launcher set: {barcode}");
                    })
                );

                launcherPage.CreateFunction(
                    "Add Item (Left Hand)",
                    Color.green,
                    ObjectLauncherController.AddItemFromLeftHand
                );
                launcherPage.CreateFunction(
                    "Launch!",
                    Color.red,
                    ObjectLauncherController.LaunchObject
                );

                // ── Object Launcher Presets sub-page ──
                var presetPage = launcherPage.CreatePage("Presets", Color.magenta);
                var presetLoadPage = presetPage.CreatePage("+ Load Preset", Color.green);
                var presetDeletePage = presetPage.CreatePage("+ Delete Preset", Color.red);
                presetPage.CreateString(
                    "Preset Name",
                    Color.white,
                    ObjectLauncherController.PresetName,
                    (value) => ObjectLauncherController.PresetName = value
                );
                presetPage.CreateFunction(
                    "Save Current Settings",
                    Color.green,
                    () =>
                    {
                        ObjectLauncherController.SavePreset(ObjectLauncherController.PresetName);
                        ObjectLauncherController.PopulatePresetLoadPage(presetLoadPage);
                        ObjectLauncherController.PopulatePresetDeletePage(presetDeletePage);
                    }
                );
                presetPage.CreateFunction(
                    "Refresh Preset List",
                    Color.yellow,
                    () =>
                    {
                        ObjectLauncherController.PopulatePresetLoadPage(presetLoadPage);
                        ObjectLauncherController.PopulatePresetDeletePage(presetDeletePage);
                    }
                );

                // ============================================
                // TELEPORT submenu (NO level search - REMOVED)
                // ============================================
                var teleportPage = playerPage.CreatePage("Teleport", Color.green);
                teleportPage.CreateFunction(
                    "Save Position",
                    Color.cyan,
                    TeleportController.SaveCurrentPosition
                );
                teleportPage.CreateFunction(
                    "Teleport to Saved",
                    Color.yellow,
                    TeleportController.TeleportToSavedPosition
                );
                teleportPage.CreateFunction(
                    "Clear Saved Position",
                    Color.red,
                    TeleportController.ClearSavedPosition
                );

                // Teleport to Player submenu
                var playerTeleportPage = teleportPage.CreatePage("Teleport to Player", Color.magenta);
                var playerSearchResults = playerTeleportPage.CreatePage("+ Player Search Results", Color.green);
                playerTeleportPage.CreateString(
                    "Search Name",
                    Color.white,
                    TeleportController.PlayerSearchQuery,
                    (value) => TeleportController.PlayerSearchQuery = value
                );
                playerTeleportPage.CreateFunction(
                    "Find Players",
                    Color.yellow,
                    () => TeleportController.PlayerSearchToPage(playerSearchResults)
                );

                // Waypoints submenu inside Teleport
                var waypointPage = teleportPage.CreatePage("Waypoints", Color.cyan);
                waypointPage.CreateFunction(
                    "Create Waypoint",
                    Color.green,
                    WaypointController.CreateWaypoint
                );
                waypointPage.CreateFloat(
                    "Teleport Hold Time",
                    Color.yellow,
                    WaypointController.TeleportHoldTime,
                    0.5f,
                    0.5f,
                    5f,
                    (value) => WaypointController.TeleportHoldTime = value
                );
                waypointPage.CreateBool(
                    "Controller Shortcut (B+Y)",
                    Color.cyan,
                    WaypointController.ControllerShortcut,
                    (value) => { WaypointController.ControllerShortcut = value; SettingsManager.MarkDirty(); }
                );
                var waypointListPage = waypointPage.CreatePage("+ Waypoint List", Color.green);
                waypointPage.CreateFunction(
                    "Refresh List",
                    Color.yellow,
                    () => WaypointController.PopulateWaypointPage(waypointListPage)
                );
                waypointPage.CreateFunction(
                    "Clear All Waypoints",
                    Color.red,
                    WaypointController.ClearAllWaypoints
                );
                waypointPage.CreateFunction(
                    "Set Default Spawn",
                    Color.green,
                    WaypointController.SetDefaultSpawn
                );
                waypointPage.CreateFunction(
                    "Go To Default Spawn",
                    Color.cyan,
                    WaypointController.TeleportToDefaultSpawn
                );
                waypointPage.CreateFunction(
                    "Clear Default Spawn",
                    Color.red,
                    WaypointController.ClearDefaultSpawn
                );

                // ============================================
                // COSMETICS top-level page
                // ============================================
                var cosmeticsPage = _mainPage.CreatePage("Cosmetics", Color.magenta);

                // ============================================
                // BODYLOG / MENU COLOR submenu
                // ============================================
                var colorPage = cosmeticsPage.CreatePage("BodyLog Color", Color.magenta);
                colorPage.CreateBool("Enabled", Color.white, BodyLogColorController.Enabled,
                    (value) => { BodyLogColorController.Enabled = value; SettingsManager.MarkDirty(); });
                colorPage.CreateFunction("Apply Colors Now", Color.green, () => BodyLogColorController.ApplyAll());

                // Body Log hologram tint
                var bodyLogColorPage = colorPage.CreatePage("Body Log Tint", Color.cyan);
                bodyLogColorPage.CreateFloat("R", Color.red, BodyLogColorController.BodyLogR, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BodyLogR = value; SettingsManager.MarkDirty(); });
                bodyLogColorPage.CreateFloat("G", Color.green, BodyLogColorController.BodyLogG, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BodyLogG = value; SettingsManager.MarkDirty(); });
                bodyLogColorPage.CreateFloat("B", Color.blue, BodyLogColorController.BodyLogB, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BodyLogB = value; SettingsManager.MarkDirty(); });
                bodyLogColorPage.CreateFloat("A", Color.white, BodyLogColorController.BodyLogA, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BodyLogA = value; SettingsManager.MarkDirty(); });

                // Ball (sphere grip)
                var ballColorPage = colorPage.CreatePage("Ball Color", Color.yellow);
                ballColorPage.CreateFloat("R", Color.red, BodyLogColorController.BallR, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BallR = value; SettingsManager.MarkDirty(); });
                ballColorPage.CreateFloat("G", Color.green, BodyLogColorController.BallG, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BallG = value; SettingsManager.MarkDirty(); });
                ballColorPage.CreateFloat("B", Color.blue, BodyLogColorController.BallB, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BallB = value; SettingsManager.MarkDirty(); });
                ballColorPage.CreateFloat("A", Color.white, BodyLogColorController.BallA, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.BallA = value; SettingsManager.MarkDirty(); });

                // Line renderer
                var lineColorPage = colorPage.CreatePage("Line Color", Color.green);
                lineColorPage.CreateFloat("R", Color.red, BodyLogColorController.LineR, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.LineR = value; SettingsManager.MarkDirty(); });
                lineColorPage.CreateFloat("G", Color.green, BodyLogColorController.LineG, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.LineG = value; SettingsManager.MarkDirty(); });
                lineColorPage.CreateFloat("B", Color.blue, BodyLogColorController.LineB, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.LineB = value; SettingsManager.MarkDirty(); });
                lineColorPage.CreateFloat("A", Color.white, BodyLogColorController.LineA, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.LineA = value; SettingsManager.MarkDirty(); });

                // Radial menu
                var radialColorPage = colorPage.CreatePage("Radial Menu Color", Color.red);
                radialColorPage.CreateFloat("R", Color.red, BodyLogColorController.RadialR, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.RadialR = value; SettingsManager.MarkDirty(); });
                radialColorPage.CreateFloat("G", Color.green, BodyLogColorController.RadialG, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.RadialG = value; SettingsManager.MarkDirty(); });
                radialColorPage.CreateFloat("B", Color.blue, BodyLogColorController.RadialB, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.RadialB = value; SettingsManager.MarkDirty(); });
                radialColorPage.CreateFloat("A", Color.white, BodyLogColorController.RadialA, 5f, 0f, 255f,
                    (value) => { BodyLogColorController.RadialA = value; SettingsManager.MarkDirty(); });

                // ============================================
                // HOLSTER HIDER submenu
                // ============================================
                var holsterPage = cosmeticsPage.CreatePage("Holster Hider", Color.yellow);
                holsterPage.CreateBool("Hide Holsters", Color.white, HolsterHiderController.HideHolsters,
                    (value) => { HolsterHiderController.HideHolsters = value; SettingsManager.MarkDirty(); });
                holsterPage.CreateBool("Hide Ammo Pouch", Color.white, HolsterHiderController.HideAmmoPouch,
                    (value) => { HolsterHiderController.HideAmmoPouch = value; SettingsManager.MarkDirty(); });
                holsterPage.CreateBool("Hide Body Log", Color.white, HolsterHiderController.HideBodyLog,
                    (value) => { HolsterHiderController.HideBodyLog = value; SettingsManager.MarkDirty(); });
                holsterPage.CreateFunction("Re-apply Now", Color.green, () => HolsterHiderController.Apply());

                // ============================================
                // COSMETIC PRESETS submenu
                // ============================================
                var cosPresetsPage = cosmeticsPage.CreatePage("Cosmetic Presets", Color.magenta);
                var cosPresetListPage = cosPresetsPage.CreatePage("+ Saved Presets", Color.green);
                cosPresetsPage.CreateFunction("Refresh List", Color.yellow,
                    () => CosmeticPresetController.PopulatePresetsPage(cosPresetListPage));
                string _newPresetName = "";
                cosPresetsPage.CreateString("New Preset Name", Color.white, "",
                    (value) => { _newPresetName = value; });
                cosPresetsPage.CreateFunction("Save Current Cosmetics", Color.green,
                    () =>
                    {
                        CosmeticPresetController.SaveCurrentAsPreset(_newPresetName);
                        CosmeticPresetController.PopulatePresetsPage(cosPresetListPage);
                    });

                // ============================================
                // AVATAR COPIER submenu
                // ============================================
                var avatarCopierPage = cosmeticsPage.CreatePage("Avatar Copier", Color.red);
                var avatarSearchResults = avatarCopierPage.CreatePage("+ Player Search Results", Color.green);
                avatarCopierPage.CreateString(
                    "Search Name",
                    Color.white,
                    AvatarCopierController.SearchQuery,
                    (value) => AvatarCopierController.SearchQuery = value
                );
                avatarCopierPage.CreateFunction(
                    "Find Players",
                    Color.yellow,
                    () => AvatarCopierController.SearchToPage(avatarSearchResults)
                );
                avatarCopierPage.CreateBool(
                    "Copy Nickname Too",
                    Color.cyan,
                    AvatarCopierController.CopyNickname,
                    (value) => AvatarCopierController.CopyNickname = value
                );
                avatarCopierPage.CreateBool(
                    "Copy Cosmetics",
                    Color.magenta,
                    AvatarCopierController.CopyCosmetics,
                    (value) => AvatarCopierController.CopyCosmetics = value
                );
                avatarCopierPage.CreateBool(
                    "Copy Description",
                    Color.yellow,
                    AvatarCopierController.CopyDescription,
                    (value) => AvatarCopierController.CopyDescription = value
                );
                avatarCopierPage.CreateFunction(
                    "Revert Avatar",
                    Color.red,
                    AvatarCopierController.RevertAvatar
                );

                // ── Bodylog Presets (inside Cosmetics) ──
                var bodylogPresetsPage = cosmeticsPage.CreatePage("Bodylog Presets", Color.cyan);
                var bodylogPresetListPage = bodylogPresetsPage.CreatePage("+ Saved Presets", Color.green);
                bodylogPresetsPage.CreateFunction("Refresh List", Color.yellow,
                    () => BodylogPresetController.PopulatePresetsPage(bodylogPresetListPage));
                string _newBodylogPresetName = "";
                bodylogPresetsPage.CreateString("New Preset Name", Color.white, "",
                    (value) => { _newBodylogPresetName = value; });
                bodylogPresetsPage.CreateFunction("Save Current BodyLog", Color.green,
                    () =>
                    {
                        BodylogPresetController.CreatePreset(_newBodylogPresetName);
                        BodylogPresetController.PopulatePresetsPage(bodylogPresetListPage);
                    });

                // ── Weeping Angel (inside Cosmetics) ──
                var weepingAngelPage = cosmeticsPage.CreatePage("Weeping Angel", Color.white);
                weepingAngelPage.CreateBool("Enabled", Color.white, WeepingAngelController.Enabled,
                    (value) => { WeepingAngelController.Enabled = value; SettingsManager.MarkDirty(); });
                weepingAngelPage.CreateBool("Target Everyone", Color.cyan, WeepingAngelController.TargetEveryone,
                    (value) => { WeepingAngelController.TargetEveryone = value; SettingsManager.MarkDirty(); });
                weepingAngelPage.CreateFloat("View Angle", Color.cyan, WeepingAngelController.ViewAngle,
                    5f, 10f, 180f,
                    (value) => { WeepingAngelController.ViewAngle = value; SettingsManager.MarkDirty(); });
                weepingAngelPage.CreateFloat("View Distance", Color.yellow, WeepingAngelController.ViewDistance,
                    10f, 5f, 500f,
                    (value) => { WeepingAngelController.ViewDistance = value; SettingsManager.MarkDirty(); });

                // Player selection for single-player mode
                var waPlayerPage = weepingAngelPage.CreatePage("Select Player", Color.green);
                waPlayerPage.CreateFunction($"Current: {WeepingAngelController.TargetPlayerName}", Color.white, () => { });
                waPlayerPage.CreateFunction("Clear Target", Color.red, () =>
                {
                    WeepingAngelController.ClearTarget();
                    SendNotification(NotificationType.Success, "Weeping Angel target cleared");
                });
                waPlayerPage.CreateFunction("Refresh Player List", Color.yellow, () =>
                {
                    PopulateWeepingAngelPlayerList(waPlayerPage);
                });
                PopulateWeepingAngelPlayerList(waPlayerPage);

                // ============================================
                // SPAWN ON PLAYER submenu
                // ============================================
                var playerSpawnPage = combatPage.CreatePage("Spawn on Player", Color.red);
                var itemSearchResults = playerSpawnPage.CreatePage("+ Item Search Results", Color.yellow);
                playerSpawnPage.CreateString(
                    "Item Search",
                    Color.white,
                    PlayerSpawnController.ItemSearchQuery,
                    (value) => PlayerSpawnController.ItemSearchQuery = value
                );
                playerSpawnPage.CreateFunction(
                    "Find Items",
                    Color.yellow,
                    () => PlayerSpawnController.ItemSearchToPage(itemSearchResults)
                );
                playerSpawnPage.CreateFloat(
                    "Height (0=Torso)",
                    Color.cyan,
                    PlayerSpawnController.HeightAbovePlayer,
                    0.5f,
                    -50f,
                    50f,
                    (value) => { PlayerSpawnController.HeightAbovePlayer = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Launch Force",
                    Color.red,
                    PlayerSpawnController.LaunchForce,
                    10f,
                    0f,
                    1000f,
                    (value) => { PlayerSpawnController.LaunchForce = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Projectile Count",
                    Color.magenta,
                    PlayerSpawnController.ProjectileCount,
                    1f,
                    1f,
                    25f,
                    (value) => { PlayerSpawnController.ProjectileCount = (int)value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Projectile Spacing",
                    Color.cyan,
                    PlayerSpawnController.ProjectileSpacing,
                    0.1f,
                    0.1f,
                    5f,
                    (value) => { PlayerSpawnController.ProjectileSpacing = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Spawn Scale",
                    Color.magenta,
                    PlayerSpawnController.SpawnScale,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { PlayerSpawnController.SpawnScale = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Spin Velocity",
                    Color.green,
                    PlayerSpawnController.SpinVelocity,
                    5f,
                    0f,
                    5000f,
                    (value) => { PlayerSpawnController.SpinVelocity = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateBool(
                    "Aim Rotation (Face Launch Dir)",
                    Color.green,
                    PlayerSpawnController.AimRotationEnabled,
                    (value) => { PlayerSpawnController.AimRotationEnabled = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateBool(
                    "Pre-Activate (Menu Tap)",
                    Color.magenta,
                    PlayerSpawnController.PreActivateMenuTap,
                    (value) => { PlayerSpawnController.PreActivateMenuTap = value; SettingsManager.MarkDirty(); }
                );
                playerSpawnPage.CreateFloat(
                    "Spawn Force Delay",
                    Color.green,
                    PlayerSpawnController.SpawnForceDelay,
                    0.01f,
                    0f,
                    2f,
                    (value) => { PlayerSpawnController.SpawnForceDelay = value; SettingsManager.MarkDirty(); }
                );
                var playerDropResults = playerSpawnPage.CreatePage("+ Target Players", Color.red);
                playerSpawnPage.CreateString(
                    "Player Search",
                    Color.white,
                    PlayerSpawnController.PlayerSearchQuery,
                    (value) => PlayerSpawnController.PlayerSearchQuery = value
                );
                playerSpawnPage.CreateFunction(
                    "Find Players (Click to Spawn)",
                    Color.red,
                    () => PlayerSpawnController.PlayerSearchToPage(playerDropResults)
                );

                // Homing submenu for Drop on Player
                var dropHomingPage = playerSpawnPage.CreatePage("Homing", Color.red);
                dropHomingPage.CreateBool("Enabled", Color.white, PlayerSpawnController.HomingEnabled,
                    (value) => { PlayerSpawnController.HomingEnabled = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateEnum("Filter", Color.red, PlayerSpawnController.HomingFilter,
                    (value) => { PlayerSpawnController.HomingFilter = (TargetFilter)value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Strength", Color.yellow, PlayerSpawnController.HomingStrength, 1f, 1f, 50f,
                    (value) => { PlayerSpawnController.HomingStrength = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Duration (0=unlimited)", Color.cyan, PlayerSpawnController.HomingDuration, 0.5f, 0f, 30f,
                    (value) => { PlayerSpawnController.HomingDuration = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateBool("Rotation Lock", Color.green, PlayerSpawnController.HomingRotationLock,
                    (value) => { PlayerSpawnController.HomingRotationLock = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Speed (0=auto)", Color.yellow, PlayerSpawnController.HomingSpeed, 5f, 0f, 500f,
                    (value) => { PlayerSpawnController.HomingSpeed = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateBool("Acceleration", Color.cyan, PlayerSpawnController.HomingAccelEnabled,
                    (value) => { PlayerSpawnController.HomingAccelEnabled = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Accel Rate", Color.cyan, PlayerSpawnController.HomingAccelRate, 0.5f, 0.1f, 10f,
                    (value) => { PlayerSpawnController.HomingAccelRate = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateBool("Target Head", Color.magenta, PlayerSpawnController.HomingTargetHead,
                    (value) => { PlayerSpawnController.HomingTargetHead = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateBool("Momentum", Color.green, PlayerSpawnController.HomingMomentum,
                    (value) => { PlayerSpawnController.HomingMomentum = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Stay Duration", Color.yellow, PlayerSpawnController.HomingStayDuration, 0.5f, 0f, 30f,
                    (value) => { PlayerSpawnController.HomingStayDuration = value; SettingsManager.MarkDirty(); });
                dropHomingPage.CreateFloat("Force Delay", Color.cyan, PlayerSpawnController.ForceDelay, 0.01f, 0f, 1f,
                    (value) => { PlayerSpawnController.ForceDelay = value; SettingsManager.MarkDirty(); });
                BuildSelectPlayerSubMenu(dropHomingPage);

                // ============================================
                // FORCE GRAB submenu (inside Player)
                // ============================================
                var forceGrabPage = playerPage.CreatePage("Force Grab", Color.magenta);
                forceGrabPage.CreateBool(
                    "Enabled",
                    Color.white,
                    ForceGrabController.IsEnabled,
                    (value) => { ForceGrabController.IsEnabled = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Global (Any Distance)",
                    Color.red,
                    ForceGrabController.GlobalMode,
                    (value) => { ForceGrabController.GlobalMode = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Instant Mode",
                    Color.yellow,
                    ForceGrabController.InstantMode,
                    (value) => { ForceGrabController.InstantMode = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Grip Only (Items w/ Grip)",
                    Color.cyan,
                    ForceGrabController.GripOnly,
                    (value) => { ForceGrabController.GripOnly = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Ignore Player Rig",
                    Color.cyan,
                    ForceGrabController.IgnorePlayerRig,
                    (value) => { ForceGrabController.IgnorePlayerRig = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Affect Players (Push/Pull)",
                    Color.yellow,
                    ForceGrabController.AffectPlayers,
                    (value) => { ForceGrabController.AffectPlayers = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateBool(
                    "Force Push (Push Away)",
                    Color.red,
                    ForceGrabController.ForcePush,
                    (value) => { ForceGrabController.ForcePush = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateFloat(
                    "Push Force",
                    Color.red,
                    ForceGrabController.PushForce,
                    5f,
                    5f,
                    500f,
                    (value) => { ForceGrabController.PushForce = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateFloat(
                    "Fly Speed",
                    Color.cyan,
                    ForceGrabController.FlySpeed,
                    5f,
                    5f,
                    200f,
                    (value) => { ForceGrabController.FlySpeed = value; SettingsManager.MarkDirty(); }
                );
                forceGrabPage.CreateFunction(
                    "Select Item (Left Hand)",
                    Color.green,
                    () => ForceGrabController.SelectItemFromHand(true)
                );
                forceGrabPage.CreateFunction(
                    "Select Item (Right Hand)",
                    Color.green,
                    () => ForceGrabController.SelectItemFromHand(false)
                );
                forceGrabPage.CreateFunction(
                    "Clear Selected",
                    Color.red,
                    ForceGrabController.ClearSelected
                );

                // Freeze Player moved to Utilities

                // ============================================
                // WAYPOINT PROJECTILE submenu
                // ============================================
                var waypointProjPage = combatPage.CreatePage("Waypoint Projectile", Color.yellow);
                var wpItemSearchResults = waypointProjPage.CreatePage("+ Item Search", Color.yellow);
                waypointProjPage.CreateString(
                    "Item Search",
                    Color.white,
                    WaypointController.ItemSearchQuery,
                    (value) => WaypointController.ItemSearchQuery = value
                );
                waypointProjPage.CreateFunction(
                    "Find Items",
                    Color.yellow,
                    () => WaypointController.ItemSearchToPage(wpItemSearchResults)
                );
                waypointProjPage.CreateFloat(
                    "Spawn Height",
                    Color.cyan,
                    WaypointController.SpawnHeight,
                    1f,
                    0f,
                    50f,
                    (value) => { WaypointController.SpawnHeight = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Launch Force",
                    Color.red,
                    WaypointController.LaunchForce,
                    10f,
                    0f,
                    1000f,
                    (value) => { WaypointController.LaunchForce = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Projectile Count",
                    Color.magenta,
                    WaypointController.ProjectileCount,
                    1f,
                    1f,
                    25f,
                    (value) => { WaypointController.ProjectileCount = (int)value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Projectile Spacing",
                    Color.cyan,
                    WaypointController.ProjectileSpacing,
                    0.1f,
                    0.1f,
                    5f,
                    (value) => { WaypointController.ProjectileSpacing = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Spawn Scale",
                    Color.magenta,
                    WaypointController.SpawnScale,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { WaypointController.SpawnScale = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Spin Velocity",
                    Color.green,
                    WaypointController.SpinVelocity,
                    5f,
                    0f,
                    5000f,
                    (value) => { WaypointController.SpinVelocity = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateBool(
                    "Aim Rotation (Face Launch Dir)",
                    Color.green,
                    WaypointController.AimRotationEnabled,
                    (value) => { WaypointController.AimRotationEnabled = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateBool(
                    "Pre-Activate (Menu Tap)",
                    Color.magenta,
                    WaypointController.PreActivateMenuTap,
                    (value) => { WaypointController.PreActivateMenuTap = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFloat(
                    "Spawn Force Delay",
                    Color.green,
                    WaypointController.SpawnForceDelay,
                    0.01f,
                    0f,
                    2f,
                    (value) => { WaypointController.SpawnForceDelay = value; SettingsManager.MarkDirty(); }
                );
                waypointProjPage.CreateFunction(
                    "Save Spawn Position",
                    Color.green,
                    WaypointController.SaveSpawnPosition
                );
                waypointProjPage.CreateFunction(
                    "Spawn at Saved Pos",
                    Color.red,
                    WaypointController.SpawnAtSavedPosition
                );

                // Homing submenu for Waypoint Projectile
                var wpHomingPage = waypointProjPage.CreatePage("Homing", Color.red);
                wpHomingPage.CreateBool("Enabled", Color.white, WaypointController.HomingEnabled,
                    (value) => { WaypointController.HomingEnabled = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateEnum("Filter", Color.red, WaypointController.HomingFilter,
                    (value) => { WaypointController.HomingFilter = (TargetFilter)value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Strength", Color.yellow, WaypointController.HomingStrength, 1f, 1f, 50f,
                    (value) => { WaypointController.HomingStrength = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Duration (0=unlimited)", Color.cyan, WaypointController.HomingDuration, 0.5f, 0f, 30f,
                    (value) => { WaypointController.HomingDuration = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateBool("Rotation Lock", Color.green, WaypointController.HomingRotationLock,
                    (value) => { WaypointController.HomingRotationLock = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Speed (0=auto)", Color.yellow, WaypointController.HomingSpeed, 5f, 0f, 500f,
                    (value) => { WaypointController.HomingSpeed = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateBool("Acceleration", Color.cyan, WaypointController.HomingAccelEnabled,
                    (value) => { WaypointController.HomingAccelEnabled = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Accel Rate", Color.cyan, WaypointController.HomingAccelRate, 0.5f, 0.1f, 10f,
                    (value) => { WaypointController.HomingAccelRate = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateBool("Target Head", Color.magenta, WaypointController.HomingTargetHead,
                    (value) => { WaypointController.HomingTargetHead = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateBool("Momentum", Color.green, WaypointController.HomingMomentum,
                    (value) => { WaypointController.HomingMomentum = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Stay Duration", Color.yellow, WaypointController.HomingStayDuration, 0.5f, 0f, 30f,
                    (value) => { WaypointController.HomingStayDuration = value; SettingsManager.MarkDirty(); });
                wpHomingPage.CreateFloat("Force Delay", Color.cyan, WaypointController.ForceDelay, 0.01f, 0f, 1f,
                    (value) => { WaypointController.ForceDelay = value; SettingsManager.MarkDirty(); });
                BuildSelectPlayerSubMenu(wpHomingPage);

                // ============================================
                // UTILITIES submenu (Despawn All, Change Map, Avatar Search, Anti-Despawn)
                // ============================================
                var utilitiesPage = _mainPage.CreatePage("Utilities", Color.yellow);

                // ── AI NPC Controls (inside Utilities) ──
                var aiNpcPage = utilitiesPage.CreatePage("AI NPC Controls", Color.red);
                aiNpcPage.CreateEnum(
                    "Mental State",
                    Color.yellow,
                    AINpcController.SelectedState,
                    (value) => { AINpcController.SelectedState = (AINpcController.NpcMentalState)value; }
                );
                aiNpcPage.CreateFunction("Apply State to Held NPC", Color.green, AINpcController.ApplyStateToHeld);
                aiNpcPage.CreateFunction("Apply State to ALL NPCs", Color.red, AINpcController.ApplyStateToAll);
                aiNpcPage.CreateFloat(
                    "Custom HP",
                    Color.cyan,
                    AINpcController.CustomHp,
                    50f,
                    1f,
                    10000f,
                    (value) => { AINpcController.CustomHp = value; }
                );
                aiNpcPage.CreateFunction("Apply HP to Held NPC", Color.green, AINpcController.ApplyHpToHeld);
                aiNpcPage.CreateFloat(
                    "Custom Mass",
                    Color.magenta,
                    AINpcController.CustomMass,
                    0.5f,
                    0.01f,
                    100f,
                    (value) => { AINpcController.CustomMass = value; }
                );
                aiNpcPage.CreateFunction("Apply Mass to Held NPC", Color.green, AINpcController.ApplyMassToHeld);

                // ── Anti-Despawn Effect (inside Utilities) ──
                utilitiesPage.CreateBool(
                    "Anti-Despawn Effect",
                    Color.cyan,
                    AntiDespawnController.Enabled,
                    (value) => { AntiDespawnController.Enabled = value; SettingsManager.MarkDirty(); }
                );

                // ── Avatar Logger (inside Utilities) ──
                var avatarLoggerPage = utilitiesPage.CreatePage("Avatar Logger", Color.green);
                avatarLoggerPage.CreateBool(
                    "Enabled",
                    Color.white,
                    AvatarLoggerController.Enabled,
                    (value) => { AvatarLoggerController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                avatarLoggerPage.CreateBool(
                    "Show Notifications",
                    Color.cyan,
                    AvatarLoggerController.ShowNotifications,
                    (value) => { AvatarLoggerController.ShowNotifications = value; SettingsManager.MarkDirty(); }
                );

                // ============================================
                // SPAWN MENU submenu (inside Utilities)
                // ============================================
                var spawnMenuPage = utilitiesPage.CreatePage("Spawn Menu", Color.yellow);
                var searchResultsPage = spawnMenuPage.CreatePage("+ Spawnable Search Results", Color.green);
                spawnMenuPage.CreateEnum(
                    "Search Action",
                    Color.cyan,
                    SpawnableSearcher.CurrentSpawnType,
                    (value) => SpawnableSearcher.CurrentSpawnType = (SpawnableSearcher.SpawnableSearchType)value
                );
                spawnMenuPage.CreateEnum(
                    "Search Method",
                    Color.magenta,
                    SpawnableSearcher.CurrentSearchMethod,
                    (value) => SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value
                );
                spawnMenuPage.CreateString(
                    "Match",
                    Color.white,
                    SpawnableSearcher.SearchQuery,
                    (value) => SpawnableSearcher.SearchQuery = value
                );
                spawnMenuPage.CreateFunction(
                    "Find Results",
                    Color.yellow,
                    () => SpawnableSearcher.SearchToPage(searchResultsPage)
                );
                spawnMenuPage.CreateBool(
                    "Exclude Ammo/Mags",
                    Color.yellow,
                    SpawnableSearcher.ExcludeAmmo,
                    (value) => SpawnableSearcher.ExcludeAmmo = value
                );
                var historyPage = spawnMenuPage.CreatePage("+ Search History", Color.cyan);
                spawnMenuPage.CreateFunction(
                    "Show History",
                    Color.cyan,
                    () => SpawnableSearcher.PopulateHistoryPage(historyPage)
                );
                var favoritesPage = spawnMenuPage.CreatePage("+ Favorites", Color.yellow);
                spawnMenuPage.CreateFunction(
                    "Show Favorites",
                    Color.yellow,
                    () => SaveFavoritesReader.PopulateGameFavoritesPage(favoritesPage)
                );
                var customFavPage = spawnMenuPage.CreatePage("+ Custom Favorites", Color.green);
                spawnMenuPage.CreateFunction(
                    "Show Custom Favorites",
                    Color.green,
                    () => SpawnableSearcher.PopulateFavoritesPage(customFavPage)
                );
                spawnMenuPage.CreateFunction(
                    "Favorite Item From Hand",
                    Color.green,
                    () => SpawnableSearcher.FavoriteItemFromHand()
                );

                // ── Screen Share (inside Utilities) ──
                var screenSharePage = utilitiesPage.CreatePage("Screen Share", Color.magenta);
                screenSharePage.CreateBool(
                    "Enabled",
                    Color.green,
                    ScreenShareController.Enabled,
                    (value) => { ScreenShareController.SetEnabled(value); SettingsManager.MarkDirty(); }
                );
                screenSharePage.CreateBool(
                    "Preview Visible",
                    Color.cyan,
                    ScreenShareController.PreviewVisible,
                    (value) => { ScreenShareController.SetPreviewVisible(value); SettingsManager.MarkDirty(); }
                );
                screenSharePage.CreateFunction(
                    "Reposition Quad",
                    Color.white,
                    ScreenShareController.Reposition
                );

                // Source selection
                var ssSourcePage = screenSharePage.CreatePage("Source", Color.yellow);
                ssSourcePage.CreateFunction(
                    "Desktop (Full Screen)",
                    Color.green,
                    () => ScreenShareController.SelectSource(-1)
                );
                var ssWindowResults = ssSourcePage.CreatePage("+ Windows", Color.green);
                ssSourcePage.CreateFunction(
                    "Refresh Windows",
                    Color.cyan,
                    () =>
                    {
                        ScreenShareController.RefreshWindows();
                        // Rebuild window list dynamically
                        try { ssWindowResults?.RemoveAll(); } catch { }
                        for (int wi = 0; wi < ScreenShareController.AvailableWindows.Count; wi++)
                        {
                            int capturedIdx = wi;
                            string title = ScreenShareController.AvailableWindows[wi];
                            // Truncate long window titles for menu display
                            string display = title.Length > 40 ? title.Substring(0, 37) + "..." : title;
                            ssWindowResults?.CreateFunction(display, Color.white,
                                () => ScreenShareController.SelectSource(capturedIdx));
                        }
                        NotificationHelper.Send(NotificationType.Success,
                            $"Found {ScreenShareController.AvailableWindows.Count} windows");
                    }
                );

                // Scale
                var ssScalePage = screenSharePage.CreatePage("Scale", Color.white);
                ssScalePage.CreateFunction("25%  (480x270)", Color.white,
                    () => { ScreenShareController.SetScale(25); SettingsManager.MarkDirty(); });
                ssScalePage.CreateFunction("50%  (960x540)", Color.white,
                    () => { ScreenShareController.SetScale(50); SettingsManager.MarkDirty(); });
                ssScalePage.CreateFunction("75%  (1440x810)", Color.white,
                    () => { ScreenShareController.SetScale(75); SettingsManager.MarkDirty(); });
                ssScalePage.CreateFunction("100% (1920x1080)", Color.white,
                    () => { ScreenShareController.SetScale(100); SettingsManager.MarkDirty(); });

                // FPS
                var ssFpsPage = screenSharePage.CreatePage("Framerate", Color.white);
                ssFpsPage.CreateFunction("15 fps", Color.white,
                    () => { ScreenShareController.SetFps(15); SettingsManager.MarkDirty(); });
                ssFpsPage.CreateFunction("30 fps", Color.white,
                    () => { ScreenShareController.SetFps(30); SettingsManager.MarkDirty(); });
                ssFpsPage.CreateFunction("60 fps", Color.white,
                    () => { ScreenShareController.SetFps(60); SettingsManager.MarkDirty(); });

                // HTTP Network Stream
                var ssStreamPage = screenSharePage.CreatePage("Network Stream", Color.red);
                ssStreamPage.CreateBool(
                    "HTTP Stream",
                    Color.green,
                    ScreenShareController.StreamEnabled,
                    (value) => { ScreenShareController.SetStreamEnabled(value); SettingsManager.MarkDirty(); }
                );
                ssStreamPage.CreateFunction(
                    "Copy URL to Clipboard",
                    Color.yellow,
                    ScreenShareController.CopyUrlToClipboard
                );
                ssStreamPage.CreateBool(
                    "Use Public IP (Internet)",
                    Color.cyan,
                    ScreenShareController.UsePublicIp,
                    (value) => { ScreenShareController.UsePublicIp = value; SettingsManager.MarkDirty(); }
                );
                ssStreamPage.CreateFloat(
                    "Port",
                    Color.cyan,
                    ScreenShareController.StreamPort,
                    1f,
                    1024f,
                    65535f,
                    (value) => { ScreenShareController.StreamPort = (int)value; SettingsManager.MarkDirty(); }
                );
                ssStreamPage.CreateFunction(
                    "Show Stream URL",
                    Color.yellow,
                    () =>
                    {
                        string url = ScreenShareController.StreamUrl;
                        if (string.IsNullOrEmpty(url))
                            NotificationHelper.Send(NotificationType.Warning, "Stream not active");
                        else
                            NotificationHelper.Send(NotificationType.Information, $"Stream URL:\n{url}");
                    }
                );
                ssStreamPage.CreateFunction(
                    "Show IPs",
                    Color.white,
                    () => NotificationHelper.Send(NotificationType.Information,
                        $"LAN: {ScreenShareController.GetLanIp()}\nPublic: {ScreenShareController.GetPublicIp()}")
                );

                // ── Freeze Player (inside Utilities) ──
                var freezePage = utilitiesPage.CreatePage("Freeze Player", Color.cyan);
                freezePage.CreateFunction(
                    "Unfreeze All",
                    Color.red,
                    FreezePlayerController.UnfreezeAll
                );
                var freezePlayerResults = freezePage.CreatePage("+ Select Player", Color.green);
                freezePage.CreateString(
                    "Search Name",
                    Color.white,
                    FreezePlayerController.PlayerSearchQuery,
                    (value) => FreezePlayerController.PlayerSearchQuery = value
                );
                freezePage.CreateFunction(
                    "Find Players",
                    Color.yellow,
                    () => FreezePlayerController.PlayerSearchToPage(freezePlayerResults)
                );

                // ── Avatar Search (inside Utilities) ──
                var avatarSearchPage = utilitiesPage.CreatePage("Avatar Search", Color.green);
                avatarSearchPage.CreateEnum(
                    "Search Type",
                    Color.yellow,
                    AvatarSearchController.CurrentSearchType,
                    (value) => { AvatarSearchController.CurrentSearchType = (AvatarSearchType)value; }
                );
                avatarSearchPage.CreateEnum(
                    "Search Method",
                    Color.yellow,
                    AvatarSearchController.CurrentSearchMethod,
                    (value) => { AvatarSearchController.CurrentSearchMethod = (AvatarSearchMethod)value; }
                );
                avatarSearchPage.CreateFloat(
                    "BodyLog Slot",
                    Color.green,
                    AvatarSearchController.BodyLogSlot,
                    1f, 1f, 6f,
                    (value) => { AvatarSearchController.BodyLogSlot = (int)value; }
                );
                avatarSearchPage.CreateString(
                    "Search",
                    Color.white,
                    AvatarSearchController.SearchQuery,
                    (value) => AvatarSearchController.SearchQuery = value
                );
                var avatarSearchResultsPage = avatarSearchPage.CreatePage("+ Results", Color.green);
                var avatarSearchHistory = avatarSearchPage.CreatePage("+ Search History", Color.green);
                avatarSearchPage.CreateFunction(
                    "Find Results",
                    Color.yellow,
                    () => AvatarSearchController.SearchToPage(avatarSearchResultsPage, avatarSearchHistory)
                );

                // ── Anti-Despawn Effect (inside Utilities) ──
                utilitiesPage.CreateBool(
                    "Anti-Despawn Effect",
                    Color.cyan,
                    AntiDespawnController.Enabled,
                    (value) => { AntiDespawnController.Enabled = value; SettingsManager.MarkDirty(); }
                );

                // ── Despawn All (inside Utilities) ──
                var despawnPage = utilitiesPage.CreatePage("Despawn All", Color.red);
                despawnPage.CreateFunction(
                    "Despawn Now",
                    Color.red,
                    DespawnAllController.DespawnAll
                );
                despawnPage.CreateEnum(
                    "Filter",
                    Color.yellow,
                    DespawnAllController.Filter,
                    (value) => { DespawnAllController.Filter = (DespawnFilter)value; SettingsManager.MarkDirty(); }
                );
                despawnPage.CreateBool(
                    "Auto-Despawn",
                    Color.cyan,
                    DespawnAllController.AutoDespawnEnabled,
                    (value) => { DespawnAllController.AutoDespawnEnabled = value; SettingsManager.MarkDirty(); }
                );
                despawnPage.CreateFloat(
                    "Interval (min)",
                    Color.cyan,
                    DespawnAllController.AutoDespawnIntervalMins,
                    0.5f,
                    0f,
                    60f,
                    (value) => { DespawnAllController.AutoDespawnIntervalMins = value; SettingsManager.MarkDirty(); }
                );
                despawnPage.CreateBool(
                    "Keep Holstered Items",
                    Color.green,
                    DespawnAllController.KeepHolsteredItems,
                    (value) => { DespawnAllController.KeepHolsteredItems = value; SettingsManager.MarkDirty(); }
                );
                despawnPage.CreateBool(
                    "Only My Holsters",
                    Color.green,
                    DespawnAllController.KeepOnlyMyHolsters,
                    (value) => { DespawnAllController.KeepOnlyMyHolsters = value; SettingsManager.MarkDirty(); }
                );

                // ── Change Map (inside Utilities) ──
                var mapPage = utilitiesPage.CreatePage("Change Map", Color.green);
                var mapResultsPage = mapPage.CreatePage("+ Level Results", Color.green);
                mapPage.CreateString(
                    "Search",
                    Color.white,
                    MapChangeController.SearchQuery,
                    (value) => MapChangeController.SearchQuery = value
                );
                mapPage.CreateFunction(
                    "Find Levels",
                    Color.yellow,
                    () => MapChangeController.PopulateResultsPage(mapResultsPage)
                );
                mapPage.CreateFunction(
                    "Reload Level",
                    Color.cyan,
                    MapChangeController.ReloadLevel
                );

                // ── Server Queue (inside Utilities) ──
                var queuePage = utilitiesPage.CreatePage("Server Queue", Color.magenta);
                queuePage.CreateBool(
                    "Auto-Queue",
                    Color.green,
                    ServerQueueController.Enabled,
                    (value) => { ServerQueueController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                queuePage.CreateFloat(
                    "Poll Interval (s)",
                    Color.cyan,
                    ServerQueueController.PollInterval,
                    5f,
                    5f,
                    60f,
                    (value) => { ServerQueueController.PollInterval = value; SettingsManager.MarkDirty(); }
                );
                queuePage.CreateFunction(
                    "Start Queue (use last code)",
                    Color.green,
                    () => ServerQueueController.StartQueueForCode(ServerQueueController.LastServerCode)
                );
                queuePage.CreateFunction(
                    "Stop Queue",
                    Color.red,
                    ServerQueueController.StopQueue
                );

                // ── Fix Wobbly Avatar (inside Utilities) ──
                utilitiesPage.CreateFunction(
                    "Fix Wobbly Avatar",
                    Color.cyan,
                    BlockController.FixWobblyAvatar
                );

                // ── XYZ Scale (inside Utilities) ──
                var xyzScalePage = utilitiesPage.CreatePage("XYZ Scale", Color.cyan);
                xyzScalePage.CreateBool(
                    "Enabled",
                    Color.green,
                    XYZScaleController.Enabled,
                    (value) => { XYZScaleController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                xyzScalePage.CreateFloat(
                    "Scale X",
                    Color.red,
                    XYZScaleController.ScaleX,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { XYZScaleController.ScaleX = value; SettingsManager.MarkDirty(); }
                );
                xyzScalePage.CreateFloat(
                    "Scale Y",
                    Color.green,
                    XYZScaleController.ScaleY,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { XYZScaleController.ScaleY = value; SettingsManager.MarkDirty(); }
                );
                xyzScalePage.CreateFloat(
                    "Scale Z",
                    Color.blue,
                    XYZScaleController.ScaleZ,
                    0.1f,
                    0.1f,
                    10f,
                    (value) => { XYZScaleController.ScaleZ = value; SettingsManager.MarkDirty(); }
                );
                xyzScalePage.CreateFunction(
                    "Apply Scale",
                    Color.yellow,
                    XYZScaleController.ApplyScale
                );
                xyzScalePage.CreateFunction(
                    "Reset (1, 1, 1)",
                    Color.white,
                    () =>
                    {
                        XYZScaleController.ScaleX = 1f;
                        XYZScaleController.ScaleY = 1f;
                        XYZScaleController.ScaleZ = 1f;
                        XYZScaleController.ApplyScale();
                        SettingsManager.MarkDirty();
                    }
                );

                // ── Disable Avatar FX (inside Utilities) ──
                utilitiesPage.CreateBool(
                    "Disable Avatar Switch FX",
                    Color.magenta,
                    DisableAvatarFXController.Enabled,
                    (value) => { DisableAvatarFXController.Enabled = value; SettingsManager.MarkDirty(); }
                );

                // ── Player Block (inside Utilities) ──
                var playerBlockPage = utilitiesPage.CreatePage("Player Block", Color.red);
                playerBlockPage.CreateBool(
                    "Enabled",
                    Color.green,
                    BlockController.PlayerBlockEnabled,
                    (value) => { BlockController.PlayerBlockEnabled = value; SettingsManager.MarkDirty(); }
                );
                playerBlockPage.CreateString(
                    "Search",
                    Color.white,
                    TeleportController.PlayerSearchQuery,
                    (value) => TeleportController.PlayerSearchQuery = value
                );
                var playerBlockResultsPage = playerBlockPage.CreatePage("+ Search Results", Color.green);
                playerBlockPage.CreateFunction(
                    "Find Players",
                    Color.yellow,
                    () =>
                    {
                        try { playerBlockResultsPage?.RemoveAll(); } catch { }
                        TeleportController.RefreshPlayerList();
                        var players = TeleportController.GetCachedPlayers();
                        string searchLower = (TeleportController.PlayerSearchQuery ?? "").ToLower();
                        foreach (var player in players)
                        {
                            if (!string.IsNullOrEmpty(searchLower) && !player.DisplayName.ToLower().Contains(searchLower))
                                continue;
                            byte capturedId = player.SmallID;
                            string capturedName = player.DisplayName;
                            playerBlockResultsPage?.CreateFunction(capturedName, Color.green, () =>
                            {
                                BlockController.AddBlockedPlayer(capturedId, capturedName);
                            });
                        }
                    }
                );
                var playerBlockListPage = playerBlockPage.CreatePage("+ Blocked Players", Color.red);
                playerBlockPage.CreateFunction(
                    "Refresh Block List",
                    Color.cyan,
                    () =>
                    {
                        try { playerBlockListPage?.RemoveAll(); } catch { }
                        foreach (var bp in BlockController.BlockedPlayers)
                        {
                            byte capturedId = bp.SmallID;
                            string capturedName = bp.DisplayName;
                            playerBlockListPage?.CreateFunction($"{capturedName} [Remove]", Color.red, () =>
                            {
                                BlockController.RemoveBlockedPlayer(capturedId);
                            });
                        }
                    }
                );

                // ── Item Block / Server-Side (inside Utilities) ──
                var itemBlockPage = utilitiesPage.CreatePage("Item Block (Server)", Color.red);
                itemBlockPage.CreateBool(
                    "Enabled",
                    Color.green,
                    BlockController.ItemBlockEnabled,
                    (value) => { BlockController.ItemBlockEnabled = value; SettingsManager.MarkDirty(); }
                );
                itemBlockPage.CreateEnum(
                    "Search Method",
                    Color.yellow,
                    SpawnableSearcher.CurrentSearchMethod,
                    (value) => { SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value; }
                );
                itemBlockPage.CreateString(
                    "Search",
                    Color.white,
                    SpawnableSearcher.SearchQuery,
                    (value) => SpawnableSearcher.SearchQuery = value
                );
                var itemBlockResultsPage = itemBlockPage.CreatePage("+ Search Results", Color.green);
                itemBlockPage.CreateFunction(
                    "Find Items",
                    Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(itemBlockResultsPage, (barcode) =>
                    {
                        string name = BlockController.ResolveDisplayName(barcode);
                        BlockController.AddBlockedItem(barcode, name);
                    })
                );
                var itemBlockListPage = itemBlockPage.CreatePage("+ Blocked Items", Color.red);
                itemBlockPage.CreateFunction(
                    "Refresh Block List",
                    Color.cyan,
                    () =>
                    {
                        try { itemBlockListPage?.RemoveAll(); } catch { }
                        foreach (var bi in BlockController.BlockedItems)
                        {
                            string capturedBarcode = bi.Barcode;
                            string capturedName = bi.DisplayName;
                            itemBlockListPage?.CreateFunction($"{capturedName} [Remove]", Color.red, () =>
                            {
                                BlockController.RemoveBlockedItem(capturedBarcode);
                            });
                        }
                    }
                );

                // ── Local Block / Client-Side (inside Utilities) ──
                var localBlockPage = utilitiesPage.CreatePage("Local Block (Client)", Color.magenta);
                localBlockPage.CreateBool(
                    "Enabled",
                    Color.green,
                    BlockController.LocalBlockEnabled,
                    (value) => { BlockController.LocalBlockEnabled = value; SettingsManager.MarkDirty(); }
                );
                localBlockPage.CreateEnum(
                    "Search Method",
                    Color.yellow,
                    SpawnableSearcher.CurrentSearchMethod,
                    (value) => { SpawnableSearcher.CurrentSearchMethod = (SpawnableSearcher.SearchMethod)value; }
                );
                localBlockPage.CreateString(
                    "Search",
                    Color.white,
                    SpawnableSearcher.SearchQuery,
                    (value) => SpawnableSearcher.SearchQuery = value
                );
                var localBlockResultsPage = localBlockPage.CreatePage("+ Search Results", Color.green);
                localBlockPage.CreateFunction(
                    "Find Items",
                    Color.yellow,
                    () => SpawnableSearcher.SearchToPageWithAction(localBlockResultsPage, (barcode) =>
                    {
                        string name = BlockController.ResolveDisplayName(barcode);
                        BlockController.AddLocalBlockedItem(barcode, name);
                    })
                );
                var localBlockListPage = localBlockPage.CreatePage("+ Blocked Items (Local)", Color.magenta);
                localBlockPage.CreateFunction(
                    "Refresh Block List",
                    Color.cyan,
                    () =>
                    {
                        try { localBlockListPage?.RemoveAll(); } catch { }
                        foreach (var bi in BlockController.LocalBlockedItems)
                        {
                            string capturedBarcode = bi.Barcode;
                            string capturedName = bi.DisplayName;
                            localBlockListPage?.CreateFunction($"{capturedName} [Remove]", Color.magenta, () =>
                            {
                                BlockController.RemoveLocalBlockedItem(capturedBarcode);
                            });
                        }
                    }
                );

                // ── Force Spawner (inside Utilities) ──
                var forceSpawnerPage = utilitiesPage.CreatePage("Force Spawner", Color.magenta);
                forceSpawnerPage.CreateBool("Enabled", Color.green, ForceSpawnerController.Enabled,
                    (value) => { ForceSpawnerController.Enabled = value; SettingsManager.MarkDirty(); });
                forceSpawnerPage.CreateBool("Unredact All", Color.yellow, ForceSpawnerController.UnredactAll,
                    (value) => { ForceSpawnerController.UnredactAll = value; SettingsManager.MarkDirty(); });
                forceSpawnerPage.CreateInt("Spawn Distance", Color.cyan, ForceSpawnerController.Distance,
                    1, 1, 50,
                    (value) => { ForceSpawnerController.Distance = value; SettingsManager.MarkDirty(); });
                forceSpawnerPage.CreateFloat("Offset X", Color.red, ForceSpawnerController.OffsetX,
                    0.5f, -10f, 10f,
                    (value) => { ForceSpawnerController.OffsetX = value; SettingsManager.MarkDirty(); });
                forceSpawnerPage.CreateFloat("Offset Y", Color.green, ForceSpawnerController.OffsetY,
                    0.5f, -10f, 10f,
                    (value) => { ForceSpawnerController.OffsetY = value; SettingsManager.MarkDirty(); });
                forceSpawnerPage.CreateFloat("Offset Z", Color.blue, ForceSpawnerController.OffsetZ,
                    0.5f, -10f, 10f,
                    (value) => { ForceSpawnerController.OffsetZ = value; SettingsManager.MarkDirty(); });

                // ── Remove Wind SFX (inside Utilities) ──
                utilitiesPage.CreateBool(
                    "Remove Wind SFX",
                    Color.cyan,
                    RemoveWindSFXController.Enabled,
                    (value) => { RemoveWindSFXController.Enabled = value; SettingsManager.MarkDirty(); }
                );

                // ── Player Info / Steam Profiles (inside Utilities) ──
                var playerInfoPage = utilitiesPage.CreatePage("Player Info", Color.cyan);
                var playerInfoResultsPage = playerInfoPage.CreatePage("+ Players", Color.green);
                playerInfoPage.CreateFunction(
                    "Refresh Players",
                    Color.yellow,
                    () =>
                    {
                        try { playerInfoResultsPage?.RemoveAll(); } catch { }
                        PlayerInfoController.ForceRefresh();
                        var players = PlayerInfoController.Players;
                        if (players.Count == 0)
                        {
                            playerInfoResultsPage?.CreateFunction("No players found", Color.gray, () => { });
                            return;
                        }
                        foreach (var p in players)
                        {
                            string localTag = p.IsLocal ? " (YOU)" : "";
                            string spoofTag = p.IsSuspectedSpoof ? " [SPOOF?]" : "";
                            Color pColor = p.IsSuspectedSpoof ? Color.red : (p.IsLocal ? Color.cyan : Color.green);
                            var pPage = playerInfoResultsPage?.CreatePage($"{p.Username}{localTag}{spoofTag}", pColor);
                            if (pPage == null) continue;
                            pPage.CreateFunction($"SmallID: {p.SmallID}", Color.white, () => { });
                            pPage.CreateFunction($"SteamID: {p.SteamID}", Color.white, () => { });
                            if (p.SteamID != 0)
                            {
                                ulong capturedSteamId = p.SteamID;
                                pPage.CreateFunction("Open Steam Profile", Color.cyan, () =>
                                {
                                    Application.OpenURL($"steam://url/SteamIDPage/{capturedSteamId}");
                                });
                            }
                        }
                    }
                );

                Main.MelonLog.Msg("BoneMenu setup completed successfully!");
                SendNotification(NotificationType.Success, "Enjoy <3");

                // ============================================
                // KEYBINDS submenu
                // ============================================
                var keybindPage = utilitiesPage.CreatePage("Keybinds", Color.yellow);
                PopulateKeybindPage(keybindPage);

                // ── Lobby Browser (inside Utilities) ──
                var lobbyBrowserPage = utilitiesPage.CreatePage("Lobby Browser", Color.cyan);
                lobbyBrowserPage.CreateFunction(
                    "Refresh Lobbies",
                    Color.yellow,
                    LobbyBrowserController.RefreshLobbies
                );
                var lobbyListPage = lobbyBrowserPage.CreatePage("+ Lobby List", Color.green);
                lobbyBrowserPage.CreateFunction(
                    "Show Lobbies",
                    Color.green,
                    () =>
                    {
                        try { lobbyListPage?.RemoveAll(); } catch { }
                        var lobbies = LobbyBrowserController.CachedLobbies;
                        if (lobbies.Count == 0)
                        {
                            lobbyListPage?.CreateFunction("No lobbies found (Refresh first)", Color.gray, () => { });
                            return;
                        }
                        foreach (var lobby in lobbies)
                        {
                            string display = $"{lobby.LobbyName} ({lobby.PlayerCount}/{lobby.MaxPlayers})";
                            string code = lobby.LobbyCode;
                            lobbyListPage?.CreateFunction(display, Color.green, () =>
                            {
                                LobbyBrowserController.JoinLobby(code);
                            });
                        }
                    }
                );

                // ── Player Action Logger (inside Utilities) ──
                var playerActionLogPage = utilitiesPage.CreatePage("Player Action Logger", Color.cyan);
                playerActionLogPage.CreateBool(
                    "Enabled",
                    Color.white,
                    PlayerActionLoggerController.Enabled,
                    (value) => { PlayerActionLoggerController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                playerActionLogPage.CreateBool(
                    "Log Joins",
                    Color.green,
                    PlayerActionLoggerController.LogJoins,
                    (value) => { PlayerActionLoggerController.LogJoins = value; SettingsManager.MarkDirty(); }
                );
                playerActionLogPage.CreateBool(
                    "Log Leaves",
                    Color.yellow,
                    PlayerActionLoggerController.LogLeaves,
                    (value) => { PlayerActionLoggerController.LogLeaves = value; SettingsManager.MarkDirty(); }
                );
                playerActionLogPage.CreateBool(
                    "Log Deaths",
                    Color.red,
                    PlayerActionLoggerController.LogDeaths,
                    (value) => { PlayerActionLoggerController.LogDeaths = value; SettingsManager.MarkDirty(); }
                );
                playerActionLogPage.CreateBool(
                    "Show Notifications",
                    Color.cyan,
                    PlayerActionLoggerController.ShowNotifications,
                    (value) => { PlayerActionLoggerController.ShowNotifications = value; SettingsManager.MarkDirty(); }
                );

                // ── Spawn Logger (inside Utilities) ──
                var spawnLoggerPage = utilitiesPage.CreatePage("Spawn Logger", Color.green);
                spawnLoggerPage.CreateBool(
                    "Enabled",
                    Color.white,
                    SpawnLoggerController.Enabled,
                    (value) => { SpawnLoggerController.Enabled = value; SettingsManager.MarkDirty(); }
                );
                spawnLoggerPage.CreateBool(
                    "Show Notifications",
                    Color.cyan,
                    SpawnLoggerController.ShowNotifications,
                    (value) => { SpawnLoggerController.ShowNotifications = value; SettingsManager.MarkDirty(); }
                );
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to setup BoneMenu: {ex.Message}\n{ex.StackTrace}");
                SendNotification(NotificationType.Error, $"Menu setup failed: {ex.Message}");
            }
        }

        private static void PopulateKeybindPage(Page keybindPage)
        {
            keybindPage.RemoveAll();
            for (int i = 0; i < KeybindManager.Keybinds.Count; i++)
            {
                int idx = i;
                var kb = KeybindManager.Keybinds[i];
                string keyLabel = kb.Key == KeyCode.None ? "---" : kb.Key.ToString();
                keybindPage.CreateFunction(
                    $"{kb.Name}: [{keyLabel}]",
                    kb.Key == KeyCode.None ? Color.gray : Color.green,
                    () => KeybindManager.StartRebind(idx)
                );
            }
            keybindPage.CreateFunction(
                "Refresh Keybinds",
                Color.cyan,
                () => PopulateKeybindPage(keybindPage)
            );
            keybindPage.CreateFunction(
                "Reset All to Defaults",
                Color.red,
                () => { KeybindManager.ResetAllDefaults(); PopulateKeybindPage(keybindPage); }
            );
        }

        /// <summary>
        /// Move the last-added element of a BoneMenu page to position 0
        /// so our mod always appears first in the root page list.
        /// </summary>
        private static void MovePageToFront(Page parentPage)
        {
            try
            {
                var field = typeof(Page).GetField("_elements",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null) return;

                var elements = field.GetValue(parentPage) as System.Collections.IList;
                if (elements == null || elements.Count < 2) return;

                // Our page link is the last element (just created)
                var ourElement = elements[elements.Count - 1];
                elements.RemoveAt(elements.Count - 1);
                elements.Insert(0, ourElement);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Could not reorder BoneMenu: {ex.Message}");
            }
        }

        /// <summary>
        /// Set custom punch barcode from the item held in the specified hand.
        /// </summary>
        private static void SetCustomPunchFromHand(bool leftHand)
        {
            try
            {
                var hand = leftHand ? Player.LeftHand : Player.RightHand;
                if (hand == null)
                {
                    SendNotification(NotificationType.Warning, "Cannot access hand");
                    return;
                }

                // Find Poolee type
                Type pooleeType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "Poolee") { pooleeType = t; break; }
                        }
                    }
                    catch { }
                    if (pooleeType != null) break;
                }
                if (pooleeType == null) { SendNotification(NotificationType.Warning, "Poolee type not found"); return; }

                var genericMethod = typeof(Player).GetMethod("GetComponentInHand",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.MakeGenericMethod(pooleeType);
                if (genericMethod == null) { SendNotification(NotificationType.Warning, "GetComponentInHand not found"); return; }

                var poolee = genericMethod.Invoke(null, new object[] { hand });
                if (poolee == null) { SendNotification(NotificationType.Warning, "No spawnable item in hand"); return; }

                var crateProp = pooleeType.GetProperty("SpawnableCrate",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var crate = crateProp?.GetValue(poolee);
                if (crate == null) { SendNotification(NotificationType.Warning, "Not a spawnable item"); return; }

                var barcodeProp = crate.GetType().GetProperty("Barcode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var barcodeObj = barcodeProp?.GetValue(crate);
                var idProp = barcodeObj?.GetType().GetProperty("ID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                string barcode = idProp?.GetValue(barcodeObj) as string;

                if (string.IsNullOrEmpty(barcode))
                {
                    SendNotification(NotificationType.Warning, "Could not read barcode");
                    return;
                }

                string displayName = crate.GetType().GetProperty("name",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(crate)?.ToString() ?? barcode;

                ExplosivePunchController.CustomPunchBarcode = barcode;
                SettingsManager.MarkDirty();
                SendNotification(NotificationType.Success, $"Custom Punch set: {displayName}");
            }
            catch (Exception ex)
            {
                SendNotification(NotificationType.Error, $"Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a "Select Player" submenu with search + results page,
        /// same pattern as Teleport to Player / Drop on Player.
        /// </summary>
        private static void BuildSelectPlayerSubMenu(Page parentPage)
        {
            var selectPage = parentPage.CreatePage("Select Player", Color.magenta);
            var playerResults = selectPage.CreatePage("+ Player Results", Color.green);
            selectPage.CreateFunction(
                $"Current: {PlayerTargeting.SelectedPlayerName}",
                Color.white,
                () => { }
            );
            selectPage.CreateString(
                "Search Name",
                Color.white,
                PlayerTargeting.PlayerSearchQuery,
                (value) => PlayerTargeting.PlayerSearchQuery = value
            );
            selectPage.CreateFunction(
                "Find Players",
                Color.yellow,
                () => PlayerTargeting.PlayerSearchToPage(playerResults)
            );
        }

        private static void PopulateWeepingAngelPlayerList(Page page)
        {
            // Remove old dynamic player entries (keep first 3: Current, Clear, Refresh)
            try
            {
                var elements = page.Elements;
                if (elements != null && elements.Count > 3)
                {
                    while (elements.Count > 3)
                        page.Remove(elements[elements.Count - 1]);
                }
            }
            catch { }

            try
            {
                var players = PlayerTargeting.GetCachedPlayers();
                int count = 0;
                foreach (var player in players)
                {
                    if (player.Rig == null) continue;

                    var capturedRig = player.Rig;
                    var capturedName = player.DisplayName;
                    count++;

                    page.CreateFunction(capturedName, Color.green, () =>
                    {
                        WeepingAngelController.SetTargetPlayer(capturedRig, capturedName);
                        SendNotification(NotificationType.Success, $"Weeping Angel target: {capturedName}");
                    });
                }

                if (count == 0)
                    page.CreateFunction("(no other players)", Color.gray, () => { });
            }
            catch { }
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message, "DOOBER UTILS", 3f, true);
        }
    }

    /// <summary>
    /// Harmony patch to block damage when God Mode is enabled (FusionProtector style)
    /// Works without requiring server owner permission
    /// </summary>
    [HarmonyPatch(typeof(Player_Health), "TAKEDAMAGE")]
    public static class GodModeDamagePatch
    {
        private static bool Prefix()
        {
            // Return false to skip original method (block damage)
            // Return true to allow original method (take damage)
            return !GodModeController.IsGodModeEnabled;
        }
    }

    public static class GodModeController
    {
        private static bool godModeEnabled = false;
        private static bool harmonyPatched = false;

        public static bool IsGodModeEnabled
        {
            get => godModeEnabled;
            set
            {
                if (godModeEnabled == value) return;
                godModeEnabled = value;
                if (value)
                {
                    Main.MelonLog.Msg("God Mode ENABLED");
                    ApplyFusionOverrides();
                    HealToFull();
                }
                else
                {
                    Main.MelonLog.Msg("God Mode DISABLED");
                    ClearFusionOverrides();
                }
            }
        }

        public static void Initialize()
        {
            // Try to apply Harmony patch if not already done
            if (!harmonyPatched)
            {
                try
                {
                    // The MelonLoader harmony instance automatically patches classes with HarmonyPatch attributes
                    // But we can also try to manually ensure it's applied
                    Main.MelonLog.Msg("God Mode controller initialized with Harmony damage block patch");
                    harmonyPatched = true;
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"Harmony patch setup note: {ex.Message}");
                }
            }
        }

        private static void HealToFull()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager != null && rigManager.health != null)
                {
                    // Reset damage/hits
                    rigManager.health.ResetHits();

                    // Try to set full health via reflection
                    var healthType = rigManager.health.GetType();
                    var currHealthField = healthType.GetField("curr_Health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var maxHealthField = healthType.GetField("max_Health", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (currHealthField != null && maxHealthField != null)
                    {
                        var maxHealth = maxHealthField.GetValue(rigManager.health);
                        currHealthField.SetValue(rigManager.health, maxHealth);
                    }
                }
            }
            catch { }
        }

        private static void ApplyFusionOverrides()
        {
            try
            {
                // Find LabFusion.Player.LocalHealth type
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type localHealthType = null;
                foreach (var asm in assemblies)
                {
                    try { localHealthType = asm.GetType("LabFusion.Player.LocalHealth"); } catch { }
                    if (localHealthType != null)
                        break;
                }

                if (localHealthType != null)
                {
                    // VitalityOverride (float?) and MortalityOverride (bool?) are static properties
                    var vitalityProp = localHealthType.GetProperty("VitalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var mortalityProp = localHealthType.GetProperty("MortalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (vitalityProp != null)
                    {
                        try { vitalityProp.SetValue(null, (float?)999999f); } catch { }
                    }
                    if (mortalityProp != null)
                    {
                        try { mortalityProp.SetValue(null, (bool?)false); } catch { }
                    }

                    // Call SetFullHealth if available
                    var setFull = localHealthType.GetMethod("SetFullHealth", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    try { setFull?.Invoke(null, null); } catch { }
                }
            }
            catch { }
        }

        private static void ClearFusionOverrides()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type localHealthType = null;
                foreach (var asm in assemblies)
                {
                    try { localHealthType = asm.GetType("LabFusion.Player.LocalHealth"); } catch { }
                    if (localHealthType != null) break;
                }

                if (localHealthType != null)
                {
                    var vitalityProp = localHealthType.GetProperty("VitalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var mortalityProp = localHealthType.GetProperty("MortalityOverride",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                    if (vitalityProp != null)
                    {
                        try { vitalityProp.SetValue(null, (float?)null); } catch { }
                    }
                    if (mortalityProp != null)
                    {
                        try { mortalityProp.SetValue(null, (bool?)null); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void Update()
        {
            if (!godModeEnabled)
                return;

            // FusionProtector style: continuously reset hits to prevent damage accumulation
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager != null && rigManager.health != null)
                {
                    rigManager.health.ResetHits();
                }
            }
            catch { }

            // Also keep fusion overrides active
            ApplyFusionOverrides();
        }
    }

    /// <summary>
    /// Anti-Constraint: Continuously clears any constraints attached to the player
    /// Works without server owner permission
    /// </summary>
    public static class AntiConstraintController
    {
        private static bool _enabled = false;

        public static bool IsEnabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (value)
                {
                    Main.MelonLog.Msg("Anti-Constraint ENABLED");
                    ClearConstraints(); // Immediately clear
                }
                else
                {
                    Main.MelonLog.Msg("Anti-Constraint DISABLED");
                }
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Anti-Constraint controller initialized");
        }

        public static void ClearConstraints()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null || rigManager.physicsRig == null) return;

                // Find ConstraintTracker type
                Type constraintTrackerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { constraintTrackerType = asm.GetType("Il2CppSLZ.Marrow.ConstraintTracker"); } catch { }
                    if (constraintTrackerType != null) break;
                    try { constraintTrackerType = asm.GetType("SLZ.Marrow.ConstraintTracker"); } catch { }
                    if (constraintTrackerType != null) break;
                }

                if (constraintTrackerType == null)
                {
                    // Try using BoneLib's LocalPlayer.ClearConstraints
                    try
                    {
                        var localPlayerType = typeof(Player).Assembly.GetType("BoneLib.LocalPlayer");
                        if (localPlayerType != null)
                        {
                            var clearMethod = localPlayerType.GetMethod("ClearConstraints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            clearMethod?.Invoke(null, null);
                        }
                    }
                    catch { }
                    return;
                }

                // Get all constraint trackers on physics rig
                var getComponentsMethod = typeof(Component).GetMethod("GetComponentsInChildren", new Type[] { typeof(bool) });
                if (getComponentsMethod == null) return;

                var genericMethod = getComponentsMethod.MakeGenericMethod(constraintTrackerType);
                var constraints = genericMethod.Invoke(rigManager.physicsRig, new object[] { true });

                if (constraints != null)
                {
                    var enumerable = constraints as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var constraint in enumerable)
                        {
                            try
                            {
                                var deleteMethod = constraintTrackerType.GetMethod("DeleteConstraint");
                                deleteMethod?.Invoke(constraint, null);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        public static void Update()
        {
            if (!_enabled) return;
            ClearConstraints();
        }
    }

    /// <summary>
    /// Infinite Ammo Controller - keeps ammo inventory infinite (magazines still need reloading).
    /// </summary>
    [HarmonyPatch]
    public static class InfiniteAmmoController
    {
        private static bool _enabled = false;

        public static bool IsEnabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (value)
                {
                    Main.MelonLog.Msg("Infinite Ammo ENABLED");
                    EnsureAmmo();
                }
                else
                {
                    Main.MelonLog.Msg("Infinite Ammo DISABLED");
                }
            }
        }

        /// <summary>
        /// Harmony prefix: when ammo is removed, immediately add it back so the count never drops.
        /// This makes the belt always show the same number (appears infinite).
        /// </summary>
        [HarmonyPatch(typeof(AmmoInventory), "RemoveCartridge")]
        [HarmonyPrefix]
        public static void RemoveCartridgePrefix(AmmoInventory __instance, CartridgeData cartridge, int count)
        {
            if (_enabled)
            {
                __instance.AddCartridge(cartridge, count);
            }
        }

        /// <summary>
        /// Harmony prefix: when grabbing from the ammo belt, ensure there's ammo available.
        /// </summary>
        [HarmonyPatch(typeof(InventoryAmmoReceiver), "OnHandGrab")]
        [HarmonyPrefix]
        public static void OnHandGrabPrefix()
        {
            if (!_enabled) return;
            EnsureAmmo();
        }







        /// <summary>Ensure all ammo types have at least some ammo.</summary>
        private static void EnsureAmmo()
        {
            try
            {
                var inv = AmmoInventory.Instance;
                if (inv == null) return;

                if (inv.GetCartridgeCount("light") <= 0)
                    inv.AddCartridge(inv.lightAmmoGroup, 10000);
                if (inv.GetCartridgeCount("medium") <= 0)
                    inv.AddCartridge(inv.mediumAmmoGroup, 10000);
                if (inv.GetCartridgeCount("heavy") <= 0)
                    inv.AddCartridge(inv.heavyAmmoGroup, 10000);
            }
            catch { }
        }

        public static void OnLevelLoaded()
        {
            if (_enabled)
                EnsureAmmo();
        }


    }

    /// <summary>
    /// Level Searcher - Searches for levels to load (FusionProtector style)
    /// </summary>
    public static class LevelSearcher
    {
        private static List<LevelInfo> _allLevels = new List<LevelInfo>();
        private static List<LevelInfo> _filteredLevels = new List<LevelInfo>();
        private static string _searchQuery = "";

        public struct LevelInfo
        {
            public string Title;
            public string BarcodeID;
            public string PalletName;
            public bool IsValid => !string.IsNullOrEmpty(BarcodeID);
        }

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static void LoadAllLevels()
        {
            _allLevels.Clear();
            MelonLoader.MelonCoroutines.Start(LoadLevelsCoroutine());
        }

        private static System.Collections.IEnumerator LoadLevelsCoroutine()
        {
            Main.MelonLog.Msg("Loading levels from all pallets...");

            try
            {
                var assetWarehouseType = FindTypeByName("AssetWarehouse");
                if (assetWarehouseType == null) yield break;

                var instanceProp = assetWarehouseType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var warehouse = instanceProp?.GetValue(null);
                if (warehouse == null) yield break;

                var getPalletsMethod = assetWarehouseType.GetMethod("GetPallets");
                if (getPalletsMethod == null) yield break;

                var pallets = getPalletsMethod.Invoke(warehouse, null) as System.Collections.IEnumerable;
                if (pallets == null) yield break;

                foreach (var pallet in pallets)
                {
                    try
                    {
                        string palletName = pallet.GetType().GetProperty("Title")?.GetValue(pallet)?.ToString() ?? "Unknown";

                        var cratesProp = pallet.GetType().GetProperty("Crates");
                        var crates = cratesProp?.GetValue(pallet) as System.Collections.IEnumerable;
                        if (crates == null) continue;

                        foreach (var crate in crates)
                        {
                            try
                            {
                                // Check if it's a LevelCrate
                                string crateTypeName = crate.GetType().Name;
                                if (!crateTypeName.Contains("LevelCrate")) continue;

                                string title = crate.GetType().GetProperty("Title")?.GetValue(crate)?.ToString() ?? "";
                                string barcodeId = "";

                                var barcodeProp = crate.GetType().GetProperty("Barcode");
                                if (barcodeProp != null)
                                {
                                    var barcode = barcodeProp.GetValue(crate);
                                    if (barcode != null)
                                    {
                                        var idProp = barcode.GetType().GetProperty("ID");
                                        barcodeId = idProp?.GetValue(barcode)?.ToString() ?? "";
                                    }
                                }

                                if (!string.IsNullOrEmpty(barcodeId))
                                {
                                    _allLevels.Add(new LevelInfo
                                    {
                                        Title = title,
                                        BarcodeID = barcodeId,
                                        PalletName = palletName
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                _allLevels = _allLevels.OrderBy(x => x.Title).ToList();
                Main.MelonLog.Msg($"Loaded {_allLevels.Count} levels");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to load levels: {ex.Message}");
            }

            Search();
            yield return null;
        }

        public static void Search()
        {
            _filteredLevels.Clear();
            string query = (_searchQuery ?? "").ToLower().Trim();

            foreach (var level in _allLevels)
            {
                if (string.IsNullOrEmpty(query) ||
                    level.Title.ToLower().Contains(query) ||
                    level.PalletName.ToLower().Contains(query))
                {
                    _filteredLevels.Add(level);
                }
            }
        }

        public static void PopulateLevelResultsPage(Page resultsPage, int maxItems = 50)
        {
            if (resultsPage == null) return;

            // Load levels if not already loaded
            if (_allLevels.Count == 0)
            {
                LoadAllLevels();
            }

            try
            {
                resultsPage.RemoveAll();
            }
            catch { }

            int count = 0;
            foreach (var level in _filteredLevels)
            {
                if (count >= maxItems) break;

                string displayName = $"[{count + 1}] {level.Title}";
                string barcode = level.BarcodeID;

                resultsPage.CreateFunction(displayName, Color.green, () =>
                {
                    LoadLevel(barcode);
                });

                count++;
            }

            if (count == 0)
            {
                resultsPage.CreateFunction("No levels found - Load levels first", Color.gray, LoadAllLevels);
            }

            Main.MelonLog.Msg($"Showing {count} level results");
        }

        private static void LoadLevel(string barcode)
        {
            try
            {
                // Use BoneLib's Scene Loader
                var sceneLoaderType = typeof(Player).Assembly.GetType("BoneLib.SceneUtilities");
                if (sceneLoaderType != null)
                {
                    var loadMethod = sceneLoaderType.GetMethod("LoadLevel", new Type[] { typeof(string) });
                    loadMethod?.Invoke(null, new object[] { barcode });
                    Main.MelonLog.Msg($"Loading level: {barcode}");
                    return;
                }

                // Fallback: Use LabFusion NetworkHelper or direct scene change
                var sceneStreamerType = FindTypeByName("SceneStreamer");
                if (sceneStreamerType != null)
                {
                    var loadMethod = sceneStreamerType.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    loadMethod?.Invoke(null, new object[] { barcode });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to load level: {ex.Message}");
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
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
