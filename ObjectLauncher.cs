using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;

namespace BonelabUtilityMod
{
    public static class ObjectLauncherController
    {
        private static bool launcherEnabled = false;
        private static string currentBarcodeID = "fa534c5a83ee4ec6bd641fec424c4142.Spawnable.PropBowlingBallBig";
        private static string currentItemName = "Bowling Ball Big";
        private static float launchForce = 50f;
        private static float maxLaunchForce = 10000f;
        private static float spawnDistance = 1f;
        private static bool _prevTriggerPressed = false;
        private static float _prevTriggerValue = 0f;
        private static bool fullAutoMode = false;
        private static float lastFireTime = 0f;
        private static float fullAutoDelay = 0.15f;

        // Safety system: holding grip prevents firing; release grip to shoot
        private static bool safetyEnabled = true;

        // Left hand mode
        private static bool useLeftHand = false;

        // Spin velocity (angular velocity applied to spawned objects)
        private static float spinVelocity = 0f;

        // Trajectory preview
        private static bool showTrajectory = true;
        private static GameObject trajectoryObject;
        private static LineRenderer trajectoryLineRenderer;
        private static int trajectorySegments = 20;
        private static float trajectoryTimeStep = 0.1f;

        // Multi-projectile settings
        private static int projectileCount = 1;
        private static float projectileSpacing = 0.3f;
        private static float rotationX = 0f;
        private static float rotationY = 0f;
        private static float rotationZ = 0f;

        // Spawn offset settings
        private static float spawnOffsetX = 0f;
        private static float spawnOffsetY = 0f;

        // Spawn scale (applied to spawned objects)
        private static float spawnScale = 1f;

        // Homing projectile system
        private static bool _homingEnabled = false;
        private static TargetFilter _homingFilter = TargetFilter.NEAREST;
        private static float _homingStrength = 5f;
        private static float _homingMaxLifetime = 10f;
        private static float _homingDuration = 0f; // 0 = unlimited, >0 = stop homing after N seconds
        private static bool _homingRotationLock = false; // rotate projectile to face target
        private static float _homingSpeed = 0f; // 0 = use projectile's current speed, >0 = override base speed
        private static bool _homingAccelEnabled = false;
        private static float _homingAccelRate = 2f;
        private static bool _homingTargetHead = false; // false = torso, true = head
        private static bool _homingMomentum = false; // momentum-based: smooth pursuit, target can dodge
        private static float _homingStayDuration = 2f; // how long to stay on target after reaching it
        private static bool _aimRotationEnabled = false; // rotate projectile to face launch direction
        private static bool _preActivateMenuTap = false; // simulate Y/B menu tap on spawned objects after launch
        private static float _forceDelay = 0.02f; // internal readiness delay (overridden by SpawnForceDelay)

        // PreActivate timing: keep _menuTap true for N frames so component Update() can see it
        private static BaseController _preActivateController = null;
        private static int _preActivateFramesRemaining = 0;
        private const int PRE_ACTIVATE_FRAME_COUNT = 5;
        private static List<HomingProjectile> _homingProjectiles = new List<HomingProjectile>();

        private struct HomingProjectile
        {
            public Rigidbody Rb;
            public float SpawnTime;
            public float CurrentSpeed; // tracked speed for acceleration mode
            public float StayStartTime; // -1 = not yet on target
        }

        // ── Launched object tracking & cleanup ──
        private static List<GameObject> _launchedObjects = new List<GameObject>();
        private static bool _autoCleanupEnabled = false;
        private static float _autoCleanupInterval = 30f; // seconds
        private static float _lastCleanupTime = 0f;
        private static float _spawnForceDelay = 0.02f; // delay between spawn and force application

        // ── Per-object auto-despawn ──
        private static bool _autoDespawnEnabled = false;
        private static float _autoDespawnDelay = 10f; // seconds per object
        private struct DespawnTimer
        {
            public GameObject Obj;
            public float DespawnTime;
        }
        private static List<DespawnTimer> _despawnTimers = new List<DespawnTimer>();

        // ── Claimed set for multi-projectile force resolution ──
        private static HashSet<int> _claimedForceTargets = new HashSet<int>();

        // ── Cached type lookups for per-frame registration (avoid per-frame assembly scan) ──
        private static Type _cachedMarrowEntityType = null;
        private static bool _cachedMarrowEntityTypeLookedUp = false;
        private static Type _cachedPropSenderType = null;
        private static bool _cachedPropSenderTypeLookedUp = false;
        private static MethodInfo _cachedSendPropCreation = null;

        // Search system
        private static List<SearchableItem> _allItems = new List<SearchableItem>();
        private static List<SearchableItem> _filteredItems = new List<SearchableItem>();
        private static string _searchQuery = "";
        private static int _selectedSearchIndex = 0;

        /// <summary>
        /// Returns all loaded spawnables as barcode→title pairs for external use (QuickMenu).
        /// </summary>
        public static Dictionary<string, string> GetAllSpawnables()
        {
            if (_allItems.Count == 0) RefreshSearchList();
            var result = new Dictionary<string, string>();
            foreach (var item in _allItems)
            {
                if (!result.ContainsKey(item.BarcodeID))
                    result[item.BarcodeID] = item.Title;
            }
            return result;
        }

        public struct SearchableItem
        {
            public string Title;
            public string BarcodeID;
        }

