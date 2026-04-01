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
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Spawn objects on/above/below other players with Fusion sync
    /// Uses search system like Teleport to find players
    /// Height 0 = torso (chest), positive = above, negative = below
    /// </summary>
    public static class PlayerSpawnController
    {
        // Settings
        private static string _currentBarcode = "fa534c5a83ee4ec6bd641fec424c4142.Spawnable.PropBowlingBallBig";
        private static string _currentItemName = "Bowling Ball Big";
        private static float _heightAbovePlayer = 3f;
        private static float _launchForce = 0f; // 0 = no force, just free fall
        private static bool _launchDown = true; // Launch downward toward player
        private static int _projectileCount = 1; // Number of projectiles to spawn
        private static float _projectileSpacing = 0.8f; // Spacing between projectiles in grid
        private static float _spawnScale = 1f; // Scale multiplier for spawned objects

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
            public float StayStartTime; // -1 = not yet on target
        }

        // Search
        private static string _playerSearchQuery = "";
        private static string _itemSearchQuery = "";
        private static Page _playerResultsPage = null;
        private static Page _itemResultsPage = null;

        // Cached players
        private static List<PlayerInfo> _cachedPlayers = new List<PlayerInfo>();

        // Delayed force application (like ObjectLauncher)
        private struct PendingForce
        {
            public float StartTime;
            public float Force;
            public Vector3 SpawnPos;
            public HashSet<int> PreExistingIds;
            public string BarcodeId;
            public int RetryCount;
            public float Scale;
            public string Tag;
        }

        private static List<PendingForce> _pendingForces = new List<PendingForce>();

        public struct PlayerInfo
        {
            public byte SmallID;
            public string DisplayName;
            public Vector3 Position;
        }

        // Properties
        public static string CurrentBarcode
        {
            get => _currentBarcode;
            set => _currentBarcode = value ?? "";
        }

        public static string CurrentItemName
        {
            get => _currentItemName;
            set => _currentItemName = value ?? "";
        }

        public static float HeightAbovePlayer
        {
            get => _heightAbovePlayer;
            set => _heightAbovePlayer = Mathf.Clamp(value, -50f, 50f);
        }

        public static float LaunchForce
        {
            get => _launchForce;
            set => _launchForce = Mathf.Clamp(value, 0f, 1000f);
        }

        public static bool LaunchDown
        {
            get => _launchDown;
            set => _launchDown = value;
        }

        public static string PlayerSearchQuery
        {
            get => _playerSearchQuery;
            set => _playerSearchQuery = value ?? "";
        }

        public static string ItemSearchQuery
        {
            get => _itemSearchQuery;
            set => _itemSearchQuery = value ?? "";
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
                float clamped = Mathf.Clamp(value, 0.1f, 10f);
                if (_spawnScale == clamped) return;
                _spawnScale = clamped;
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
            Main.MelonLog.Msg("Player Spawn controller initialized");
        }

        /// <summary>
        /// Call this from main Update loop to apply delayed forces
        /// </summary>
        public static void Update()
        {
            ProcessPendingForces();
            UpdateHomingProjectiles();
        }

        /// <summary>
        /// Process pending forces - find spawned objects and apply downward velocity.
        /// Uses progressive delay with retries for async spawn reliability.
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
                    Main.MelonLog.Warning($"[PlayerSpawn] Pending force timed out for {pf.BarcodeId}");
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

                    var allRbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                    foreach (var rb in allRbs)
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

                // Apply force and scale
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
                            Main.MelonLog.Msg($"[PlayerSpawn] Applied scale {pf.Scale}x to '{scaleRb.name}'");
                        }
                        catch (Exception scaleEx)
                        {
                            Main.MelonLog.Warning($"[PlayerSpawn] Scale failed: {scaleEx.Message}");
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

                    Main.MelonLog.Msg($"[PlayerSpawn] Applied force {pf.Force} scale {pf.Scale}x (retry #{pf.RetryCount}, dist {closestDist:0.00}m)");

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
                    Main.MelonLog.Warning($"[PlayerSpawn] Failed to apply force: {ex.Message}");
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
                    // Staying on target — snap to target position
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
        /// Search for players and populate results page
        /// Click a player to spawn the object above them
        /// </summary>
        public static void PlayerSearchToPage(Page resultsPage)
        {
            _playerResultsPage = resultsPage;

            // Refresh player list
            RefreshPlayerList();

            // Clear page
            try { _playerResultsPage?.RemoveAll(); } catch { }

            if (_cachedPlayers.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No other players found");
                return;
            }

            string searchLower = (_playerSearchQuery ?? "").ToLower();
            int resultCount = 0;

            foreach (var player in _cachedPlayers)
            {
                // Filter by search query
                if (!string.IsNullOrEmpty(searchLower))
                {
                    if (!player.DisplayName.ToLower().Contains(searchLower))
                        continue;
                }

                // Capture for closure
                byte capturedSmallId = player.SmallID;
                string capturedName = player.DisplayName;

                _playerResultsPage?.CreateFunction(capturedName, Color.red, () =>
                {
                    SpawnAbovePlayer(capturedSmallId, capturedName);
                });
                resultCount++;
            }

            Main.MelonLog.Msg($"[Player Spawn Search] Found {resultCount} player(s)");
            SendNotification(NotificationType.Success, $"{resultCount} player(s) found");
        }

        /// <summary>
        /// Search for items to set as spawn object (uses SpawnableSearcher style)
        /// </summary>
        public static void ItemSearchToPage(Page resultsPage)
        {
            _itemResultsPage = resultsPage;

            // Clear page
            try { _itemResultsPage?.RemoveAll(); } catch { }

            string searchLower = (_itemSearchQuery ?? "").ToLower();
            if (string.IsNullOrEmpty(searchLower))
            {
                SendNotification(NotificationType.Warning, "Enter a search term first");
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

                        // Check if spawnable
                        var spawnableCrate = crate.TryCast<SpawnableCrate>();
                        if (spawnableCrate == null) continue;

                        string crateName = (crate.name ?? "").ToLower();
                        string barcodeId = crate.Barcode?.ID ?? "";

                        if (crateName.Contains(searchLower) || barcodeId.ToLower().Contains(searchLower))
                        {
                            string displayName = crate.name ?? barcodeId;
                            string capturedBarcode = barcodeId;
                            string capturedName = displayName;

                            _itemResultsPage?.CreateFunction(displayName, Color.yellow, () =>
                            {
                                _currentBarcode = capturedBarcode;
                                _currentItemName = capturedName;
                                SendNotification(NotificationType.Success, $"Set: {capturedName}");
                                Main.MelonLog.Msg($"Set spawn item: {capturedName} ({capturedBarcode})");
                            });
                            resultCount++;

                            if (resultCount >= 50) break; // Limit results
                        }
                    }

                    if (resultCount >= 50) break;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Item search error: {ex.Message}");
            }

            Main.MelonLog.Msg($"[Item Search] Found {resultCount} items");
            SendNotification(NotificationType.Success, $"{resultCount} items found");
        }

        /// <summary>
        /// Public wrapper to spawn on a player by SmallID (used by QuickMenu).
        /// </summary>
        public static void SpawnOnPlayerBySmallId(byte smallId, string playerName)
        {
            SpawnAbovePlayer(smallId, playerName);
        }

        /// <summary>
        /// Spawn an object above the specified player with Fusion sync
        /// Uses NetworkAssetSpawner for multiplayer visibility, falls back to BoneLib
        /// </summary>
        private static void SpawnAbovePlayer(byte smallId, string playerName)
        {
            try
            {
                // Get player position
                Vector3 playerPos = GetPlayerPosition(smallId);
                if (playerPos == Vector3.zero)
                {
                    SendNotification(NotificationType.Error, $"Cannot find {playerName}");
                    return;
                }

                // Calculate spawn position above player
                Vector3 baseSpawnPos = playerPos + Vector3.up * _heightAbovePlayer;
                float capturedForce = _launchForce;
                string capturedBarcode = _currentBarcode;
                string capturedItemName = _currentItemName;
                int count = Mathf.Max(1, _projectileCount);

                Main.MelonLog.Msg($"[PlayerSpawn] Spawning {count}x {capturedItemName} at {baseSpawnPos} above {playerName} (force: {capturedForce})");

                for (int spawnIdx = 0; spawnIdx < count; spawnIdx++)
                {
                    // Offset each projectile in a grid pattern
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

                    // Capture pre-existing object IDs for delayed force application
                    HashSet<int> preExistingIds = new HashSet<int>();
                    try
                    {
                        foreach (var obj in UnityEngine.Object.FindObjectsOfType<GameObject>())
                        {
                            if (obj != null)
                                preExistingIds.Add(obj.GetInstanceID());
                        }
                    }
                    catch { }

                    bool spawned = false;
                    string tag = SpawnTagRegistry.GenerateTag("PS");

                    // Try NetworkAssetSpawner first for Fusion sync (other players can see it)
                    try
                    {
                        spawned = SpawnWithNetworkAssetSpawner(capturedBarcode, spawnPos, Quaternion.identity, tag, _spawnScale);
                        if (spawned)
                        {
                            Main.MelonLog.Msg("[PlayerSpawn] NetworkAssetSpawner spawn succeeded (Fusion synced)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"[PlayerSpawn] NetworkAssetSpawner failed: {ex.Message}");
                    }

                    // Fallback to BoneLib HelperMethods.SpawnCrate
                    if (!spawned)
                    {
                        spawned = SpawnWithBoneLib(capturedBarcode, spawnPos);
                        if (spawned)
                        {
                            Main.MelonLog.Msg("[PlayerSpawn] BoneLib spawn succeeded (local only)");
                        }
                    }

                    // Final fallback to AssetSpawner
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
                            Main.MelonLog.Msg("[PlayerSpawn] Used AssetSpawner fallback");
                        }
                        catch (Exception fallbackEx)
                        {
                            Main.MelonLog.Error($"[PlayerSpawn] Fallback spawn failed: {fallbackEx.Message}");
                        }
                    }

                    if (spawned)
                    {
                        // Queue delayed force application if force > 0, scale != 1, or homing enabled
                        if (capturedForce > 0 || Mathf.Abs(_spawnScale - 1f) > 0.001f || _homingEnabled)
                        {
                            _pendingForces.Add(new PendingForce
                            {
                                StartTime = Time.time,
                                Force = capturedForce,
                                SpawnPos = spawnPos,
                                PreExistingIds = preExistingIds,
                                BarcodeId = capturedBarcode,
                                RetryCount = 0,
                                Scale = _spawnScale,
                                Tag = tag
                            });
                            Main.MelonLog.Msg($"[PlayerSpawn] Queued delayed force ({capturedForce}) scale ({_spawnScale})");
                        }
                    }
                } // end of projectile count for loop

                Main.MelonLog.Msg($"Spawned {count}x {capturedItemName} above {playerName} at height {_heightAbovePlayer}m");
                SendNotification(NotificationType.Success, $"Dropped {count}x {capturedItemName} on {playerName}!");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Spawn above player failed: {ex.Message}\n{ex.StackTrace}");
                SendNotification(NotificationType.Error, "Spawn failed");
            }
        }

        /// <summary>
        /// Spawn using LabFusion NetworkAssetSpawner for multiplayer sync
        /// </summary>
        private static bool SpawnWithNetworkAssetSpawner(string barcode, Vector3 position, Quaternion rotation, string tag = null, float scale = 1f)
        {
            try
            {
                Main.MelonLog.Msg("[PlayerSpawn] Attempting NetworkAssetSpawner spawn...");

                // Find required types
                var networkAssetSpawnerType = FindTypeInAssembly("NetworkAssetSpawner", "LabFusion") ?? FindTypeInAssembly("NetworkAssetSpawner", "");
                var spawnableType = FindTypeInAssembly("Spawnable", "LabFusion") ?? FindTypeInAssembly("Spawnable", "");
                var spawnRequestType = FindTypeInAssembly("SpawnRequestInfo", "LabFusion") ?? FindTypeInAssembly("SpawnRequestInfo", "");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] Required LabFusion types not found");
                    return false;
                }

                // Create SpawnableCrateReference
                var crateRefType = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("SpawnableCrateReference", "Assembly-CSharp");
                if (crateRefType == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] SpawnableCrateReference type not found");
                    return false;
                }

                ConstructorInfo crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] No string ctor on SpawnableCrateReference");
                    return false;
                }

                object crateRef = crateCtor.Invoke(new object[] { barcode });

                // Create Spawnable instance
                object spawnable = Activator.CreateInstance(spawnableType);
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi)
                    fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi)
                    pi.SetValue(spawnable, crateRef);

                // Create SpawnRequestInfo and set fields
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

                // Set SpawnCallback to register tag and apply scale
                try
                {
                    SpawnTagRegistry.TrySetSpawnCallback(spawnReq, spawnRequestType, tag, scale);
                }
                catch { }

                // Find Spawn method and invoke
                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] NetworkAssetSpawner.Spawn method not found");
                    return false;
                }

                spawnMethod.Invoke(null, new object[] { spawnReq });
                Main.MelonLog.Msg("[PlayerSpawn] Invoked NetworkAssetSpawner.Spawn");
                return true;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[PlayerSpawn] NetworkAssetSpawner spawn failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Spawn using BoneLib HelperMethods.SpawnCrate (local spawn)
        /// </summary>
        private static bool SpawnWithBoneLib(string barcode, Vector3 spawnPos)
        {
            try
            {
                var helperMethodsType = FindTypeInAssembly("HelperMethods", "BoneLib");
                if (helperMethodsType == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] BoneLib HelperMethods not found");
                    return false;
                }

                var candidates = helperMethodsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "SpawnCrate").ToArray();

                MethodInfo chosenSpawnMethod = null;
                object spawnFirstArg = null;

                foreach (var m in candidates)
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 2)
                    {
                        var firstType = ps[0].ParameterType;

                        if (firstType == typeof(string))
                        {
                            chosenSpawnMethod = m;
                            spawnFirstArg = barcode;
                            break;
                        }

                        if (firstType.Name.Contains("SpawnableCrateReference") || firstType.Name.Contains("SpawnableCrateRef"))
                        {
                            var crateRefType = firstType;
                            ConstructorInfo ctor = null;
                            try { ctor = crateRefType.GetConstructor(new[] { typeof(string) }); } catch { }
                            object crateRef = null;
                            if (ctor != null)
                            {
                                try { crateRef = ctor.Invoke(new object[] { barcode }); } catch { crateRef = null; }
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

                if (chosenSpawnMethod == null)
                {
                    Main.MelonLog.Warning("[PlayerSpawn] Could not find SpawnCrate method");
                    return false;
                }

                var ps2 = chosenSpawnMethod.GetParameters();
                var args = new List<object>();

                for (int i = 0; i < ps2.Length; ++i)
                {
                    var p = ps2[i];
                    var pType = p.ParameterType;

                    if (i == 0)
                    {
                        args.Add(spawnFirstArg);
                        continue;
                    }

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

                    // Action<GameObject> - we handle force via delayed queue, so just pass null
                    args.Add(null);
                }

                chosenSpawnMethod.Invoke(null, args.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[PlayerSpawn] BoneLib spawn error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find a type in a specific assembly or search all assemblies
        /// </summary>
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

                // Search all assemblies for the type
                var typeFromAnyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName);

                if (typeFromAnyAssembly != null)
                {
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
        /// Apply launch force to spawned object
        /// </summary>
        private static void ApplyLaunchForce(GameObject go, Vector3 targetPos)
        {
            if (go == null) return;

            try
            {
                // Find Rigidbody
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = go.GetComponentInChildren<Rigidbody>();
                }

                if (rb != null)
                {
                    Vector3 direction = _launchDown ? Vector3.down : (targetPos - go.transform.position).normalized;
                    rb.AddForce(direction * _launchForce, ForceMode.Impulse);
                    Main.MelonLog.Msg($"Applied launch force: {_launchForce}");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Could not apply force: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the world position of a player by SmallID
        /// </summary>
        private static Vector3 GetPlayerPosition(byte smallId)
        {
            try
            {
                // Check if this is the local player
                var playerIdManagerType = FindTypeByName("PlayerIDManager");
                if (playerIdManagerType != null)
                {
                    var localSmallIdProp = playerIdManagerType.GetProperty("LocalSmallID", BindingFlags.Public | BindingFlags.Static);
                    if (localSmallIdProp != null)
                    {
                        byte localSmallId = (byte)localSmallIdProp.GetValue(null);
                        if (smallId == localSmallId)
                        {
                            // Use BoneLib for local player position
                            var head = Player.Head;
                            if (head != null) return head.position;
                        }
                    }
                }

                var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager");
                if (networkPlayerManagerType == null) return Vector3.zero;

                var tryGetPlayerMethod = networkPlayerManagerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "TryGetPlayer" && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(byte));

                if (tryGetPlayerMethod == null) return Vector3.zero;

                var args = new object[] { smallId, null };
                bool found = (bool)tryGetPlayerMethod.Invoke(null, args);

                if (!found || args[1] == null) return Vector3.zero;

                var networkPlayer = args[1];
                var networkPlayerType = networkPlayer.GetType();

                // Check HasRig
                var hasRigProp = networkPlayerType.GetProperty("HasRig", BindingFlags.Public | BindingFlags.Instance);
                if (hasRigProp != null && !(bool)hasRigProp.GetValue(networkPlayer))
                {
                    return Vector3.zero;
                }

                // Get RigRefs
                var rigRefsProp = networkPlayerType.GetProperty("RigRefs", BindingFlags.Public | BindingFlags.Instance);
                if (rigRefsProp == null) return Vector3.zero;

                var rigRefs = rigRefsProp.GetValue(networkPlayer);
                if (rigRefs == null) return Vector3.zero;

                // Try Chest/Spine transform first (torso = origin point for height 0)
                var chestProp = rigRefs.GetType().GetProperty("Chest", BindingFlags.Public | BindingFlags.Instance)
                    ?? rigRefs.GetType().GetProperty("Spine", BindingFlags.Public | BindingFlags.Instance);
                if (chestProp != null)
                {
                    var chestTransform = chestProp.GetValue(rigRefs) as Transform;
                    if (chestTransform != null)
                    {
                        return chestTransform.position;
                    }
                }

                // Try Head transform as fallback
                var headProp = rigRefs.GetType().GetProperty("Head", BindingFlags.Public | BindingFlags.Instance);
                if (headProp != null)
                {
                    var headTransform = headProp.GetValue(rigRefs) as Transform;
                    if (headTransform != null)
                    {
                        return headTransform.position;
                    }
                }

                // Fallback to RigManager transform
                var rigManagerProp = rigRefs.GetType().GetProperty("RigManager", BindingFlags.Public | BindingFlags.Instance);
                if (rigManagerProp != null)
                {
                    var rigManager = rigManagerProp.GetValue(rigRefs);
                    if (rigManager != null)
                    {
                        var transform = (rigManager as Component)?.transform;
                        if (transform != null)
                        {
                            return transform.position;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"GetPlayerPosition error: {ex.Message}");
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Refresh the cached player list
        /// </summary>
        private static void RefreshPlayerList()
        {
            _cachedPlayers.Clear();

            try
            {
                var playerIdManagerType = FindTypeByName("PlayerIDManager");
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

                    bool isLocal = (smallId == localSmallId);
                    string displayName = GetPlayerDisplayName(smallId);
                    if (isLocal) displayName = displayName + " (You)";

                    _cachedPlayers.Add(new PlayerInfo
                    {
                        SmallID = smallId,
                        DisplayName = displayName,
                        Position = Vector3.zero // Will be fetched when needed
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"RefreshPlayerList error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get display name for a player
        /// </summary>
        private static string GetPlayerDisplayName(byte smallId)
        {
            string displayName = $"Player {smallId}";

            try
            {
                var networkPlayerManagerType = FindTypeByName("NetworkPlayerManager");
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
            catch
            {
                return null;
            }
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}
