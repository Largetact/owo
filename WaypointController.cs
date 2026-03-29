using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Data;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Waypoint Controller - Save waypoints (locations), teleport to them,
    /// and optionally spawn objects at waypoint positions.
    /// Hold Y+B to teleport to nearest waypoint.
    /// Also provides Waypoint Projectile: save a spawn position, search for items, spawn at that position.
    /// </summary>
    public static class WaypointController
    {
        // Waypoint data
        public struct Waypoint
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private static List<Waypoint> _waypoints = new List<Waypoint>();
        private static int _waypointCounter = 0;
        private static float _teleportHoldTime = 1.5f; // Hold Y+B for this duration to teleport
        private static float _holdStartTime = -1f;
        private static bool _prevHoldDetected = false;
        private static bool _controllerShortcut = true;

        // Default spawn waypoint
        private static int _defaultWaypointIdx = -1;
        private static bool _hasDefaultSpawn = false;
        private static Vector3 _defaultSpawnPos = Vector3.zero;
        private static Quaternion _defaultSpawnRot = Quaternion.identity;

        // Waypoint Projectile feature
        private static string _itemSearchQuery = "";
        private static string _currentBarcode = "fa534c5a83ee4ec6bd641fec424c4142.Spawnable.PropBowlingBallBig";
        private static string _currentItemName = "Bowling Ball Big";
        private static Vector3 _savedSpawnPosition = Vector3.zero;
        private static bool _hasSavedSpawnPos = false;
        private static float _spawnHeight = 5f;
        private static float _launchForce = 0f;
        private static int _projectileCount = 1;
        private static float _projectileSpacing = 0.8f;
        private static float _spawnScale = 1f;

        // Features ported from ObjectLauncher
        private static float _spinVelocity = 0f;
        private static bool _aimRotationEnabled = false;
        private static bool _preActivateMenuTap = false;
        private static float _spawnForceDelay = 0.02f;

        // Homing system (mirrors ObjectLauncher)
        private static bool _homingEnabled = false;
        private static TargetFilter _homingFilter = TargetFilter.NEAREST;
        private static float _homingStrength = 5f;
        private static float _homingMaxLifetime = 10f;
        private static float _homingDuration = 0f;
        private static bool _homingRotationLock = false;
        private static float _homingSpeed = 0f;
        private static bool _homingAccelEnabled = false;
        private static float _homingAccelRate = 2f;
        private static bool _homingTargetHead = false;
        private static bool _homingMomentum = false;
        private static float _homingStayDuration = 2f;
        private static float _forceDelay = 0.02f;
        private static readonly float[] _retryDelays = { 0f, 0.3f, 0.6f, 1.0f, 1.5f, 2.0f, 3.0f };
        private static List<HomingProjectile> _homingProjectiles = new List<HomingProjectile>();

        private struct HomingProjectile
        {
            public Rigidbody Rb;
            public float SpawnTime;
            public float CurrentSpeed;
            public float StayStartTime;
        }

        // Delayed force application
        private struct PendingForce
        {
            public float StartTime;
            public float Force;
            public Vector3 SpawnPos;
            public HashSet<int> PreExistingIds;
            public int RetryCount;
            public float Scale;
            public string Tag;
        }
        private static List<PendingForce> _pendingForces = new List<PendingForce>();

        // Overlay-accessible read-only state
        public static int WaypointCount => _waypoints.Count;
        public static string CurrentItemName { get => _currentItemName; set => _currentItemName = value; }
        public static string CurrentBarcode { get => _currentBarcode; set => _currentBarcode = value; }
        public static bool HasSavedSpawnPosition => _hasSavedSpawnPos;

        // Properties
        public static float TeleportHoldTime
        {
            get => _teleportHoldTime;
            set => _teleportHoldTime = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static bool ControllerShortcut
        {
            get => _controllerShortcut;
            set => _controllerShortcut = value;
        }

        public static string ItemSearchQuery
        {
            get => _itemSearchQuery;
            set => _itemSearchQuery = value ?? "";
        }

        public static float SpawnHeight
        {
            get => _spawnHeight;
            set => _spawnHeight = Mathf.Clamp(value, 0f, 50f);
        }

        public static float LaunchForce
        {
            get => _launchForce;
            set => _launchForce = Mathf.Clamp(value, 0f, 1000f);
        }

        public static int ProjectileCount
        {
            get => _projectileCount;
            set => _projectileCount = Mathf.Clamp(value, 1, 25);
        }

        public static float ProjectileSpacing
        {
            get => _projectileSpacing;
            set => _projectileSpacing = Mathf.Clamp(value, 0.1f, 5f);
        }

        public static float SpawnScale
        {
            get => _spawnScale;
            set
            {
                _spawnScale = Mathf.Clamp(value, 0.1f, 10f);
                Main.MelonLog.Msg($"[WaypointProj] Spawn scale: {_spawnScale}");
            }
        }

        public static float SpinVelocity
        {
            get => _spinVelocity;
            set => _spinVelocity = Mathf.Clamp(value, 0f, 5000f);
        }

        public static bool AimRotationEnabled
        {
            get => _aimRotationEnabled;
            set => _aimRotationEnabled = value;
        }

        public static bool PreActivateMenuTap
        {
            get => _preActivateMenuTap;
            set => _preActivateMenuTap = value;
        }

        public static float SpawnForceDelay
        {
            get => _spawnForceDelay;
            set { _spawnForceDelay = Mathf.Clamp(value, 0f, 2f); _forceDelay = _spawnForceDelay; }
        }

        // Homing properties
        public static bool HomingEnabled
        {
            get => _homingEnabled;
            set { _homingEnabled = value; if (!value) _homingProjectiles.Clear(); }
        }
        public static TargetFilter HomingFilter
        {
            get => _homingFilter;
            set => _homingFilter = value;
        }
        public static float HomingStrength
        {
            get => _homingStrength;
            set => _homingStrength = Mathf.Clamp(value, 1f, 50f);
        }
        public static float HomingDuration
        {
            get => _homingDuration;
            set => _homingDuration = Mathf.Max(0f, value);
        }
        public static bool HomingRotationLock
        {
            get => _homingRotationLock;
            set => _homingRotationLock = value;
        }
        public static float HomingSpeed
        {
            get => _homingSpeed;
            set => _homingSpeed = Mathf.Max(0f, value);
        }
        public static bool HomingAccelEnabled
        {
            get => _homingAccelEnabled;
            set => _homingAccelEnabled = value;
        }
        public static float HomingAccelRate
        {
            get => _homingAccelRate;
            set => _homingAccelRate = Mathf.Clamp(value, 0.1f, 10f);
        }
        public static bool HomingTargetHead
        {
            get => _homingTargetHead;
            set => _homingTargetHead = value;
        }
        public static bool HomingMomentum
        {
            get => _homingMomentum;
            set => _homingMomentum = value;
        }
        public static float HomingStayDuration
        {
            get => _homingStayDuration;
            set => _homingStayDuration = Mathf.Max(0f, value);
        }
        public static float ForceDelay
        {
            get => _forceDelay;
            set => _forceDelay = Mathf.Clamp(value, 0f, 5f);
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Waypoint controller initialized");
        }

        // ── Default Spawn ──

        public static bool HasDefaultSpawn => _hasDefaultSpawn;

        public static void SetDefaultSpawn()
        {
            try
            {
                var head = Player.Head;
                if (head == null)
                {
                    SendNotification(NotificationType.Error, "Cannot access player position");
                    return;
                }
                _defaultSpawnPos = head.position;
                _defaultSpawnRot = head.rotation;
                _hasDefaultSpawn = true;
                SendNotification(NotificationType.Success, $"Default spawn set at ({_defaultSpawnPos.x:0.0}, {_defaultSpawnPos.y:0.0}, {_defaultSpawnPos.z:0.0})");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"SetDefaultSpawn failed: {ex.Message}");
            }
        }

        public static void ClearDefaultSpawn()
        {
            _hasDefaultSpawn = false;
            _defaultSpawnPos = Vector3.zero;
            _defaultSpawnRot = Quaternion.identity;
            SendNotification(NotificationType.Success, "Default spawn cleared");
        }

        public static void TeleportToDefaultSpawn()
        {
            if (!_hasDefaultSpawn)
            {
                SendNotification(NotificationType.Warning, "No default spawn set");
                return;
            }

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                var rmComponent = rigManager as Component;
                if (rmComponent != null)
                    rmComponent.transform.position = _defaultSpawnPos;

                try
                {
                    var teleMethod = rigManager.GetType().GetMethod("Teleport",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(Vector3), typeof(bool) },
                        null);
                    teleMethod?.Invoke(rigManager, new object[] { _defaultSpawnPos, true });
                }
                catch { }

                SendNotification(NotificationType.Success, "Teleported to default spawn");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"TeleportToDefaultSpawn failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Called on level load - auto-teleport to default spawn if set
        /// </summary>
        public static void OnLevelLoaded()
        {
            if (_hasDefaultSpawn)
            {
                // Slight delay for level to finish loading
                _pendingDefaultSpawnTeleport = true;
                _defaultSpawnTeleportTime = UnityEngine.Time.time + 2f;
            }
        }

        private static bool _pendingDefaultSpawnTeleport = false;
        private static float _defaultSpawnTeleportTime = 0f;

        // ============================================
        // WAYPOINT MANAGEMENT
        // ============================================

        /// <summary>
        /// Create a waypoint at the player's current position
        /// </summary>
        public static void CreateWaypoint()
        {
            try
            {
                var head = Player.Head;
                if (head == null)
                {
                    SendNotification(NotificationType.Error, "Cannot access player position");
                    return;
                }

                _waypointCounter++;
                var wp = new Waypoint
                {
                    Name = $"WP {_waypointCounter}",
                    Position = head.position,
                    Rotation = head.rotation
                };

                _waypoints.Add(wp);
                SendNotification(NotificationType.Success, $"Waypoint '{wp.Name}' created ({_waypoints.Count} total)");
                Main.MelonLog.Msg($"Created waypoint '{wp.Name}' at {wp.Position}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Create waypoint failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Populate a BoneMenu page with all waypoints (tappable to teleport)
        /// </summary>
        public static void PopulateWaypointPage(Page page)
        {
            if (page == null) return;

            try { page.RemoveAll(); } catch { }

            if (_waypoints.Count == 0)
            {
                page.CreateFunction("No waypoints - Create one first", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _waypoints.Count; i++)
            {
                int idx = i; // Capture for closure
                var wp = _waypoints[i];
                string displayText = $"{wp.Name} ({wp.Position.x:0.0}, {wp.Position.y:0.0}, {wp.Position.z:0.0})";

                page.CreateFunction(displayText, Color.green, () =>
                {
                    TeleportToWaypoint(idx);
                });
            }

            // Add delete buttons
            page.CreateFunction("Delete Last", Color.yellow, () =>
            {
                if (_waypoints.Count > 0)
                {
                    string name = _waypoints[_waypoints.Count - 1].Name;
                    _waypoints.RemoveAt(_waypoints.Count - 1);
                    SendNotification(NotificationType.Success, $"Deleted {name}");
                }
            });
        }

        public static void ClearAllWaypoints()
        {
            _waypoints.Clear();
            _waypointCounter = 0;
            SendNotification(NotificationType.Success, "All waypoints cleared");
            Main.MelonLog.Msg("All waypoints cleared");
        }

        /// <summary>
        /// Teleport to a specific waypoint by index
        /// </summary>
        public static void TeleportToWaypoint(int index)
        {
            if (index < 0 || index >= _waypoints.Count)
            {
                SendNotification(NotificationType.Warning, "Invalid waypoint index");
                return;
            }

            try
            {
                var wp = _waypoints[index];
                var rigManager = Player.RigManager;
                if (rigManager == null)
                {
                    SendNotification(NotificationType.Error, "Cannot find player");
                    return;
                }

                // Teleport the player
                var rmComponent = rigManager as Component;
                if (rmComponent != null)
                {
                    rmComponent.transform.position = wp.Position;
                }

                // Also try rig-based teleport
                try
                {
                    var teleMethod = rigManager.GetType().GetMethod("Teleport",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(Vector3), typeof(bool) },
                        null);
                    teleMethod?.Invoke(rigManager, new object[] { wp.Position, true });
                }
                catch { }

                SendNotification(NotificationType.Success, $"Teleported to {wp.Name}");
                Main.MelonLog.Msg($"Teleported to waypoint '{wp.Name}' at {wp.Position}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Teleport to waypoint failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport to the nearest waypoint
        /// </summary>
        private static void TeleportToNearest()
        {
            if (_waypoints.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No waypoints saved");
                return;
            }

            var head = Player.Head;
            if (head == null) return;

            Vector3 playerPos = head.position;
            int nearestIdx = 0;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                float dist = Vector3.Distance(playerPos, _waypoints[i].Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIdx = i;
                }
            }

            // Teleport to the nearest (or if only 1, the only one)
            // If standing ON a waypoint, go to the next one
            if (_waypoints.Count > 1 && nearestDist < 2f)
            {
                // Find next furthest
                float nextDist = float.MaxValue;
                int nextIdx = nearestIdx;
                for (int i = 0; i < _waypoints.Count; i++)
                {
                    if (i == nearestIdx) continue;
                    float dist = Vector3.Distance(playerPos, _waypoints[i].Position);
                    if (dist < nextDist)
                    {
                        nextDist = dist;
                        nextIdx = i;
                    }
                }
                nearestIdx = nextIdx;
            }

            TeleportToWaypoint(nearestIdx);
        }

        /// <summary>
        /// Check for Y+B hold to teleport (called from Update in main mod)
        /// Y is JoystickButton3 (left Y), B is JoystickButton1 (right B)
        /// </summary>
        public static void Update()
        {
            // Always process pending forces, even without waypoints
            // (Waypoint Projectile spawns don't require saved waypoints)
            ProcessPendingForces();
            UpdateHomingProjectiles();

            // Check pending default spawn teleport
            if (_pendingDefaultSpawnTeleport && Time.time >= _defaultSpawnTeleportTime)
            {
                _pendingDefaultSpawnTeleport = false;
                TeleportToDefaultSpawn();
            }

            if (_waypoints.Count == 0) return;

            if (!_controllerShortcut) return;

            try
            {
                bool yHeld = false;
                bool bHeld = false;

                try
                {
                    yHeld = Input.GetKey(KeyCode.JoystickButton3); // Y button (left controller)
                    bHeld = Input.GetKey(KeyCode.JoystickButton1); // B button (right controller)
                }
                catch { }

                bool holdDetected = yHeld && bHeld;

                if (holdDetected && !_prevHoldDetected)
                {
                    // Just started holding
                    _holdStartTime = Time.time;
                }
                else if (holdDetected && _holdStartTime > 0)
                {
                    // Continue holding - check duration
                    if (Time.time - _holdStartTime >= _teleportHoldTime)
                    {
                        TeleportToNearest();
                        _holdStartTime = -1f; // Reset to prevent repeated teleports
                    }
                }
                else if (!holdDetected)
                {
                    _holdStartTime = -1f;
                }

                _prevHoldDetected = holdDetected;
            }
            catch { }
        }

        // ============================================
        // WAYPOINT PROJECTILE - Spawn objects at saved positions
        // ============================================

        /// <summary>
        /// Save the current player position as the spawn position for waypoint projectiles.
        /// Height is applied at spawn time so changing SpawnHeight takes effect immediately.
        /// </summary>
        public static void SaveSpawnPosition()
        {
            try
            {
                var head = Player.Head;
                if (head == null)
                {
                    SendNotification(NotificationType.Error, "Cannot access player position");
                    return;
                }

                _savedSpawnPosition = head.position; // Store RAW position; height applied at spawn time
                _hasSavedSpawnPos = true;
                SendNotification(NotificationType.Success, $"Spawn position saved (height will be +{_spawnHeight}m)");
                Main.MelonLog.Msg($"Waypoint Projectile spawn position saved at {_savedSpawnPosition} (raw, height applied at spawn)");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Save spawn pos failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn the selected item(s) at the saved spawn position with optional force
        /// </summary>
        public static void SpawnAtSavedPosition()
        {
            if (!_hasSavedSpawnPos)
            {
                SendNotification(NotificationType.Warning, "No spawn position saved. Save one first!");
                return;
            }

            if (string.IsNullOrEmpty(_currentBarcode))
            {
                SendNotification(NotificationType.Warning, "No item selected");
                return;
            }

            try
            {
                int count = Mathf.Max(1, _projectileCount);
                float capturedForce = _launchForce;
                string capturedBarcode = _currentBarcode;
                string capturedName = _currentItemName;
                int spawnedCount = 0;

                // Apply height at spawn time so changes take effect without re-saving position
                Vector3 baseSpawnPos = _savedSpawnPosition + Vector3.up * _spawnHeight;

                for (int spawnIdx = 0; spawnIdx < count; spawnIdx++)
                {
                    // Grid offset for multiple projectiles
                    float offsetX = 0f, offsetZ = 0f;
                    if (count > 1)
                    {
                        float spacing = _projectileSpacing;
                        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
                        int row = spawnIdx / cols;
                        int col = spawnIdx % cols;
                        offsetX = (col - (cols - 1) / 2f) * spacing;
                        offsetZ = (row - (Mathf.CeilToInt((float)count / cols) - 1) / 2f) * spacing;
                    }
                    Vector3 spawnPos = baseSpawnPos + new Vector3(offsetX, 0, offsetZ);

                    // Capture pre-existing IDs for force/scale/homing application
                    HashSet<int> preExistingIds = null;
                    if (capturedForce > 0 || Mathf.Abs(_spawnScale - 1f) > 0.001f || _homingEnabled)
                    {
                        preExistingIds = new HashSet<int>();
                        try
                        {
                            foreach (var obj in UnityEngine.Object.FindObjectsOfType<GameObject>())
                            {
                                if (obj != null) preExistingIds.Add(obj.GetInstanceID());
                            }
                        }
                        catch { }
                    }

                    bool spawned = false;
                    string tag = SpawnTagRegistry.GenerateTag("WP");

                    // Check if in multiplayer server before trying network spawn
                    bool inServer = IsInMultiplayerServer();

                    if (inServer)
                    {
                        try
                        {
                            spawned = TryNetworkSpawn(capturedBarcode, spawnPos, Quaternion.identity, tag, _spawnScale);
                        }
                        catch { }
                    }

                    // Fallback to AssetSpawner (singleplayer or network failed)
                    if (!spawned)
                    {
                        try
                        {
                            var spawnable = new Spawnable
                            {
                                crateRef = new SpawnableCrateReference(capturedBarcode)
                            };
                            AssetSpawner.Register(spawnable);
                            AssetSpawner.SpawnAsync(spawnable, spawnPos, Quaternion.identity,
                                default, null, false, default, null, null, null);
                            spawned = true;
                        }
                        catch { }
                    }

                    if (spawned)
                    {
                        spawnedCount++;
                        // Queue delayed force/scale/homing if needed
                        if ((capturedForce > 0 || Mathf.Abs(_spawnScale - 1f) > 0.001f || _homingEnabled) && preExistingIds != null)
                        {
                            _pendingForces.Add(new PendingForce
                            {
                                StartTime = Time.time,
                                Force = capturedForce,
                                SpawnPos = spawnPos,
                                PreExistingIds = preExistingIds,
                                RetryCount = 0,
                                Scale = _spawnScale,
                                Tag = tag
                            });
                        }
                    }
                }

                if (spawnedCount > 0)
                {
                    SendNotification(NotificationType.Success, $"Spawned {spawnedCount}x {capturedName} at waypoint");
                    Main.MelonLog.Msg($"Waypoint Projectile: Spawned {spawnedCount}x {capturedName} at {_savedSpawnPosition}");
                }
                else
                {
                    SendNotification(NotificationType.Error, "Spawn failed");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Waypoint spawn error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process pending forces - find newly spawned objects and apply downward velocity.
        /// Uses progressive delay: tries multiple times with increasing wait for async spawns.
        /// </summary>
        private static void ProcessPendingForces()
        {
            if (_pendingForces.Count == 0) return;

            // Track which Rigidbody instance IDs have already been claimed this frame
            // so multiple PendingForce entries don't match the same object
            HashSet<int> claimedRbIds = new HashSet<int>();

            for (int i = _pendingForces.Count - 1; i >= 0; i--)
            {
                var pf = _pendingForces[i];
                float elapsed = Time.time - pf.StartTime;

                // Timeout after 5 seconds
                if (elapsed > 5f)
                {
                    Main.MelonLog.Warning("[WaypointProj] Pending force timed out");
                    SpawnTagRegistry.Remove(pf.Tag);
                    _pendingForces.RemoveAt(i);
                    continue;
                }

                // Progressive wait: use ForceDelay for first try, then progressive retries
                _retryDelays[0] = _forceDelay;
                int retryIdx = pf.RetryCount;
                if (retryIdx >= _retryDelays.Length) retryIdx = _retryDelays.Length - 1;
                if (elapsed < _retryDelays[retryIdx]) continue;

                // Try tag registry first (most reliable)
                Rigidbody foundRb = null;
                float closestDist = 0f;
                var tagged = SpawnTagRegistry.Resolve(pf.Tag);
                if (tagged != null)
                {
                    foundRb = tagged.GetComponent<Rigidbody>() ?? tagged.GetComponentInChildren<Rigidbody>();
                    if (foundRb != null)
                    {
                        claimedRbIds.Add(foundRb.gameObject.GetInstanceID());
                        SpawnTagRegistry.Remove(pf.Tag);
                    }
                }

                // Fall back to proximity search
                if (foundRb == null)
                {
                    float searchRadius = 5f + pf.RetryCount * 5f;
                    closestDist = searchRadius;

                    var allObjects = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                    foreach (var rb in allObjects)
                    {
                        if (rb == null || rb.gameObject == null) continue;
                        int rbId = rb.gameObject.GetInstanceID();
                        if (pf.PreExistingIds.Contains(rbId)) continue;
                        if (claimedRbIds.Contains(rbId)) continue;

                        float dist = Vector3.Distance(rb.transform.position, pf.SpawnPos);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            foundRb = rb;
                        }
                    }
                }

                if (foundRb == null)
                {
                    // Not found yet - increment retry and keep waiting
                    var updated = pf;
                    updated.RetryCount = pf.RetryCount + 1;
                    _pendingForces[i] = updated;
                    continue;
                }

                // Mark this RB as claimed so no other PendingForce matches it
                claimedRbIds.Add(foundRb.gameObject.GetInstanceID());

                // Freeze object during SpawnForceDelay to prevent falling before force is applied
                if (Time.time - pf.StartTime < _spawnForceDelay)
                {
                    try
                    {
                        foundRb.useGravity = false;
                        foundRb.velocity = Vector3.zero;
                        foundRb.angularVelocity = Vector3.zero;
                    }
                    catch { }
                    continue;
                }

                try
                {
                    if (foundRb.isKinematic)
                        foundRb.isKinematic = false;

                    foundRb.useGravity = true;
                    foundRb.constraints = RigidbodyConstraints.None;
                    foundRb.WakeUp();

                    // Apply scale if not 1.0
                    if (pf.Scale > 0f && Mathf.Abs(pf.Scale - 1f) > 0.001f)
                    {
                        try
                        {
                            var scaleRb = foundRb.GetComponentInParent<Rigidbody>() ?? foundRb;
                            var scaleTransform = ((Component)scaleRb).transform;
                            Vector3 originalScale = scaleTransform.localScale;
                            Vector3 newScale = originalScale * pf.Scale;
                            scaleTransform.localScale = newScale;
                            float origVol = originalScale.x * originalScale.y * originalScale.z;
                            float newVol = newScale.x * newScale.y * newScale.z;
                            if (origVol > 0.0001f) scaleRb.mass = scaleRb.mass / origVol * newVol;
                            Main.MelonLog.Msg($"[WaypointProj] Applied scale {pf.Scale}x to '{scaleRb.name}'");
                        }
                        catch (Exception scaleEx)
                        {
                            Main.MelonLog.Warning($"[WaypointProj] Scale failed: {scaleEx.Message}");
                        }
                    }

                    // Apply aim rotation (face launch direction)
                    Vector3 launchDir = Vector3.down;
                    if (_aimRotationEnabled && launchDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion aimRot = Quaternion.LookRotation(launchDir);
                        foundRb.gameObject.transform.rotation = aimRot;
                    }

                    foundRb.velocity = Vector3.zero;
                    foundRb.angularVelocity = Vector3.zero;

                    if (pf.Force > 0)
                    {
                        foundRb.velocity = Vector3.down * pf.Force;
                        foundRb.AddForce(Vector3.down * pf.Force * 0.1f, ForceMode.VelocityChange);
                    }

                    // Apply spin velocity if configured
                    if (_spinVelocity != 0f)
                    {
                        foundRb.angularVelocity = Vector3.down * _spinVelocity;
                    }

                    foundRb.WakeUp();

                    // Pre-activate: simulate Y/B menu tap on spawned object
                    if (_preActivateMenuTap)
                    {
                        try { ObjectLauncherController.TryPreActivate(foundRb.gameObject); } catch { }
                    }

                    Main.MelonLog.Msg($"[WaypointProj] Applied force {pf.Force} scale {pf.Scale}x (retry #{pf.RetryCount}, dist {closestDist:0.00}m)");

                    // Register as homing projectile if enabled
                    if (_homingEnabled)
                    {
                        foundRb.useGravity = false;
                        float initSpeed = _homingSpeed > 0f ? _homingSpeed : Mathf.Max(1f, foundRb.velocity.magnitude);
                        _homingProjectiles.Add(new HomingProjectile { Rb = foundRb, SpawnTime = Time.time, CurrentSpeed = initSpeed, StayStartTime = -1f });
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[WaypointProj] Force application failed: {ex.Message}");
                }

                _pendingForces.RemoveAt(i);
            }
        }

        private static void UpdateHomingProjectiles()
        {
            if (!_homingEnabled || _homingProjectiles.Count == 0) return;

            for (int i = _homingProjectiles.Count - 1; i >= 0; i--)
            {
                var hp = _homingProjectiles[i];
                if (hp.Rb == null || hp.Rb.gameObject == null)
                {
                    _homingProjectiles.RemoveAt(i);
                    continue;
                }

                float elapsed = Time.time - hp.SpawnTime;
                if (elapsed > _homingMaxLifetime)
                {
                    _homingProjectiles.RemoveAt(i);
                    continue;
                }
                if (_homingDuration > 0f && elapsed > _homingDuration)
                {
                    _homingProjectiles.RemoveAt(i);
                    continue;
                }

                var target = PlayerTargeting.FindTarget(_homingFilter, hp.Rb.position);
                if (target == null) continue;

                Vector3? targetPos = _homingTargetHead
                    ? PlayerTargeting.GetTargetHeadPosition(target)
                    : PlayerTargeting.GetTargetPosition(target);
                if (!targetPos.HasValue) continue;

                Vector3 toTarget = targetPos.Value - hp.Rb.position;
                float distToTarget = toTarget.magnitude;

                // Stay-on-target: when close enough, lock onto target for stay duration
                if (distToTarget < 0.5f && hp.StayStartTime < 0f)
                {
                    hp.StayStartTime = Time.time;
                    _homingProjectiles[i] = hp;
                }

                if (hp.StayStartTime > 0f)
                {
                    hp.Rb.velocity = Vector3.zero;
                    hp.Rb.position = targetPos.Value;
                    _homingProjectiles[i] = hp;

                    if (Time.time - hp.StayStartTime >= _homingStayDuration)
                    {
                        hp.Rb.useGravity = true;
                        _homingProjectiles.RemoveAt(i);
                    }
                    continue;
                }

                Vector3 dir = toTarget / Mathf.Max(distToTarget, 0.01f);

                float speed = hp.CurrentSpeed;
                if (_homingSpeed > 0f && !_homingAccelEnabled) speed = _homingSpeed;

                if (_homingAccelEnabled)
                {
                    float targetSpeed = 0f;
                    try
                    {
                        var physRig = target.physicsRig;
                        if (physRig?.torso?.rbPelvis != null)
                            targetSpeed = physRig.torso.rbPelvis.velocity.magnitude;
                    }
                    catch { }
                    float accelFactor = 1f + targetSpeed * _homingAccelRate * 0.1f;
                    speed *= Mathf.Pow(accelFactor, Time.deltaTime * _homingAccelRate);
                    speed = Mathf.Min(speed, 500f);
                }

                hp.CurrentSpeed = speed;
                _homingProjectiles[i] = hp;

                Vector3 desired = dir * speed;
                if (_homingMomentum)
                {
                    Vector3 steerForce = (desired - hp.Rb.velocity) * _homingStrength;
                    hp.Rb.velocity += steerForce * Time.deltaTime;
                }
                else
                {
                    hp.Rb.velocity = Vector3.Lerp(hp.Rb.velocity, desired, _homingStrength * Time.deltaTime);
                }

                if (_homingRotationLock)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir);
                    hp.Rb.rotation = Quaternion.Slerp(hp.Rb.rotation, lookRot, _homingStrength * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Item search to page - for selecting what to spawn at waypoint
        /// </summary>
        public static void ItemSearchToPage(Page resultsPage)
        {
            if (resultsPage == null) return;
            try { resultsPage.RemoveAll(); } catch { }

            string searchLower = (_itemSearchQuery ?? "").ToLower();
            if (string.IsNullOrEmpty(searchLower))
            {
                SendNotification(NotificationType.Warning, "Enter search term first");
                return;
            }

            int resultCount = 0;

            try
            {
                var pallets = AssetWarehouse.Instance.GetPallets();
                if (pallets == null)
                {
                    SendNotification(NotificationType.Error, "No pallets found");
                    return;
                }

                foreach (var pallet in pallets)
                {
                    if (pallet == null) continue;
                    var crates = pallet.Crates;
                    if (crates == null) continue;

                    foreach (var crate in crates)
                    {
                        if (crate == null) continue;
                        var spawnableCrate = crate.TryCast<SpawnableCrate>();
                        if (spawnableCrate == null) continue;

                        string crateName = (crate.name ?? "").ToLower();
                        string barcodeId = crate.Barcode?.ID ?? "";

                        if (crateName.Contains(searchLower) || barcodeId.ToLower().Contains(searchLower))
                        {
                            string displayName = crate.name ?? barcodeId;
                            string capturedBarcode = barcodeId;
                            string capturedName = displayName;

                            resultsPage.CreateFunction(displayName, Color.yellow, () =>
                            {
                                _currentBarcode = capturedBarcode;
                                _currentItemName = capturedName;
                                SendNotification(NotificationType.Success, $"Set: {capturedName}");
                                Main.MelonLog.Msg($"Waypoint item set: {capturedName} ({capturedBarcode})");
                            });
                            resultCount++;
                            if (resultCount >= 50) break;
                        }
                    }
                    if (resultCount >= 50) break;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Waypoint item search error: {ex.Message}");
            }

            if (resultCount > 0)
            {
                SendNotification(NotificationType.Success, $"{resultCount} items found");
            }
            else
            {
                SendNotification(NotificationType.Warning, "No items found");
            }
        }

        /// <summary>
        /// Try to spawn via LabFusion NetworkAssetSpawner
        /// </summary>
        private static bool TryNetworkSpawn(string barcode, Vector3 position, Quaternion rotation, string tag = null, float scale = 1f)
        {
            try
            {
                var networkAssetSpawnerType = FindTypeByName("NetworkAssetSpawner");
                if (networkAssetSpawnerType == null) return false;

                var spawnRequestInfoType = FindTypeByName("SpawnRequestInfo");
                if (spawnRequestInfoType == null) return false;

                var spawnRequest = Activator.CreateInstance(spawnRequestInfoType);

                var spawnable = new Spawnable
                {
                    crateRef = new SpawnableCrateReference(barcode)
                };

                var spawnableField = spawnRequestInfoType.GetField("Spawnable");
                spawnableField?.SetValue(spawnRequest, spawnable);

                var positionField = spawnRequestInfoType.GetField("Position");
                positionField?.SetValue(spawnRequest, position);

                var rotationField = spawnRequestInfoType.GetField("Rotation");
                rotationField?.SetValue(spawnRequest, rotation);

                // Set SpawnCallback to register tag and apply scale
                try
                {
                    SpawnTagRegistry.TrySetSpawnCallback(spawnRequest, spawnRequestInfoType, tag, scale);
                }
                catch { }

                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn",
                    BindingFlags.Public | BindingFlags.Static);
                spawnMethod?.Invoke(null, new object[] { spawnRequest });

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if we are currently in a multiplayer server.
        /// Returns false if Fusion DLL is not present or no active server.
        /// </summary>
        private static bool IsInMultiplayerServer()
        {
            try
            {
                var networkInfoType = FindTypeByName("NetworkInfo");
                if (networkInfoType == null) return false;

                var hasServerProp = networkInfoType.GetProperty("HasServer",
                    BindingFlags.Public | BindingFlags.Static);
                if (hasServerProp == null) return false;

                return (bool)hasServerProp.GetValue(null);
            }
            catch
            {
                return false;
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
                        if (t.Name == typeName) return t;
                    }
                }
                catch { }
            }
            return null;
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}