        public static string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value ?? "";
                FilterSearchItems();
            }
        }

        public static bool IsFullAuto
        {
            get => fullAutoMode;
            set
            {
                if (fullAutoMode == value) return;
                fullAutoMode = value;
                Main.MelonLog.Msg($"Fire mode: {(value ? "Full-Auto" : "Semi-Auto")}");
            }
        }

        public static float FullAutoDelay
        {
            get => fullAutoDelay;
            set
            {
                fullAutoDelay = Mathf.Clamp(value, 0.01f, 1f);
            }
        }

        /// <summary>
        /// Called every frame while the fire keybind is held and full-auto is on.
        /// Respects FullAutoDelay between shots.
        /// </summary>
        public static void KeybindFullAutoFire()
        {
            if (Time.time - lastFireTime >= fullAutoDelay)
            {
                LaunchObject();
                lastFireTime = Time.time;
            }
        }

        public static bool ShowTrajectory
        {
            get => showTrajectory;
            set
            {
                if (showTrajectory == value) return;
                showTrajectory = value;
                if (!value)
                {
                    DestroyTrajectory();
                }
                Main.MelonLog.Msg($"Trajectory preview: {(value ? "ON" : "OFF")}");
            }
        }

        public static int ProjectileCount
        {
            get => projectileCount;
            set
            {
                int clamped = Mathf.Clamp(value, 1, 25);
                if (projectileCount == clamped) return;
                projectileCount = clamped;
            }
        }

        public static float ProjectileSpacing
        {
            get => projectileSpacing;
            set
            {
                projectileSpacing = Mathf.Max(value, 0.1f);
            }
        }

        public static float RotationX
        {
            get => rotationX;
            set => rotationX = value % 360f;
        }

        public static float RotationY
        {
            get => rotationY;
            set => rotationY = value % 360f;
        }

        public static float RotationZ
        {
            get => rotationZ;
            set => rotationZ = value % 360f;
        }

        public static float SpawnScale
        {
            get => spawnScale;
            set
            {
                float clamped = Mathf.Clamp(value, 0.1f, 10f);
                if (spawnScale == clamped) return;
                spawnScale = clamped;
            }
        }

        public static bool HomingEnabled
        {
            get => _homingEnabled;
            set
            {
                if (_homingEnabled == value) return;
                _homingEnabled = value;
                if (!value) _homingProjectiles.Clear();
                Main.MelonLog.Msg($"Homing: {(value ? "ON" : "OFF")}");
            }
        }

        public static TargetFilter HomingFilter
        {
            get => _homingFilter;
            set
            {
                if (_homingFilter == value) return;
                _homingFilter = value;
                Main.MelonLog.Msg($"Homing Filter: {value}");
            }
        }

        public static float HomingStrength
        {
            get => _homingStrength;
            set
            {
                _homingStrength = Mathf.Clamp(value, 1f, 50f);
            }
        }

        public static float HomingDuration
        {
            get => _homingDuration;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (_homingDuration == clamped) return;
                _homingDuration = clamped;
            }
        }

        public static bool HomingRotationLock
        {
            get => _homingRotationLock;
            set
            {
                if (_homingRotationLock == value) return;
                _homingRotationLock = value;
            }
        }

        public static float HomingSpeed
        {
            get => _homingSpeed;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (_homingSpeed == clamped) return;
                _homingSpeed = clamped;
            }
        }

        public static bool HomingAccelEnabled
        {
            get => _homingAccelEnabled;
            set
            {
                if (_homingAccelEnabled == value) return;
                _homingAccelEnabled = value;
            }
        }

        public static float HomingAccelRate
        {
            get => _homingAccelRate;
            set
            {
                _homingAccelRate = Mathf.Clamp(value, 0.1f, 10f);
            }
        }

        public static bool HomingTargetHead
        {
            get => _homingTargetHead;
            set
            {
                if (_homingTargetHead == value) return;
                _homingTargetHead = value;
            }
        }

        public static bool HomingMomentum
        {
            get => _homingMomentum;
            set
            {
                if (_homingMomentum == value) return;
                _homingMomentum = value;
            }
        }

        public static float HomingStayDuration
        {
            get => _homingStayDuration;
            set => _homingStayDuration = Mathf.Clamp(value, 0f, 30f);
        }

        public static bool AimRotationEnabled
        {
            get => _aimRotationEnabled;
            set
            {
                if (_aimRotationEnabled == value) return;
                _aimRotationEnabled = value;
            }
        }

        public static bool PreActivateMenuTap
        {
            get => _preActivateMenuTap;
            set
            {
                if (_preActivateMenuTap == value) return;
                _preActivateMenuTap = value;
            }
        }

        public static float ForceDelay
        {
            get => _spawnForceDelay;
            set
            {
                _spawnForceDelay = Mathf.Clamp(value, 0f, 2f);
                _forceDelay = _spawnForceDelay;
            }
        }

        // ── Cleanup properties ──
        public static bool AutoCleanupEnabled
        {
            get => _autoCleanupEnabled;
            set => _autoCleanupEnabled = value;
        }

        public static float AutoCleanupInterval
        {
            get => _autoCleanupInterval;
            set => _autoCleanupInterval = Mathf.Clamp(value, 1f, 300f);
        }

        public static float SpawnForceDelay
        {
            get => _spawnForceDelay;
            set => _spawnForceDelay = Mathf.Clamp(value, 0f, 2f);
        }

        public static bool AutoDespawnEnabled
        {
            get => _autoDespawnEnabled;
            set => _autoDespawnEnabled = value;
        }

        public static float AutoDespawnDelay
        {
            get => _autoDespawnDelay;
            set => _autoDespawnDelay = Mathf.Clamp(value, 1f, 300f);
        }

        /// <summary>
        /// Despawn all objects that were launched by the Object Launcher and match the current barcode.
        /// </summary>
        public static void DespawnLaunchedObjects()
        {
            int count = 0;
            for (int i = _launchedObjects.Count - 1; i >= 0; i--)
            {
                var go = _launchedObjects[i];
                if (go == null)
                {
                    _launchedObjects.RemoveAt(i);
                    continue;
                }
                // Try FusionSync'd network despawn first, fall back to local destroy
                bool networkDespawned = false;
                try { networkDespawned = DespawnAllController.TryDespawnObject(go); } catch { }
                if (!networkDespawned)
                {
                    try { UnityEngine.Object.Destroy(go); } catch { }
                }
                count++;
                _launchedObjects.RemoveAt(i);
            }
            SpawnTagRegistry.Cleanup();
            if (count > 0)
            {
                Main.MelonLog.Msg($"[OL Cleanup] Despawned {count} launched objects");
                SendNotification(NotificationType.Success, $"Despawned {count} launched objects");
            }
            else
            {
                SendNotification(NotificationType.Information, "No launched objects to despawn");
            }
        }

        /// <summary>
        /// Track a launched object for later cleanup.
        /// </summary>
        private static void TrackLaunchedObject(GameObject go)
        {
            if (go == null) return;
            _launchedObjects.Add(go);
            // Schedule per-object auto-despawn if enabled
            if (_autoDespawnEnabled)
            {
                _despawnTimers.Add(new DespawnTimer { Obj = go, DespawnTime = Time.time + _autoDespawnDelay });
            }
            // Prune null entries periodically
            if (_launchedObjects.Count > 200)
            {
                _launchedObjects.RemoveAll(o => o == null);
            }
        }

        private struct PendingRegistration
        {
            public GameObject Obj;
            public float StartTime;
        }

        private struct PendingForce
        {
            public GameObject Obj;
            public float StartTime;
            public float Force;
            public Vector3 SpawnPos;
            public HashSet<int> PreExistingIds;
            public Vector3 ForwardDir;
            public string BarcodeId;
            public string BarcodeToken;
            public float Scale;
            public string Tag;
        }

        private static List<PendingRegistration> _pendingRegistrations = new List<PendingRegistration>();
        private static List<PendingForce> _pendingForces = new List<PendingForce>();

        public static bool IsLauncherEnabled
        {
            get => launcherEnabled;
            set
            {
                if (launcherEnabled == value) return;
                launcherEnabled = value;
                Main.MelonLog.Msg($"Object Launcher {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static float LaunchForce
        {
            get => launchForce;
            set
            {
                launchForce = Mathf.Clamp(value, 1f, maxLaunchForce);
            }
        }

        public static float SpawnDistance
        {
            get => spawnDistance;
            set
            {
                spawnDistance = Mathf.Clamp(value, 0.5f, 10f);
            }
        }

        public static float SpawnOffsetX
        {
            get => spawnOffsetX;
            set
            {
                spawnOffsetX = Mathf.Clamp(value, -10f, 10f);
            }
        }

        public static float SpawnOffsetY
        {
            get => spawnOffsetY;
            set
            {
                spawnOffsetY = Mathf.Clamp(value, -10f, 10f);
            }
        }

        public static bool SafetyEnabled
        {
            get => safetyEnabled;
            set
            {
                safetyEnabled = value;
                Main.MelonLog.Msg($"Launcher Safety: {(value ? "ON (Release Grip to Fire)" : "OFF")}");
            }
        }

        public static bool UseLeftHand
        {
            get => useLeftHand;
            set
            {
                useLeftHand = value;
                Main.MelonLog.Msg($"Launcher Hand: {(value ? "LEFT" : "RIGHT")}");
            }
        }

        public static float SpinVelocity
        {
            get => spinVelocity;
            set
            {
                spinVelocity = Mathf.Clamp(value, 0f, 5000f);
            }
        }

        public static string CurrentBarcodeID
        {
            get => currentBarcodeID;
            set => currentBarcodeID = value ?? "";
        }

        public static string CurrentItemName
        {
            get => currentItemName;
            set => currentItemName = value ?? "";
        }

        // ============================================
        // PRESET SYSTEM
        // ============================================

        public struct LauncherPreset
        {
            public string Name;
            public string BarcodeID;
            public string ItemName;
            public float LaunchForce;
            public float SpawnDistance;
            public float SpawnOffsetX;
            public float SpawnOffsetY;
            public float SpinVelocity;
            public float RotationX;
            public float RotationY;
            public float RotationZ;
            public float Scale;
        }

        private static List<LauncherPreset> _presets = new List<LauncherPreset>();
        private static string _presetName = "";

        public static string PresetName
        {
            get => _presetName;
            set => _presetName = value ?? "";
        }

        public static List<LauncherPreset> GetPresets() => _presets;

        /// <summary>
        /// Replace the full preset list (used by SettingsManager on load).
        /// </summary>
        public static void SetPresets(List<LauncherPreset> presets)
        {
            _presets.Clear();
            if (presets != null)
                _presets.AddRange(presets);
            Main.MelonLog.Msg($"[Preset] Loaded {_presets.Count} presets");
        }

        /// <summary>
        /// Capture all current launcher settings into a preset.
        /// </summary>
        public static LauncherPreset CaptureCurrentAsPreset(string name)
        {
            return new LauncherPreset
            {
                Name = name,
                BarcodeID = currentBarcodeID,
                ItemName = currentItemName,
                LaunchForce = launchForce,
                SpawnDistance = spawnDistance,
                SpawnOffsetX = spawnOffsetX,
                SpawnOffsetY = spawnOffsetY,
                SpinVelocity = spinVelocity,
                RotationX = rotationX,
                RotationY = rotationY,
                RotationZ = rotationZ,
                Scale = spawnScale
            };
        }

        /// <summary>
        /// Apply a preset's settings to the launcher.
        /// </summary>
        public static void ApplyPreset(LauncherPreset preset)
        {
            currentBarcodeID = preset.BarcodeID ?? currentBarcodeID;
            currentItemName = preset.ItemName ?? currentItemName;
            launchForce = Mathf.Clamp(preset.LaunchForce, 1f, maxLaunchForce);
            spawnDistance = Mathf.Clamp(preset.SpawnDistance, 0.5f, 10f);
            spawnOffsetX = Mathf.Clamp(preset.SpawnOffsetX, -10f, 10f);
            spawnOffsetY = Mathf.Clamp(preset.SpawnOffsetY, -10f, 10f);
            spinVelocity = Mathf.Clamp(preset.SpinVelocity, 0f, 5000f);
            rotationX = preset.RotationX % 360f;
            rotationY = preset.RotationY % 360f;
            rotationZ = preset.RotationZ % 360f;
            spawnScale = Mathf.Clamp(preset.Scale > 0 ? preset.Scale : 1f, 0.1f, 10f);
            SettingsManager.MarkDirty();
            Main.MelonLog.Msg($"[Preset] Applied preset '{preset.Name}': {preset.ItemName} Force={preset.LaunchForce}");
            SendNotification(NotificationType.Success, $"Loaded preset: {preset.Name}");
        }

        /// <summary>
        /// Save current settings as a named preset (overwrites if name exists).
        /// </summary>
        public static void SavePreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                // Auto-generate name from item name + count
                name = $"{currentItemName} #{_presets.Count + 1}";
            }

            _presets.RemoveAll(p => p.Name == name);
            var preset = CaptureCurrentAsPreset(name);
            _presets.Add(preset);
            SettingsManager.MarkDirty();
            Main.MelonLog.Msg($"[Preset] Saved preset '{name}': {currentItemName} Force={launchForce}");
            SendNotification(NotificationType.Success, $"Saved preset: {name}");
        }

        /// <summary>
        /// Delete a preset by name.
        /// </summary>
        public static void DeletePreset(string name)
        {
            int removed = _presets.RemoveAll(p => p.Name == name);
            if (removed > 0)
            {
                SettingsManager.MarkDirty();
                Main.MelonLog.Msg($"[Preset] Deleted preset '{name}'");
                SendNotification(NotificationType.Success, $"Deleted preset: {name}");
            }
        }

        /// <summary>
        /// Serialize all presets to a pipe-delimited string for MelonPreferences storage.
        /// Format per preset: Name|BarcodeID|ItemName|LaunchForce|SpawnDistance|OffsetH|OffsetV|SpinVelocity
        /// Presets separated by ;;
        /// </summary>
        public static string SerializePresets()
        {
            var parts = new List<string>();
            foreach (var p in _presets)
            {
                parts.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                    (p.Name ?? "").Replace("|", "_").Replace(";;", "__"),
                    (p.BarcodeID ?? "").Replace("|", "_"),
                    (p.ItemName ?? "").Replace("|", "_").Replace(";;", "__"),
                    p.LaunchForce, p.SpawnDistance, p.SpawnOffsetX, p.SpawnOffsetY, p.SpinVelocity,
                    p.RotationX, p.RotationY, p.RotationZ, p.Scale));
            }
            return string.Join(";;", parts);
        }

        /// <summary>
        /// Deserialize presets from a pipe-delimited string.
        /// </summary>
        public static void DeserializePresets(string data)
        {
            _presets.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var entry in data.Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length >= 8)
                {
                    try
                    {
                        _presets.Add(new LauncherPreset
                        {
                            Name = parts[0],
                            BarcodeID = parts[1],
                            ItemName = parts[2],
                            LaunchForce = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnDistance = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnOffsetX = float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnOffsetY = float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture),
                            SpinVelocity = float.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture),
                            RotationX = parts.Length > 8 ? float.Parse(parts[8], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            RotationY = parts.Length > 9 ? float.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            RotationZ = parts.Length > 10 ? float.Parse(parts[10], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            Scale = parts.Length > 11 ? float.Parse(parts[11], System.Globalization.CultureInfo.InvariantCulture) : 1f
                        });
                    }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"[Preset] Failed to parse preset entry: {ex.Message}");
                    }
                }
            }
            Main.MelonLog.Msg($"[Preset] Deserialized {_presets.Count} presets");
        }

        /// <summary>
        /// Populate a BoneMenu page with saved presets for loading.
        /// </summary>
        public static void PopulatePresetLoadPage(Page page)
        {
            if (page == null) return;
            try { page.RemoveAll(); } catch { }

            if (_presets.Count == 0)
            {
                page.CreateFunction("No presets saved", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                string display = $"{preset.Name} ({preset.ItemName}, F:{preset.LaunchForce})";
                page.CreateFunction(display, Color.green, () => ApplyPreset(preset));
            }
        }

        /// <summary>
        /// Populate a BoneMenu page with saved presets for deletion.
        /// </summary>
        public static void PopulatePresetDeletePage(Page page)
        {
            if (page == null) return;
            try { page.RemoveAll(); } catch { }

            if (_presets.Count == 0)
            {
                page.CreateFunction("No presets to delete", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                string name = preset.Name;
                page.CreateFunction($"Delete: {name}", Color.red, () =>
                {
                    DeletePreset(name);
                    PopulatePresetDeletePage(page); // Refresh after delete
                });
            }
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Object Launcher controller initialized");
        }

        public static void OnLevelUnloaded()
        {
            DestroyTrajectory();
            _launchedObjects.Clear();
            _pendingRegistrations.Clear();
            _pendingForces.Clear();
            _preActivateController = null;
            _preActivateFramesRemaining = 0;
        }

        private static void DestroyTrajectory()
        {
            if (trajectoryObject != null)
            {
                try { UnityEngine.Object.Destroy(trajectoryObject); } catch { }
            }
            trajectoryObject = null;
            trajectoryLineRenderer = null;
        }

        // ============================================
        // SEARCH SYSTEM METHODS
        // ============================================

        private static string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, "<[^>]*>", "");
        }

        public static void RefreshSearchList()
        {
            try
            {
                Main.MelonLog.Msg("Refreshing searchable items for launcher...");
                _allItems.Clear();

                // Find AssetWarehouse type
                var assetWarehouseType = FindTypeByName("AssetWarehouse");
                if (assetWarehouseType == null)
                {
                    Main.MelonLog.Error("AssetWarehouse type not found!");
                    SendNotification(NotificationType.Error, "AssetWarehouse not found");
                    return;
                }

                // Get Instance property
                var instanceProp = assetWarehouseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    Main.MelonLog.Error("AssetWarehouse.Instance not found!");
                    return;
                }

                var warehouseInstance = instanceProp.GetValue(null);
                if (warehouseInstance == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse.Instance is null");
                    SendNotification(NotificationType.Warning, "Game not fully loaded");
                    return;
                }

                // Find SpawnableCrate type
                var spawnableCrateType = FindTypeByName("SpawnableCrate");
                if (spawnableCrateType == null)
                {
                    Main.MelonLog.Error("SpawnableCrate type not found!");
                    return;
                }

                // Call GetCrates<SpawnableCrate>(null)
                var getCratesMethod = assetWarehouseType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetCrates" && m.IsGenericMethod);

                if (getCratesMethod == null)
                {
                    Main.MelonLog.Error("GetCrates method not found!");
                    return;
                }

                var genericGetCrates = getCratesMethod.MakeGenericMethod(spawnableCrateType);
                var crates = genericGetCrates.Invoke(warehouseInstance, new object[] { null });

                if (crates == null)
                {
                    Main.MelonLog.Warning("GetCrates returned null");
                    return;
                }

                // Iterate through crates
                var cratesType = crates.GetType();
                var countProp = cratesType.GetProperty("Count");
                var itemProp = cratesType.GetProperty("Item");

                if (countProp != null && itemProp != null)
                {
                    int count = (int)countProp.GetValue(crates);
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var crate = itemProp.GetValue(crates, new object[] { i });
                            if (crate != null)
                            {
                                AddCrateToSearchList(crate);
                            }
                        }
                        catch { }
                    }
                }

                // Sort alphabetically
                _allItems = _allItems.OrderBy(x => x.Title).ToList();
                FilterSearchItems();

                Main.MelonLog.Msg($"Loaded {_allItems.Count} items for launcher search");
                SendNotification(NotificationType.Success, $"Loaded {_allItems.Count} items");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to refresh search list: {ex.Message}");
                SendNotification(NotificationType.Error, "Failed to load items");
            }
        }

        private static void AddCrateToSearchList(object crate)
        {
            try
            {
                var crateType = crate.GetType();

                // Get Title
                string title = null;
                var titleProp = crateType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                if (titleProp != null)
                {
                    var titleObj = titleProp.GetValue(crate);
                    title = titleObj?.ToString();
                }

                // Get Barcode.ID
                string barcodeId = null;
                var barcodeProp = crateType.GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                if (barcodeProp != null)
                {
                    var barcode = barcodeProp.GetValue(crate);
                    if (barcode != null)
                    {
                        var idProp = barcode.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                        if (idProp != null)
                        {
                            barcodeId = idProp.GetValue(barcode) as string;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(barcodeId))
                {
                    if (string.IsNullOrEmpty(title))
                    {
                        int lastDot = barcodeId.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot + 1 < barcodeId.Length)
                        {
                            title = barcodeId.Substring(lastDot + 1);
                            title = Regex.Replace(title, "([a-z])([A-Z])", "$1 $2");
                        }
                        else
                        {
                            title = barcodeId;
                        }
                    }

                    _allItems.Add(new SearchableItem
                    {
                        Title = StripRichText(title),
                        BarcodeID = barcodeId
                    });
                }
            }
            catch { }
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

        private static void FilterSearchItems()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredItems = new List<SearchableItem>(_allItems);
            }
            else
            {
                var query = _searchQuery.ToLower();
                _filteredItems = _allItems
                    .Where(item => item.Title.ToLower().Contains(query) || item.BarcodeID.ToLower().Contains(query))
                    .ToList();
            }
            _selectedSearchIndex = 0;

            // Show notification with results
            if (_filteredItems.Count > 0)
            {
                var firstItem = _filteredItems[0];
                SendNotification(NotificationType.Information, $"Found {_filteredItems.Count} items\n[1] {firstItem.Title}");
            }
            else if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                SendNotification(NotificationType.Warning, "No items found");
            }
        }

        public static void NextSearchItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }
            _selectedSearchIndex = (_selectedSearchIndex + 1) % _filteredItems.Count;
            ShowCurrentSearchItem();
        }

        public static void PreviousSearchItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }
            _selectedSearchIndex--;
            if (_selectedSearchIndex < 0)
                _selectedSearchIndex = _filteredItems.Count - 1;
            ShowCurrentSearchItem();
        }

        private static void ShowCurrentSearchItem()
        {
            if (_filteredItems.Count == 0 || _selectedSearchIndex < 0 || _selectedSearchIndex >= _filteredItems.Count)
                return;

            var item = _filteredItems[_selectedSearchIndex];
            SendNotification(NotificationType.Information, $"[{_selectedSearchIndex + 1}/{_filteredItems.Count}] {item.Title}");
        }

        public static void CopyBarcodeFromSearch()
        {
            if (_filteredItems.Count == 0 || _selectedSearchIndex < 0 || _selectedSearchIndex >= _filteredItems.Count)
            {
                SendNotification(NotificationType.Warning, "No item selected");
                return;
            }

            var item = _filteredItems[_selectedSearchIndex];
            currentBarcodeID = item.BarcodeID;
            currentItemName = item.Title;

            Main.MelonLog.Msg($"Copied barcode: {item.Title} ({item.BarcodeID})");
            SendNotification(NotificationType.Success, $"Copied: {item.Title}");
        }

        /// <summary>
        /// Sync items from SpawnableSearcher (used via SetInLauncher action)
        /// The new SpawnableSearcher uses SetInLauncher to set CurrentBarcodeID directly
        /// </summary>
        public static void SyncFromSpawnableSearcher()
        {
            // New workflow: Use SpawnableSearcher's "Set in Launcher" action
            // which sets ObjectLauncherController.CurrentBarcodeID directly
            SendNotification(NotificationType.Information, "Use 'Set In Launcher' action in Spawn Menu to copy items");
        }

        // ============================================
        // END SEARCH SYSTEM
        // ============================================

        public static void Update()
        {
            // PreActivate frame countdown: reset _menuTap after N frames
            if (_preActivateFramesRemaining > 0)
            {
                _preActivateFramesRemaining--;
                if (_preActivateFramesRemaining <= 0 && _preActivateController != null)
                {
                    _preActivateController._menuTap = false;
                    _preActivateController = null;
                    Main.MelonLog.Msg("[PreActivate] _menuTap reset after frame countdown");
                }
            }

            // Panic key: backslash always works even when launcher is disabled
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                DespawnLaunchedObjects();
            }

            // Auto-cleanup timer
            if (_autoCleanupEnabled && _launchedObjects.Count > 0)
            {
                if (Time.time - _lastCleanupTime >= _autoCleanupInterval)
                {
                    _lastCleanupTime = Time.time;
                    DespawnLaunchedObjects();
                }
            }

            // Per-object auto-despawn timers
            if (_despawnTimers.Count > 0)
            {
                float now = Time.time;
                for (int i = _despawnTimers.Count - 1; i >= 0; i--)
                {
                    var dt = _despawnTimers[i];
                    if (dt.Obj == null)
                    {
                        _despawnTimers.RemoveAt(i);
                        continue;
                    }
                    if (now >= dt.DespawnTime)
                    {
                        bool networkDespawned = false;
                        try { networkDespawned = DespawnAllController.TryDespawnObject(dt.Obj); } catch { }
                        if (!networkDespawned)
                        {
                            try { UnityEngine.Object.Destroy(dt.Obj); } catch { }
                        }
                        _launchedObjects.Remove(dt.Obj);
                        _despawnTimers.RemoveAt(i);
                    }
                }
            }

            if (!launcherEnabled)
            {
                // Hide trajectory when launcher is disabled
                DestroyTrajectory();
                return;
            }

            // Read input FIRST (needed by trajectory and firing logic)
            bool triggerPressed = false;
            bool gripHeld = false;
            float triggerValue = 0f;

            try
            {
                if (useLeftHand)
                {
                    float leftTrigger = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger");
                    triggerValue = leftTrigger;
                    if (leftTrigger > 0.5f) triggerPressed = true;
                    float leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger");
                    if (leftGrip > 0.5f) gripHeld = true;
                }
                else
                {
                    float rightTrigger = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger");
                    triggerValue = rightTrigger;
                    if (rightTrigger > 0.5f) triggerPressed = true;
                    float rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger");
                    if (rightGrip > 0.5f) gripHeld = true;
                }
            }
            catch { }

            // Update trajectory preview
            if (showTrajectory)
            {
                // Hide trajectory when safety is on and grip is held (about to fire)
                if (safetyEnabled && gripHeld)
                {
                    if (trajectoryObject != null)
                        trajectoryObject.SetActive(false);
                }
                else
                {
                    UpdateTrajectoryPreview();
                }
            }
            else
            {
                DestroyTrajectory();
            }

            // Safety check: holding grip blocks firing; release grip to shoot
            if (safetyEnabled && gripHeld)
            {
                triggerPressed = false;
            }

            // Fire based on mode
            bool currentPressed = triggerPressed;
            if (fullAutoMode)
            {
                // First shot fires immediately on rising edge
                if (currentPressed && !_prevTriggerPressed)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
                // Continuous fire: require trigger held AND value not decreasing (releasing)
                else if (currentPressed && _prevTriggerPressed && triggerValue >= _prevTriggerValue - 0.05f && Time.time - lastFireTime >= fullAutoDelay)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
            }
            else
            {
                // Semi-auto: only fire on trigger press rising edge
                if (currentPressed && !_prevTriggerPressed)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
            }
            _prevTriggerPressed = currentPressed;
            _prevTriggerValue = triggerValue;

            // Process any pending LabFusion registrations (delayed to allow object initialization)
            if (_pendingRegistrations.Count > 0)
            {
                // Cache MarrowEntity type once
                if (!_cachedMarrowEntityTypeLookedUp)
                {
                    _cachedMarrowEntityType = FindTypeInAssembly("MarrowEntity", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("MarrowEntity", "");
                    _cachedMarrowEntityTypeLookedUp = true;
                }
                // Cache PropSender type + method once
                if (!_cachedPropSenderTypeLookedUp)
                {
                    _cachedPropSenderType = FindTypeInAssembly("PropSender", "LabFusion") ?? FindTypeInAssembly("PropSender", "LabFusion.Senders") ?? FindTypeInAssembly("PropSender", "");
                    if (_cachedPropSenderType != null)
                        _cachedSendPropCreation = _cachedPropSenderType.GetMethod("SendPropCreation", BindingFlags.Public | BindingFlags.Static);
                    _cachedPropSenderTypeLookedUp = true;
                }

                for (int i = _pendingRegistrations.Count - 1; i >= 0; --i)
                {
                    var pr = _pendingRegistrations[i];
                    var go = pr.Obj;
                    if (go == null)
                    {
                        _pendingRegistrations.RemoveAt(i);
                        continue;
                    }

                    // Try to find MarrowEntity using cached type (avoid per-frame GetType + string matching)
                    UnityEngine.Component marrowEntity = null;
                    try
                    {
                        if (_cachedMarrowEntityType != null)
                        {
                            var comp = go.GetComponent(_cachedMarrowEntityType.Name);
                            if (comp != null)
                                marrowEntity = comp;
                        }
                        if (marrowEntity == null)
                        {
                            // Fallback: check by name without iterating all components
                            marrowEntity = go.GetComponent("MarrowEntity");
                        }
                    }
                    catch { }

                    if (marrowEntity != null)
                    {
                        try
                        {
                            var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                            if (rb == null) continue;

                            if (Time.time - pr.StartTime < 0.05f) continue;

                            if (_cachedSendPropCreation != null)
                            {
                                _cachedSendPropCreation.Invoke(null, new object[] { marrowEntity, null, false });
                            }
                        }
                        catch (Exception e)
                        {
                            Main.MelonLog.Warning($"Delayed PropSender.SendPropCreation failed: {e.ToString()}");
                        }

                        _pendingRegistrations.RemoveAt(i);
                        continue;
                    }

                    if (Time.time - pr.StartTime > 5f)
                    {
                        _pendingRegistrations.RemoveAt(i);
                    }
                }
            }

            // Process any pending force applications (delayed to allow Rigidbody initialization)
            if (_pendingForces.Count > 0)
            {
                // Add currently resolved objects to claimed set (do NOT clear — keep objects
                // that already had force applied so other PendingForces don't re-match them)
                for (int i = 0; i < _pendingForces.Count; i++)
                {
                    var existing = _pendingForces[i];
                    if (existing.Obj != null)
                        _claimedForceTargets.Add(existing.Obj.GetInstanceID());
                }

                for (int i = _pendingForces.Count - 1; i >= 0; --i)
                {
                    var pf = _pendingForces[i];
                    var go = pf.Obj;
                    if (go == null)
                    {
                        // Try tag registry first (most reliable)
                        var tagged = SpawnTagRegistry.Resolve(pf.Tag);
                        if (tagged != null)
                        {
                            pf.Obj = tagged;
                            _pendingForces[i] = pf;
                            go = tagged;
                            _claimedForceTargets.Add(go.GetInstanceID());
                            SpawnTagRegistry.Remove(pf.Tag);
                        }
                        // If tag exists but hasn't resolved yet, wait before falling through
                        // to proximity — prevents wrong-object matching under high latency
                        else if (!string.IsNullOrEmpty(pf.Tag) && Time.time - pf.StartTime < 2f)
                        {
                            continue;
                        }
                        else if (TryResolveForceTarget(ref pf, out var resolvedGo, out var resolvedRb, out var resolvedDist))
                        {
                            _pendingForces[i] = pf;
                            go = resolvedGo;
                            // Mark as claimed so other PendingForces won't grab the same object
                            _claimedForceTargets.Add(go.GetInstanceID());
                        }
                        else
                        {
                            if (Time.time - pf.StartTime > 5f)
                            {
                                Main.MelonLog.Warning("Could not resolve spawned object for force application within 5s; despawning");
                                // Try to find and despawn any object that appeared near spawn position
                                TryDespawnNearPosition(pf.SpawnPos, pf.PreExistingIds);
                                _pendingForces.RemoveAt(i);
                            }
                            continue;
                        }
                    }

                    // Check if Rigidbody is ready - search aggressively (including inactive, parent, and deep children)
                    var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>(includeInactive: true);
                    if (rb == null && go.transform.parent != null)
                        rb = go.transform.parent.GetComponent<Rigidbody>() ?? go.transform.parent.GetComponentInChildren<Rigidbody>(includeInactive: true);

                    if (rb == null)
                    {
                        float waitTime = Time.time - pf.StartTime;
                        if (waitTime < 5f)  // Wait up to 5 seconds for Rigidbody to appear
                        {
                            // Silently retry without logging every frame to reduce spam
                            continue;
                        }
                        else
                        {
                            Main.MelonLog.Warning($"Rigidbody not found after {waitTime:0.00}s on '{go.name}' or parents; trying to apply force to first Rigidbody in vicinity instead");

                            // As a last resort, find ANY Rigidbody in the vicinity and apply force to it
                            var allRigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                            foreach (var nearbyRb in allRigidbodies)
                            {
                                if (nearbyRb != null && !IsPlayerRigidbody(nearbyRb) && Vector3.Distance(nearbyRb.transform.position, go.transform.position) < 1.5f)
                                {
                                    rb = nearbyRb;
                                    break;
                                }
                            }

                            if (rb == null)
                            {
                                Main.MelonLog.Error($"Still no Rigidbody found; despawning object");
                                try { UnityEngine.Object.Destroy(go); } catch { }
                                _pendingForces.RemoveAt(i);
                                continue;
                            }
                        }
                    }

                    // Freeze object during wait to prevent falling before force is applied
                    if (Time.time - pf.StartTime < _spawnForceDelay)
                    {
                        try
                        {
                            rb.useGravity = false;
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                        catch { }
                        continue;
                    }

                    // Apply the force
                    try
                    {
                        var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                        Vector3 launchDirection = pf.ForwardDir.sqrMagnitude > 0.0001f
                            ? pf.ForwardDir.normalized
                            : activeHand.transform.forward.normalized;

                        if (rb.isKinematic)
                            rb.isKinematic = false;

                        // Restore gravity and clear any frozen axes
                        rb.useGravity = true;
                        rb.constraints = RigidbodyConstraints.None;
                        rb.WakeUp();

                        // Apply scale if not 1.0
                        if (pf.Scale > 0f && Mathf.Abs(pf.Scale - 1f) > 0.001f)
                        {
                            try
                            {
                                var scaleTarget = rb.GetComponentInParent<Rigidbody>() ?? rb;
                                var scaleTransform = ((Component)scaleTarget).transform;
                                Vector3 originalScale = scaleTransform.localScale;
                                Vector3 newScale = originalScale * pf.Scale;
                                scaleTransform.localScale = newScale;
                                // Adjust mass proportionally (volume-based)
                                float origVol = originalScale.x * originalScale.y * originalScale.z;
                                float newVol = newScale.x * newScale.y * newScale.z;
                                if (origVol > 0.0001f)
                                    scaleTarget.mass = scaleTarget.mass / origVol * newVol;
                            }
                            catch (Exception scaleEx)
                            {
                                Main.MelonLog.Warning($"Scale application failed: {scaleEx.Message}");
                            }
                        }

                        // Apply rotation to face launch direction (like SmashBone offset)
                        if (_aimRotationEnabled && launchDirection.sqrMagnitude > 0.01f)
                        {
                            Quaternion aimRot = Quaternion.LookRotation(launchDirection);
                            go.transform.rotation = aimRot * Quaternion.Euler(rotationX, rotationY, rotationZ);
                        }

                        // Zero out existing velocity before applying force
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;

                        // Apply as velocity AND impulse for reliability
                        rb.velocity = launchDirection * pf.Force;
                        rb.AddForce(launchDirection * pf.Force * 0.1f, ForceMode.VelocityChange);

                        // Apply spin velocity if configured
                        if (spinVelocity != 0f)
                        {
                            rb.angularVelocity = launchDirection * spinVelocity;
                        }

                        rb.WakeUp();

                        // Pre-activate: simulate Y/B menu tap on spawned object
                        if (_preActivateMenuTap)
                        {
                            try { TryPreActivate(go); } catch { }
                        }

                        // Track the launched object for cleanup
                        TrackLaunchedObject(go);

                        // Register as homing projectile if enabled
                        if (_homingEnabled)
                        {
                            rb.useGravity = false;
                            float initSpeed = _homingSpeed > 0f ? _homingSpeed : rb.velocity.magnitude;
                            if (initSpeed < 1f) initSpeed = 1f;
                            _homingProjectiles.Add(new HomingProjectile { Rb = rb, SpawnTime = Time.time, CurrentSpeed = initSpeed, StayStartTime = -1f });
                        }
                    }
                    catch (Exception e)
                    {
                        Main.MelonLog.Warning($"Failed to apply delayed force: {e.Message}");
                    }

                    _pendingForces.RemoveAt(i);
                    continue;
                }
            }
            else
            {
                // All pending forces resolved — safe to clear claimed set
                if (_claimedForceTargets.Count > 0)
                    _claimedForceTargets.Clear();
            }

            // Update homing projectiles
            if (_homingEnabled && _homingProjectiles.Count > 0)
            {
                for (int i = _homingProjectiles.Count - 1; i >= 0; i--)
                {
                    var hp = _homingProjectiles[i];
                    // Remove if null or destroyed
                    if (hp.Rb == null || hp.Rb.gameObject == null)
                    {
                        _homingProjectiles.RemoveAt(i);
                        continue;
                    }

                    float elapsed = Time.time - hp.SpawnTime;

                    // Remove if exceeded max lifetime
                    if (elapsed > _homingMaxLifetime)
                    {
                        _homingProjectiles.RemoveAt(i);
                        continue;
                    }

                    // Stop homing if duration exceeded (keep projectile flying)
                    if (_homingDuration > 0f && elapsed > _homingDuration)
                    {
                        _homingProjectiles.RemoveAt(i);
                        continue;
                    }

                    // Find target and get position based on head/torso setting
                    var target = PlayerTargeting.FindTarget(_homingFilter, hp.Rb.position);
                    if (target == null) continue;

                    Vector3? targetPos = _homingTargetHead
                        ? PlayerTargeting.GetTargetHeadPosition(target)
                        : PlayerTargeting.GetTargetPosition(target);
                    if (!targetPos.HasValue) continue;

                    Vector3 toTarget = targetPos.Value - hp.Rb.position;
                    float distToTarget = toTarget.magnitude;

                    // Stay-on-target logic
                    if (distToTarget < 0.5f)
                    {
                        if (hp.StayStartTime < 0f)
                        {
                            hp.StayStartTime = Time.time;
                            _homingProjectiles[i] = hp;
                        }
                        // Snap to target while staying
                        hp.Rb.velocity = Vector3.zero;
                        hp.Rb.position = targetPos.Value;
                        // Check if stay duration expired
                        if (Time.time - hp.StayStartTime >= _homingStayDuration)
                        {
                            hp.Rb.useGravity = true;
                            _homingProjectiles.RemoveAt(i);
                        }
                        continue;
                    }

                    if (distToTarget < 0.01f) continue;
                    Vector3 dir = toTarget / distToTarget;

                    // Determine speed
                    float speed = hp.CurrentSpeed;
                    if (_homingSpeed > 0f && !_homingAccelEnabled)
                        speed = _homingSpeed;

                    // Exponential acceleration: faster the target moves, faster it follows
                    if (_homingAccelEnabled)
                    {
                        // Use target's rig velocity as acceleration input
                        float targetSpeed = 0f;
                        try
                        {
                            var physRig = target.physicsRig;
                            if (physRig?.torso?.rbPelvis != null)
                                targetSpeed = physRig.torso.rbPelvis.velocity.magnitude;
                        }
                        catch { }

                        // Exponential: speed grows based on target speed and accel rate
                        float accelFactor = 1f + targetSpeed * _homingAccelRate * 0.1f;
                        speed *= Mathf.Pow(accelFactor, Time.deltaTime * _homingAccelRate);
                        speed = Mathf.Min(speed, 500f); // cap
                    }

                    // Update tracked speed
                    hp.CurrentSpeed = speed;
                    _homingProjectiles[i] = hp;

                    // Apply steering
                    Vector3 desired = dir * speed;
                    if (_homingMomentum)
                    {
                        // Momentum mode: apply a force-like steering so projectile arcs toward target
                        // The projectile keeps its inertia — target can dodge sideways
                        Vector3 steerForce = (desired - hp.Rb.velocity) * _homingStrength;
                        hp.Rb.velocity += steerForce * Time.deltaTime;
                    }
                    else
                    {
                        // Direct mode: lerp velocity toward target (existing behavior)
                        hp.Rb.velocity = Vector3.Lerp(hp.Rb.velocity, desired, _homingStrength * Time.deltaTime);
                    }

                    // Rotate projectile to face target
                    if (_homingRotationLock)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(dir);
                        Quaternion offset = Quaternion.Euler(rotationX, rotationY, rotationZ);
                        Quaternion targetRot = lookRot * offset;
                        hp.Rb.rotation = Quaternion.Slerp(hp.Rb.rotation, targetRot, _homingStrength * Time.deltaTime);
                    }
                }
            }
        }

        private static void UpdateTrajectoryPreview()
        {
            try
            {
                // Get active hand for spawn location and aiming direction
                var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                if (activeHand == null)
                {
                    if (trajectoryObject != null)
                        trajectoryObject.SetActive(false);
                    return;
                }

                Transform handTransform = activeHand.transform;
                if (handTransform == null)
                {
                    if (trajectoryObject != null)
                        trajectoryObject.SetActive(false);
                    return;
                }

                // Calculate spawn position and initial velocity (with world-aligned offsets)
                Vector3 forward = handTransform.forward;
                Vector3 worldRight = Vector3.Cross(Vector3.up, forward).normalized;
                if (worldRight.sqrMagnitude < 0.001f)
                    worldRight = Vector3.Cross(Vector3.up, forward + Vector3.right * 0.01f).normalized;
                Vector3 startPos = handTransform.position
                    + forward * spawnDistance
                    + worldRight * spawnOffsetX
                    + Vector3.up * spawnOffsetY;
                Vector3 initialVelocity = handTransform.forward.normalized * launchForce;
                Vector3 gravity = Physics.gravity;

                // Create trajectory line renderer if it doesn't exist
                if (trajectoryObject == null)
                {
                    trajectoryObject = new GameObject("TrajectoryPreview");
                    trajectoryLineRenderer = trajectoryObject.AddComponent<LineRenderer>();

                    trajectoryLineRenderer.useWorldSpace = true;
                    trajectoryLineRenderer.startWidth = 0.02f;
                    trajectoryLineRenderer.endWidth = 0.02f;
                    trajectoryLineRenderer.alignment = LineAlignment.View;
                    trajectoryLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    trajectoryLineRenderer.receiveShadows = false;
                    trajectoryLineRenderer.positionCount = trajectorySegments;

                    // Find a suitable shader and set white color
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                        shader = Shader.Find("SLZ/SLZ Unlit");
                    if (shader == null)
                        shader = Shader.Find("Unlit/Color");
                    if (shader == null)
                        shader = Shader.Find("Hidden/Internal-Colored");

                    if (shader != null)
                    {
                        trajectoryLineRenderer.material = new Material(shader);
                        trajectoryLineRenderer.material.color = Color.white;

                        // Handle different shader color properties
                        if (shader.name.Contains("Universal Render Pipeline"))
                        {
                            trajectoryLineRenderer.material.SetColor("_BaseColor", Color.white);
                        }
                        else if (shader.name.Contains("SLZ") && trajectoryLineRenderer.material.HasProperty("_Color"))
                        {
                            trajectoryLineRenderer.material.SetColor("_Color", Color.white);
                        }
                        trajectoryLineRenderer.material.renderQueue = 4000;
                    }
                }

                trajectoryObject.SetActive(true);

                // Calculate trajectory points using projectile motion
                // Position at time t: p(t) = startPos + initialVelocity * t + 0.5 * gravity * t^2
                Vector3[] points = new Vector3[trajectorySegments];

                for (int i = 0; i < trajectorySegments; i++)
                {
                    float t = i * trajectoryTimeStep;
                    Vector3 point = startPos + initialVelocity * t + 0.5f * gravity * t * t;

                    // Check for collision to stop trajectory early
                    if (i > 0)
                    {
                        Vector3 prevPoint = points[i - 1];
                        Vector3 direction = point - prevPoint;
                        float distance = direction.magnitude;

                        RaycastHit hit;
                        if (Physics.Raycast(prevPoint, direction.normalized, out hit, distance))
                        {
                            // Hit something, end trajectory here
                            points[i] = hit.point;

                            // Fill remaining points with the hit point
                            for (int j = i + 1; j < trajectorySegments; j++)
                            {
                                points[j] = hit.point;
                            }
                            break;
                        }
                    }

                    points[i] = point;
                }

                trajectoryLineRenderer.positionCount = trajectorySegments;
                trajectoryLineRenderer.SetPositions(points);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Trajectory preview error: {ex.Message}");
                if (trajectoryObject != null)
                    trajectoryObject.SetActive(false);
            }
        }

        public static void AddItemFromLeftHand()
        {
            try
            {
                Main.MelonLog.Msg("=== ATTEMPTING TO GET POOLEE COMPONENT ===");

                // Find the Poolee type from game assemblies
                var pooleeType = FindTypeInAssembly("Poolee", "Assembly-CSharp");
                if (pooleeType == null)
                {
                    pooleeType = FindTypeInAssembly("Poolee", "Il2CppSLZ.Marrow");
                }
                if (pooleeType == null)
                {
                    Main.MelonLog.Error("Cannot find Poolee type in any assembly!");
                    return;
                }

                Main.MelonLog.Msg($"Found Poolee type: {pooleeType.FullName}");

                // Get Player.GetComponentInHand method
                var playerType = typeof(Player);
                var getComponentInHandMethod = playerType.GetMethod("GetComponentInHand", BindingFlags.Public | BindingFlags.Static);
                if (getComponentInHandMethod == null)
                {
                    Main.MelonLog.Error("Cannot find GetComponentInHand method on Player!");
                    return;
                }

                Main.MelonLog.Msg($"Found GetComponentInHand method");

                // Make it generic with Poolee type: Player.GetComponentInHand<Poolee>(Player.LeftHand)
                var genericMethod = getComponentInHandMethod.MakeGenericMethod(pooleeType);
                Main.MelonLog.Msg($"Created generic method for Poolee");

                // Invoke the method
                object poolee = genericMethod.Invoke(null, new object[] { Player.LeftHand });
                if (poolee == null)
                {
                    Main.MelonLog.Warning("Error: No Poolee component found in left hand. Grab a spawnable item.");
                    return;
                }

                Main.MelonLog.Msg($"Got Poolee component: {poolee.GetType().FullName}");

                // Now get SpawnableCrate from the Poolee
                var spawnableCrateProp = pooleeType.GetProperty("SpawnableCrate", BindingFlags.Public | BindingFlags.Instance);
                if (spawnableCrateProp == null)
                {
                    Main.MelonLog.Error("Cannot find SpawnableCrate property on Poolee!");
                    return;
                }

                var spawnableCrate = spawnableCrateProp.GetValue(poolee);
                if (spawnableCrate == null)
                {
                    Main.MelonLog.Warning("Error: SpawnableCrate is null (not a spawnable item?).");
                    return;
                }

                Main.MelonLog.Msg($"Got SpawnableCrate: {spawnableCrate.GetType().FullName}");

                // SpawnableCrate should have Barcode property directly (it implements Scannable)
                var spawnableCrateType = spawnableCrate.GetType();
                var barcodeProp = spawnableCrateType.GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);

                if (barcodeProp == null)
                {
                    Main.MelonLog.Error("Cannot find Barcode property on SpawnableCrate!");
                    return;
                }

                var barcode = barcodeProp.GetValue(spawnableCrate);
                if (barcode == null)
                {
                    Main.MelonLog.Warning("Error: Barcode is null.");
                    return;
                }

                Main.MelonLog.Msg($"Got Barcode: {barcode.GetType().FullName}");

                // Get ID from Barcode
                var barcodeType = barcode.GetType();
                var idProp = barcodeType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                if (idProp == null)
                {
                    Main.MelonLog.Error("Cannot find ID property on Barcode!");
                    return;
                }

                string id = idProp.GetValue(barcode) as string;
                if (string.IsNullOrEmpty(id))
                {
                    Main.MelonLog.Warning("Error: Barcode ID is empty");
                    return;
                }

                Main.MelonLog.Msg($"SUCCESS! Extracted Barcode ID: {id}");

                // Get the display name from SpawnableCrate (Title property)
                string itemName = id; // Default to barcode ID
                try
                {
                    var titleProp = spawnableCrateType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                    if (titleProp != null)
                    {
                        var titleValue = titleProp.GetValue(spawnableCrate);
                        if (titleValue != null)
                        {
                            itemName = titleValue.ToString();
                            Main.MelonLog.Msg($"Got item title: {itemName}");
                        }
                    }
                    else
                    {
                        // Try "name" property as fallback
                        var nameProp = spawnableCrateType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            var nameValue = nameProp.GetValue(spawnableCrate);
                            if (nameValue != null)
                            {
                                itemName = nameValue.ToString();
                            }
                        }
                    }
                }
                catch (Exception titleEx)
                {
                    Main.MelonLog.Warning($"Could not get item title: {titleEx.Message}");
                }

                // If still no good name, extract from barcode token
                if (string.IsNullOrEmpty(itemName) || itemName == id)
                {
                    int lastDot = id.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot + 1 < id.Length)
                    {
                        itemName = id.Substring(lastDot + 1);
                        // Convert camelCase/PascalCase to spaces
                        itemName = System.Text.RegularExpressions.Regex.Replace(itemName, "([a-z])([A-Z])", "$1 $2");
                        itemName = System.Text.RegularExpressions.Regex.Replace(itemName, "([A-Z]+)([A-Z][a-z])", "$1 $2");
                    }
                }

                // Check if replacing old item
                bool isReplacing = !string.IsNullOrEmpty(currentBarcodeID) && currentBarcodeID != id;
                CurrentBarcodeID = id;
                CurrentItemName = itemName;
                Main.MelonLog.Msg($"Item selected: {itemName} ({id})");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to extract barcode from left hand: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void TryDespawnNearPosition(Vector3 spawnPos, HashSet<int> preExistingIds)
        {
            try
            {
                var allRbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                foreach (var rb in allRbs)
                {
                    if (rb == null || rb.gameObject == null) continue;
                    if (preExistingIds != null && preExistingIds.Contains(rb.gameObject.GetInstanceID())) continue;

                    float dist = Vector3.Distance(rb.transform.position, spawnPos);
                    if (dist < 3f)
                    {
                        UnityEngine.Object.Destroy(rb.gameObject);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Main.MelonLog.Warning($"Failed to despawn near position: {e.Message}");
            }
        }

        /// <summary>
        /// Returns true if the rigidbody belongs to the local player's rig (prevents
        /// accidental force/homing application to the player's own body).
        /// </summary>
        private static bool IsPlayerRigidbody(Rigidbody rb)
        {
            try
            {
                var localRig = Player.RigManager;
                if (localRig == null) return false;
                var rigTransform = ((Component)localRig).transform;
                return rb.transform.IsChildOf(rigTransform);
            }
            catch { return false; }
        }

        private static bool TryResolveForceTarget(ref PendingForce pf, out GameObject resolvedGo, out Rigidbody resolvedRb, out float resolvedDist)
        {
            resolvedGo = null;
            resolvedRb = null;
            resolvedDist = float.MaxValue;

            // Search Rigidbody array directly (much smaller than all GameObjects)
            var allRbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            foreach (var rb in allRbs)
            {
                if (rb == null || rb.gameObject == null) continue;
                if (IsPlayerRigidbody(rb)) continue;
                int id = rb.gameObject.GetInstanceID();
                if (pf.PreExistingIds != null && pf.PreExistingIds.Contains(id)) continue;
                // Skip objects already claimed by another PendingForce this frame
                if (_claimedForceTargets.Contains(id)) continue;

                float dist = Vector3.Distance(rb.transform.position, pf.SpawnPos);
                if (dist > 8f) continue;

                if (dist < resolvedDist)
                {
                    resolvedGo = rb.gameObject;
                    resolvedRb = rb;
                    resolvedDist = dist;
                }
            }

            if (resolvedGo != null)
            {
                pf.Obj = resolvedGo;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Simulates a Y/B menu tap on the spawned object.
        /// Strategy 1: Find components with OnTriggerGripUpdate and invoke them (standard Marrow).
        /// Strategy 2: Find SimpleGripEvents and invoke its menu-tap UltEvent (BaBaCorp grenades etc).
        /// Strategy 3: Find any component with Invoke/Activate/Arm/Prime methods.
        /// </summary>
        public static unsafe void TryPreActivate(GameObject go)
        {
            var hand = useLeftHand ? Player.LeftHand : Player.RightHand;
            if (hand == null) return;

            var controller = hand.Controller;
            if (controller == null) return;

            // Set menu tap flag — keep it active for multiple frames
            controller._menuTap = true;
            _preActivateController = controller;
            _preActivateFramesRemaining = PRE_ACTIVATE_FRAME_COUNT;

            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours == null) return;

            bool invokedAny = false;

            // === Strategy 1: OnTriggerGripUpdate (standard Marrow pattern) ===
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                try
                {
                    IntPtr il2cppClass = IL2CPP.il2cpp_object_get_class(mb.Pointer);
                    IntPtr methodPtr = IL2CPP.il2cpp_class_get_method_from_name(il2cppClass, "OnTriggerGripUpdate", 1);
                    if (methodPtr == IntPtr.Zero) continue;

                    Main.MelonLog.Msg($"[PreActivate] Invoking OnTriggerGripUpdate on {mb.GetIl2CppType().Name}");
                    void** args = stackalloc void*[1];
                    args[0] = (void*)hand.Pointer;
                    IntPtr exc = IntPtr.Zero;
                    IL2CPP.il2cpp_runtime_invoke(methodPtr, mb.Pointer, args, ref exc);
                    invokedAny = true;
                }
                catch { }
            }

            // === Strategy 2: SimpleGripEvents — invoke menu tap UltEvent via IL2CPP reflection ===
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                string typeName = mb.GetIl2CppType().Name;
                if (typeName != "SimpleGripEvents") continue;

                Main.MelonLog.Msg($"[PreActivate] Found SimpleGripEvents, inspecting...");
                try
                {
                    IntPtr il2cppClass = IL2CPP.il2cpp_object_get_class(mb.Pointer);

                    // Try known menu-tap method names
                    string[] methodNames = { "OnMenuTapDown", "OnMenuTap", "MenuTapDown", "OnHandMenuButtonDown" };
                    foreach (string name in methodNames)
                    {
                        IntPtr m0 = IL2CPP.il2cpp_class_get_method_from_name(il2cppClass, name, 0);
                        if (m0 != IntPtr.Zero)
                        {
                            Main.MelonLog.Msg($"[PreActivate] >> Invoking {name}() on SimpleGripEvents");
                            IntPtr exc = IntPtr.Zero;
                            IL2CPP.il2cpp_runtime_invoke(m0, mb.Pointer, null, ref exc);
                            invokedAny = true;
                            break;
                        }
                        IntPtr m1 = IL2CPP.il2cpp_class_get_method_from_name(il2cppClass, name, 1);
                        if (m1 != IntPtr.Zero)
                        {
                            Main.MelonLog.Msg($"[PreActivate] >> Invoking {name}(hand) on SimpleGripEvents");
                            void** args = stackalloc void*[1];
                            args[0] = (void*)hand.Pointer;
                            IntPtr exc = IntPtr.Zero;
                            IL2CPP.il2cpp_runtime_invoke(m1, mb.Pointer, args, ref exc);
                            invokedAny = true;
                            break;
                        }
                    }

                    // Enumerate all fields to find UltEvent fields with menu/tap in name
                    var il2cppType = mb.GetIl2CppType();
                    var il2cppFlags = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;
                    var fields = il2cppType.GetFields(il2cppFlags);
                    foreach (var field in fields)
                    {
                        string fName = field.Name;
                        string fType = field.FieldType.Name;
                        bool isUltEvent = fType.Contains("UltEvent") || fType.Contains("Action") || fType.Contains("Event");
                        bool isMenuRelated = fName.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0
                                          || fName.IndexOf("tap", StringComparison.OrdinalIgnoreCase) >= 0
                                          || fName.IndexOf("activate", StringComparison.OrdinalIgnoreCase) >= 0
                                          || fName.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isUltEvent || isMenuRelated)
                            Main.MelonLog.Msg($"  [PreActivate] ** Field: {fName} ({fType})");
                        else
                            Main.MelonLog.Msg($"  [PreActivate]    Field: {fName} ({fType})");

                        // If this is a UltEvent field with menu/tap in name, try to invoke it
                        if (isUltEvent && isMenuRelated)
                        {
                            try
                            {
                                var eventObj = field.GetValue(mb);
                                if (eventObj != null)
                                {
                                    // UltEvents have an Invoke() method
                                    var eventIl2Cpp = eventObj as Il2CppSystem.Object;
                                    if (eventIl2Cpp != null)
                                    {
                                        IntPtr eventClass = IL2CPP.il2cpp_object_get_class(eventIl2Cpp.Pointer);
                                        IntPtr invokePtr = IL2CPP.il2cpp_class_get_method_from_name(eventClass, "Invoke", 0);
                                        if (invokePtr != IntPtr.Zero)
                                        {
                                            Main.MelonLog.Msg($"  [PreActivate] >> Invoking UltEvent '{fName}'");
                                            IntPtr exc = IntPtr.Zero;
                                            IL2CPP.il2cpp_runtime_invoke(invokePtr, eventIl2Cpp.Pointer, null, ref exc);
                                            invokedAny = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Main.MelonLog.Msg($"  [PreActivate] Failed to invoke '{fName}': {ex.Message}");
                            }
                        }
                    }

                    // Also enumerate methods for diagnostics
                    Main.MelonLog.Msg($"  [PreActivate] Methods on SimpleGripEvents:");
                    var methods = il2cppType.GetMethods(il2cppFlags);
                    foreach (var method in methods)
                    {
                        string mName = method.Name;
                        if (mName.StartsWith("get_") || mName.StartsWith("set_") || mName == "GetHashCode" || mName == "Equals" || mName == "ToString") continue;
                        Main.MelonLog.Msg($"    Method: {mName}({method.GetParameters().Length} params)");
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[PreActivate] SimpleGripEvents inspection failed: {ex.Message}");
                }
            }

            if (!invokedAny)
                Main.MelonLog.Msg($"[PreActivate] No activation method found on '{go.name}' — check log for SimpleGripEvents fields");
        }

        /// <summary>
        /// Public helper for SpawnCallback scale application (called via expression tree).
        /// The callbackInfo parameter is boxed SpawnCallbackInfo.
        /// </summary>
        public static void ApplyScaleToSpawnCallback(object callbackInfo, float scale)
        {
            try
            {
                if (callbackInfo == null) return;
                var spawnedField = callbackInfo.GetType().GetField("Spawned");
                if (spawnedField == null) return;
                var go = spawnedField.GetValue(callbackInfo) as GameObject;
                if (go == null) return;

                var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                Transform st;
                Rigidbody scaleRb;
                if (rb != null)
                {
                    scaleRb = go.GetComponentInParent<Rigidbody>() ?? rb;
                    st = ((Component)scaleRb).transform;
                }
                else
                {
                    st = go.transform;
                    scaleRb = null;
                }

                Vector3 origS = st.localScale;
                Vector3 newS = origS * scale;
                st.localScale = newS;

                if (scaleRb != null)
                {
                    float origV = origS.x * origS.y * origS.z;
                    float newV = newS.x * newS.y * newS.z;
                    if (origV > 0.0001f)
                        scaleRb.mass = scaleRb.mass / origV * newV;
                }

                Main.MelonLog.Msg($"Applied scale {scale}x to network-spawned '{go.name}' via SpawnCallback");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"SpawnCallback scale failed: {ex.Message}");
            }
        }

        private static bool TryNetworkedSpawn(string barcode, Vector3 position, Quaternion rotation, float forceAmount = 0f, float scaleAmount = 1f, string tag = null)
        {
            try
            {

                // Find required types
                var networkAssetSpawnerType = FindTypeInAssembly("NetworkAssetSpawner", "LabFusion") ?? FindTypeInAssembly("NetworkAssetSpawner", "");
                var spawnableType = FindTypeInAssembly("Spawnable", "LabFusion") ?? FindTypeInAssembly("Spawnable", "");
                var spawnRequestType = FindTypeInAssembly("SpawnRequestInfo", "LabFusion") ?? FindTypeInAssembly("SpawnRequestInfo", "");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                {
                    Main.MelonLog.Warning("Required LabFusion types not found");
                    return false;
                }

                // Create Spawnable instance and set crateRef = new SpawnableCrateReference(barcode)
                var crateRefType = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("SpawnableCrateReference", "Assembly-CSharp");
                if (crateRefType == null)
                {
                    Main.MelonLog.Warning("SpawnableCrateReference type not found");
                    return false;
                }

                ConstructorInfo crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null)
                {
                    Main.MelonLog.Warning("No string ctor on SpawnableCrateReference");
                    return false;
                }

                object crateRef = crateCtor.Invoke(new object[] { barcode });

                object spawnable = Activator.CreateInstance(spawnableType);
                // set field or property crateRef
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi)
                    fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi)
                    pi.SetValue(spawnable, crateRef);

                // create SpawnRequestInfo and set fields
                object spawnReq = Activator.CreateInstance(spawnRequestType);
                var spawnableMember = spawnRequestType.GetField("Spawnable") ?? (MemberInfo)spawnRequestType.GetProperty("Spawnable");
                if (spawnableMember is FieldInfo sf)
                    sf.SetValue(spawnReq, spawnable);
                else if (spawnableMember is PropertyInfo sp)
                    sp.SetValue(spawnReq, spawnable);

                var posMember = spawnRequestType.GetField("Position") ?? (MemberInfo)spawnRequestType.GetProperty("Position");
                if (posMember is FieldInfo pf)
                    pf.SetValue(spawnReq, position);
                else if (posMember is PropertyInfo pp)
                    pp.SetValue(spawnReq, position);

                var rotMember = spawnRequestType.GetField("Rotation") ?? (MemberInfo)spawnRequestType.GetProperty("Rotation");
                if (rotMember is FieldInfo rf)
                    rf.SetValue(spawnReq, rotation);
                else if (rotMember is PropertyInfo rp)
                    rp.SetValue(spawnReq, rotation);

                // Always set SpawnCallback to register tag and apply scale
                try
                {
                    SpawnTagRegistry.TrySetSpawnCallback(spawnReq, spawnRequestType, tag, scaleAmount);
                }
                catch (Exception cbEx)
                {
                    Main.MelonLog.Warning($"Failed to set SpawnCallback: {cbEx.Message}");
                }

                // If force is specified, queue it for delayed application

                // find Spawn method and invoke
                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null)
                {
                    Main.MelonLog.Warning("NetworkAssetSpawner.Spawn method not found");
                    return false;
                }

                spawnMethod.Invoke(null, new object[] { spawnReq });
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Main.MelonLog.Warning($"LabFusion NetworkAssetSpawner spawn failed: {ex.ToString()}");
                    if (ex.InnerException != null)
                    {
                        Main.MelonLog.Warning($"Inner exception: {ex.InnerException.ToString()}");
                    }
                }
                catch
                {
                    Main.MelonLog.Warning($"LabFusion NetworkAssetSpawner spawn failed: {ex.Message}");
                }
                return false;
            }
        }

        public static void LaunchObject()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentBarcodeID))
                {
                    Main.MelonLog.Warning("No item selected. Use 'Add Item' first.");
                    return;
                }

                // Get the active hand for spawn location and aiming direction
                var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                if (activeHand == null)
                {
                    Main.MelonLog.Error($"Error: Cannot access {(useLeftHand ? "left" : "right")} hand.");
                    return;
                }

                Transform handTransform = activeHand.transform;
                if (handTransform == null)
                {
                    Main.MelonLog.Error("Error: Cannot get hand transform.");
                    return;
                }

                // Calculate base spawn position and directions (with world-aligned offsets)
                Vector3 fwd = handTransform.forward;
                Vector3 worldRight = Vector3.Cross(Vector3.up, fwd).normalized;
                if (worldRight.sqrMagnitude < 0.001f)
                    worldRight = Vector3.Cross(Vector3.up, fwd + Vector3.right * 0.01f).normalized;
                Vector3 baseSpawnPos = handTransform.position
                    + fwd * spawnDistance
                    + worldRight * spawnOffsetX
                    + Vector3.up * spawnOffsetY;
                Vector3 launchDir = fwd.normalized;
                Vector3 rightDir = worldRight;
                Vector3 upDir = Vector3.Cross(fwd, worldRight).normalized;

                // Calculate custom rotation from settings
                Quaternion customRotation = Quaternion.Euler(rotationX, rotationY, rotationZ);
                Quaternion spawnRotation = handTransform.rotation * customRotation;

                // Calculate matrix offsets for multiple projectiles
                var offsets = CalculateMatrixOffsets(projectileCount, projectileSpacing, rightDir, upDir);

                string barcodeToken = CurrentBarcodeID;
                if (!string.IsNullOrEmpty(barcodeToken))
                {
                    int lastDot = barcodeToken.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot + 1 < barcodeToken.Length)
                        barcodeToken = barcodeToken.Substring(lastDot + 1);

                    int lastSlash = barcodeToken.LastIndexOf('/');
                    if (lastSlash >= 0 && lastSlash + 1 < barcodeToken.Length)
                        barcodeToken = barcodeToken.Substring(lastSlash + 1);
                }

                // Snapshot all existing rigidbodies to avoid selecting pre-existing objects (important near other players)
                var preExistingIds = new HashSet<int>();
                foreach (var rb in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                {
                    if (rb == null || rb.gameObject == null) continue;
                    preExistingIds.Add(rb.gameObject.GetInstanceID());
                }

                // Spawn each projectile in the matrix pattern
                foreach (var offset in offsets)
                {
                    Vector3 spawnPos = baseSpawnPos + offset;
                    string tag = SpawnTagRegistry.GenerateTag("OL");

                    // Try LabFusion's NetworkAssetSpawner first (for multiplayer sync), passing force and scale
                    bool spawned = TryNetworkedSpawn(CurrentBarcodeID, spawnPos, spawnRotation, launchForce, spawnScale, tag);

                    // Queue launch force application if LabFusion spawn succeeded
                    if (spawned)
                    {
                        _pendingForces.Add(new PendingForce
                        {
                            Obj = null,
                            StartTime = Time.time,
                            Force = launchForce,
                            SpawnPos = spawnPos,
                            PreExistingIds = preExistingIds,
                            ForwardDir = launchDir,
                            BarcodeId = CurrentBarcodeID,
                            BarcodeToken = barcodeToken,
                            Scale = spawnScale,
                            Tag = tag
                        });
                    }

                    if (!spawned)
                    {
                        // Fallback to BoneLib's HelperMethods.SpawnCrate
                        Main.MelonLog.Msg("Falling back to BoneLib spawn method");
                        MethodInfo chosenSpawnMethod = null;
                        object spawnFirstArg = null; // could be string or SpawnableCrateReference
                        var helperMethodsType = FindTypeInAssembly("HelperMethods", "BoneLib");
                        if (helperMethodsType != null)
                        {
                            var candidates = helperMethodsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(m => m.Name == "SpawnCrate").ToArray();

                            foreach (var m in candidates)
                            {
                                var ps = m.GetParameters();
                                if (ps.Length >= 2)
                                {
                                    // check first param type
                                    var firstType = ps[0].ParameterType;
                                    if (firstType == typeof(string))
                                    {
                                        chosenSpawnMethod = m;
                                        spawnFirstArg = CurrentBarcodeID;
                                        break;
                                    }
                                    // handle SpawnableCrateReference
                                    if (firstType.Name.Contains("SpawnableCrateReference") || firstType.Name.Contains("SpawnableCrateRef"))
                                    {
                                        // try to construct SpawnableCrateReference from string
                                        var crateRefType = firstType;
                                        ConstructorInfo ctor = null;
                                        try { ctor = crateRefType.GetConstructor(new[] { typeof(string) }); } catch { }
                                        object crateRef = null;
                                        if (ctor != null)
                                        {
                                            try { crateRef = ctor.Invoke(new object[] { CurrentBarcodeID }); } catch { crateRef = null; }
                                        }
                                        // fallback: if constructor accepts Barcode
                                        if (crateRef == null)
                                        {
                                            var barcodeType = FindTypeInAssembly("Barcode", "Assembly-CSharp") ?? FindTypeInAssembly("Barcode", "Il2CppSLZ.Marrow");
                                            if (barcodeType != null)
                                            {
                                                var bcCtor = barcodeType.GetConstructor(new[] { typeof(string) });
                                                if (bcCtor != null)
                                                {
                                                    var bc = bcCtor.Invoke(new object[] { CurrentBarcodeID });
                                                    // try constructor that accepts Barcode
                                                    try { ctor = crateRefType.GetConstructor(new[] { barcodeType }); } catch { }
                                                    if (ctor != null)
                                                    {
                                                        try { crateRef = ctor.Invoke(new object[] { bc }); } catch { crateRef = null; }
                                                    }
                                                }
                                            }
                                        }

                                        if (crateRef != null)
                                        {
                                            chosenSpawnMethod = m;
                                            spawnFirstArg = crateRef;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (chosenSpawnMethod == null)
                        {
                            Main.MelonLog.Warning("Could not find SpawnCrate method on HelperMethods, falling back to AssetWarehouse method");
                        }
                        else
                        {
                            Main.MelonLog.Msg($"Invoking HelperMethods.SpawnCrate (method: {chosenSpawnMethod})");
                            try
                            {
                                var ps = chosenSpawnMethod.GetParameters();
                                var args = new System.Collections.Generic.List<object>();

                                for (int i = 0; i < ps.Length; ++i)
                                {
                                    var p = ps[i];
                                    var pType = p.ParameterType;

                                    if (i == 0)
                                    {
                                        args.Add(spawnFirstArg);
                                        continue;
                                    }

                                    // position
                                    if (pType == typeof(Vector3) && i == 1)
                                    {
                                        args.Add(spawnPos);
                                        continue;
                                    }

                                    if (pType == typeof(Quaternion))
                                    {
                                        args.Add(Quaternion.identity);
                                        continue;
                                    }

                                    // scale / size
                                    if (pType == typeof(Vector3))
                                    {
                                        args.Add(Vector3.one);
                                        continue;
                                    }

                                    if (pType == typeof(bool))
                                    {
                                        args.Add(true);
                                        continue;
                                    }

                                    // Action<GameObject> callbacks
                                    if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(System.Action<>))
                                    {
                                        var genericArg = pType.GetGenericArguments()[0];
                                        if (genericArg == typeof(GameObject))
                                        {
                                            System.Action<GameObject> callback = (go) =>
                                            {
                                                try
                                                {
                                                    var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                                                    if (rb != null)
                                                    {
                                                        Vector3 launchDirection = handTransform.forward.normalized;
                                                        if (rb.isKinematic)
                                                            rb.isKinematic = false;
                                                        // Apply scale if needed
                                                        if (spawnScale > 0f && Mathf.Abs(spawnScale - 1f) > 0.001f)
                                                        {
                                                            try
                                                            {
                                                                var scaleRb = go.GetComponentInParent<Rigidbody>() ?? rb;
                                                                var st = ((Component)scaleRb).transform;
                                                                Vector3 origS = st.localScale;
                                                                Vector3 newS = origS * spawnScale;
                                                                st.localScale = newS;
                                                                float origV = origS.x * origS.y * origS.z;
                                                                float newV = newS.x * newS.y * newS.z;
                                                                if (origV > 0.0001f) scaleRb.mass = scaleRb.mass / origV * newV;
                                                            }
                                                            catch { }
                                                        }
                                                        rb.velocity = launchDirection * launchForce;
                                                        Main.MelonLog.Msg($"Applied launch force {launchForce} to spawned object via callback");
                                                    }
                                                }
                                                catch { }
                                            };

                                            args.Add(callback);
                                            continue;
                                        }
                                    }

                                    // Fallback to null for any other parameter types
                                    args.Add(null);
                                }

                                chosenSpawnMethod.Invoke(null, args.ToArray());
                            }
                            catch (Exception ex)
                            {
                                Main.MelonLog.Error($"Error invoking HelperMethods.SpawnCrate: {ex.Message}\n{ex.StackTrace}");
                            }
                        }

                        // Queue force via _pendingForces (same as network path) instead of scanning scene immediately
                        _pendingForces.Add(new PendingForce
                        {
                            Obj = null,
                            StartTime = Time.time,
                            Force = launchForce,
                            SpawnPos = spawnPos,
                            PreExistingIds = preExistingIds,
                            ForwardDir = launchDir,
                            BarcodeId = CurrentBarcodeID,
                            BarcodeToken = barcodeToken,
                            Scale = spawnScale,
                            Tag = tag
                        });
                    }
                } // End foreach offset loop

                Main.MelonLog.Msg($"Object Launcher: Spawned and launched {projectileCount} projectile(s) of {CurrentBarcodeID}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to launch object: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static Type FindTypeInAssembly(string typeName, string assemblyName)
        {
            try
            {
                // First try exact assembly name match
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly != null)
                {
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;

                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null) return type;
                }

                // If not found, search all assemblies for the type
                Main.MelonLog.Warning($"Type {typeName} not found in {assemblyName}, searching all assemblies...");
                var typeFromAnyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName);

                if (typeFromAnyAssembly != null)
                {
                    Main.MelonLog.Msg($"Found {typeName} in {typeFromAnyAssembly.Assembly.GetName().Name}");
                    return typeFromAnyAssembly;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Error finding type {typeName}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Calculates offset positions for multiple projectiles in a matrix pattern.
        /// 1 = center, 2 = horizontal line, 3 = triangle, 4 = square, 5+ = grid/circle
        /// </summary>
        private static List<Vector3> CalculateMatrixOffsets(int count, float spacing, Vector3 right, Vector3 up)
        {
            var offsets = new List<Vector3>();

            if (count <= 1)
            {
                offsets.Add(Vector3.zero);
                return offsets;
            }

            if (count == 2)
            {
                // Horizontal line
                offsets.Add(-right * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f);
            }
            else if (count == 3)
            {
                // Triangle (inverted V)
                offsets.Add(up * spacing * 0.5f); // Top center
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f); // Bottom left
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f); // Bottom right
            }
            else if (count == 4)
            {
                // Square
                offsets.Add(-right * spacing * 0.5f + up * spacing * 0.5f); // Top left
                offsets.Add(right * spacing * 0.5f + up * spacing * 0.5f); // Top right
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f); // Bottom left
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f); // Bottom right
            }
            else
            {
                // Grid pattern for 5+ projectiles
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
                float halfGrid = (gridSize - 1) * spacing * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    int row = i / gridSize;
                    int col = i % gridSize;
                    float x = col * spacing - halfGrid;
                    float y = row * spacing - halfGrid;
                    offsets.Add(right * x + up * y);
                }
            }

            return offsets;
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}


