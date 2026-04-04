using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Entities;
using LabFusion.Marrow.Extenders;
using LabFusion.Network;
using LabFusion.RPC;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Manages three blocking features:
    /// 1. Player Block — despawn all items spawned by specific players
    /// 2. Item Block (server-side) — auto-despawn specific items by barcode
    /// 3. Local Block (client-side) — hide specific items locally without network despawn
    /// Also handles despawning the held/last-held item via keybind.
    /// </summary>
    public static class BlockController
    {
        // ── Player Block ──────────────────────────────────────────
        public struct BlockedPlayer
        {
            public byte SmallID;
            public string DisplayName;
        }
        private static List<BlockedPlayer> _blockedPlayers = new List<BlockedPlayer>();
        private static bool _playerBlockEnabled = false;

        // ── Item Block (server-side despawn) ──────────────────────
        public struct BlockedItem
        {
            public string Barcode;
            public string DisplayName;
        }
        private static List<BlockedItem> _blockedItems = new List<BlockedItem>();
        private static bool _itemBlockEnabled = false;

        // ── Local Block (client-side hide) ────────────────────────
        private static List<BlockedItem> _localBlockedItems = new List<BlockedItem>();
        private static bool _localBlockEnabled = false;

        // ── Held item tracking for despawn-held keybind ───────────
        private static GameObject _lastHeldObject = null;

        // ── Scan interval (barcode scans are heavier, throttle them) ──
        private static float _nextBarcodeScanTime = 0f;
        private const float BARCODE_SCAN_INTERVAL = 0.15f;

        // ── Public Properties ─────────────────────────────────────
        public static bool PlayerBlockEnabled
        {
            get => _playerBlockEnabled;
            set
            {
                if (_playerBlockEnabled == value) return;
                _playerBlockEnabled = value;
                Main.MelonLog.Msg($"Player Block {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static bool ItemBlockEnabled
        {
            get => _itemBlockEnabled;
            set
            {
                if (_itemBlockEnabled == value) return;
                _itemBlockEnabled = value;
                Main.MelonLog.Msg($"Item Block {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static bool LocalBlockEnabled
        {
            get => _localBlockEnabled;
            set
            {
                if (_localBlockEnabled == value) return;
                _localBlockEnabled = value;
                Main.MelonLog.Msg($"Local Block {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public static IReadOnlyList<BlockedPlayer> BlockedPlayers => _blockedPlayers;
        public static IReadOnlyList<BlockedItem> BlockedItems => _blockedItems;
        public static IReadOnlyList<BlockedItem> LocalBlockedItems => _localBlockedItems;

        // ── Initialization ────────────────────────────────────────
        public static void Initialize()
        {
            _blockedPlayers.Clear();
            _blockedItems.Clear();
            _localBlockedItems.Clear();
            Main.MelonLog.Msg("BlockController initialized");
        }

        // ── Update (called every frame from OnUpdate) ─────────────
        public static void Update()
        {
            TrackHeldItems();

            if (!NetworkInfo.HasServer) return;

            // Player block runs every frame for instant despawn
            if (_playerBlockEnabled && _blockedPlayers.Count > 0)
                ScanAndDespawnByPlayer();

            // Barcode-based scans are heavier, throttle to interval
            if (Time.time < _nextBarcodeScanTime) return;
            _nextBarcodeScanTime = Time.time + BARCODE_SCAN_INTERVAL;

            if (_itemBlockEnabled && _blockedItems.Count > 0)
                ScanAndDespawnByBarcode();

            if (_localBlockEnabled && _localBlockedItems.Count > 0)
                ScanAndHideLocally();
        }

        // ══════════════════════════════════════════════════════════
        //  HELD ITEM TRACKING + DESPAWN
        // ══════════════════════════════════════════════════════════

        private static void TrackHeldItems()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;
                var physRig = rigManager.physicsRig;
                if (physRig == null) return;

                GameObject currentHeld = null;

                if (physRig.rightHand != null && physRig.rightHand.m_CurrentAttachedGO != null)
                    currentHeld = physRig.rightHand.m_CurrentAttachedGO.transform.root.gameObject;
                else if (physRig.leftHand != null && physRig.leftHand.m_CurrentAttachedGO != null)
                    currentHeld = physRig.leftHand.m_CurrentAttachedGO.transform.root.gameObject;

                if (currentHeld != null)
                    _lastHeldObject = currentHeld;
            }
            catch { }
        }

        /// <summary>
        /// Despawn the item currently held in hand, or the last-held item.
        /// </summary>
        public static void DespawnHeldItem()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;
                var physRig = rigManager.physicsRig;
                if (physRig == null) return;

                // Try currently held first
                GameObject target = null;

                if (physRig.rightHand != null && physRig.rightHand.m_CurrentAttachedGO != null)
                    target = physRig.rightHand.m_CurrentAttachedGO.transform.root.gameObject;
                else if (physRig.leftHand != null && physRig.leftHand.m_CurrentAttachedGO != null)
                    target = physRig.leftHand.m_CurrentAttachedGO.transform.root.gameObject;

                // Fallback to last held
                if (target == null)
                    target = _lastHeldObject;

                if (target == null)
                {
                    NotificationHelper.Send(NotificationType.Warning, "No held/recent item to despawn");
                    return;
                }

                // Try network despawn first
                if (NetworkInfo.HasServer && DespawnAllController.TryDespawnObject(target))
                {
                    Main.MelonLog.Msg($"Despawned held item (network): {target.name}");
                    NotificationHelper.Send(NotificationType.Success, $"Despawned: {target.name}");
                }
                else
                {
                    // Local despawn via Poolee
                    var poolee = target.GetComponentInChildren<Poolee>();
                    if (poolee != null)
                    {
                        poolee.Despawn();
                        Main.MelonLog.Msg($"Despawned held item (local): {target.name}");
                        NotificationHelper.Send(NotificationType.Success, $"Despawned: {target.name}");
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(target);
                        Main.MelonLog.Msg($"Destroyed held item: {target.name}");
                        NotificationHelper.Send(NotificationType.Success, $"Destroyed: {target.name}");
                    }
                }

                if (_lastHeldObject == target)
                    _lastHeldObject = null;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"DespawnHeldItem error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER BLOCK — Despawn items owned by blocked players
        // ══════════════════════════════════════════════════════════

        public static void AddBlockedPlayer(byte smallId, string displayName)
        {
            // Check if already blocked
            for (int i = 0; i < _blockedPlayers.Count; i++)
            {
                if (_blockedPlayers[i].SmallID == smallId) return;
            }
            _blockedPlayers.Add(new BlockedPlayer { SmallID = smallId, DisplayName = displayName });
            Main.MelonLog.Msg($"Blocked player: {displayName} (ID: {smallId})");
            NotificationHelper.Send(NotificationType.Success, $"Blocked: {displayName}");
        }

        public static void RemoveBlockedPlayer(byte smallId)
        {
            for (int i = _blockedPlayers.Count - 1; i >= 0; i--)
            {
                if (_blockedPlayers[i].SmallID == smallId)
                {
                    string name = _blockedPlayers[i].DisplayName;
                    _blockedPlayers.RemoveAt(i);
                    Main.MelonLog.Msg($"Unblocked player: {name}");
                    NotificationHelper.Send(NotificationType.Success, $"Unblocked: {name}");
                    return;
                }
            }
        }

        private static void ScanAndDespawnByPlayer()
        {
            try
            {
                var blockedIds = new HashSet<byte>();
                foreach (var bp in _blockedPlayers)
                    blockedIds.Add(bp.SmallID);

                var idManager = NetworkEntityManager.IDManager;
                if (idManager?.RegisteredEntities?.EntityIDLookup == null) return;

                // Snapshot keys — Despawn on host triggers inline entity unregister
                // which modifies EntityIDLookup, breaking foreach iteration
                var entities = idManager.RegisteredEntities.EntityIDLookup.Keys.ToList();

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null) continue;
                        if (entity.GetExtender<NetworkPlayer>() != null) continue;

                        var ownerId = entity.OwnerID;
                        if (ownerId == null) continue;

                        byte ownerSmallId = ownerId.SmallID;
                        if (blockedIds.Contains(ownerSmallId))
                        {
                            // Network despawn — server broadcasts to ALL clients
                            NetworkAssetSpawner.Despawn(new NetworkAssetSpawner.DespawnRequestInfo
                            {
                                EntityID = entity.ID,
                                DespawnEffect = false
                            });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"ScanAndDespawnByPlayer error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITEM BLOCK — Auto-despawn specific barcodes (server-side)
        // ══════════════════════════════════════════════════════════

        public static void AddBlockedItem(string barcode, string displayName)
        {
            for (int i = 0; i < _blockedItems.Count; i++)
            {
                if (_blockedItems[i].Barcode == barcode) return;
            }
            _blockedItems.Add(new BlockedItem { Barcode = barcode, DisplayName = displayName });
            Main.MelonLog.Msg($"Blocked item: {displayName} ({barcode})");
            NotificationHelper.Send(NotificationType.Success, $"Blocked: {displayName}");
            SettingsManager.MarkDirty();
        }

        public static void RemoveBlockedItem(string barcode)
        {
            for (int i = _blockedItems.Count - 1; i >= 0; i--)
            {
                if (_blockedItems[i].Barcode == barcode)
                {
                    string name = _blockedItems[i].DisplayName;
                    _blockedItems.RemoveAt(i);
                    Main.MelonLog.Msg($"Unblocked item: {name}");
                    NotificationHelper.Send(NotificationType.Success, $"Unblocked: {name}");
                    SettingsManager.MarkDirty();
                    return;
                }
            }
        }

        private static void ScanAndDespawnByBarcode()
        {
            try
            {
                var blockedBarcodes = new HashSet<string>();
                foreach (var bi in _blockedItems)
                    blockedBarcodes.Add(bi.Barcode);

                var idManager = NetworkEntityManager.IDManager;
                if (idManager?.RegisteredEntities?.EntityIDLookup == null) return;

                // Snapshot keys — same collection modification fix as player block
                var entities = idManager.RegisteredEntities.EntityIDLookup.Keys.ToList();

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null) continue;
                        if (entity.GetExtender<NetworkPlayer>() != null) continue;

                        string barcode = GetEntityBarcode(entity);
                        if (barcode != null && blockedBarcodes.Contains(barcode))
                        {
                            // Network despawn — server broadcasts to ALL clients
                            NetworkAssetSpawner.Despawn(new NetworkAssetSpawner.DespawnRequestInfo
                            {
                                EntityID = entity.ID,
                                DespawnEffect = false
                            });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"ScanAndDespawnByBarcode error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LOCAL BLOCK — Hide specific barcodes client-side only
        // ══════════════════════════════════════════════════════════

        public static void AddLocalBlockedItem(string barcode, string displayName)
        {
            for (int i = 0; i < _localBlockedItems.Count; i++)
            {
                if (_localBlockedItems[i].Barcode == barcode) return;
            }
            _localBlockedItems.Add(new BlockedItem { Barcode = barcode, DisplayName = displayName });
            Main.MelonLog.Msg($"Local blocked item: {displayName} ({barcode})");
            NotificationHelper.Send(NotificationType.Success, $"Local blocked: {displayName}");
            SettingsManager.MarkDirty();
        }

        public static void RemoveLocalBlockedItem(string barcode)
        {
            for (int i = _localBlockedItems.Count - 1; i >= 0; i--)
            {
                if (_localBlockedItems[i].Barcode == barcode)
                {
                    string name = _localBlockedItems[i].DisplayName;
                    _localBlockedItems.RemoveAt(i);
                    Main.MelonLog.Msg($"Local unblocked item: {name}");
                    NotificationHelper.Send(NotificationType.Success, $"Local unblocked: {name}");
                    SettingsManager.MarkDirty();
                    return;
                }
            }
        }

        private static void ScanAndHideLocally()
        {
            try
            {
                var blockedBarcodes = new HashSet<string>();
                foreach (var bi in _localBlockedItems)
                    blockedBarcodes.Add(bi.Barcode);

                // Find all Poolee objects in scene and destroy matching ones locally
                var poolees = UnityEngine.Object.FindObjectsOfType<Poolee>();
                if (poolees == null) return;

                foreach (var poolee in poolees)
                {
                    try
                    {
                        if (poolee == null) continue;
                        var crate = poolee.SpawnableCrate;
                        if (crate == null) continue;
                        var barcode = ((Scannable)crate).Barcode;
                        if (barcode == null) continue;
                        string barcodeId = barcode.ID;
                        if (string.IsNullOrEmpty(barcodeId)) continue;

                        if (blockedBarcodes.Contains(barcodeId))
                        {
                            // Disable the GameObject locally — doesn't trigger network despawn
                            poolee.gameObject.SetActive(false);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"ScanAndHideLocally error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Get barcode ID from a NetworkEntity via MarrowEntity → Poolee → SpawnableCrate → Barcode.
        /// </summary>
        private static string GetEntityBarcode(NetworkEntity entity)
        {
            try
            {
                var extender = entity.GetExtender<IMarrowEntityExtender>();
                if (extender == null) return null;
                var marrow = extender.MarrowEntity;
                if (marrow == null) return null;

                var poolee = marrow._poolee;
                if (poolee == null) return null;

                var crate = poolee.SpawnableCrate;
                if (crate == null) return null;

                var barcode = ((Scannable)crate).Barcode;
                return barcode?.ID;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolve a display name for a barcode from AssetWarehouse.
        /// </summary>
        public static string ResolveDisplayName(string barcode)
        {
            try
            {
                var pallets = AssetWarehouse.Instance.GetPallets();
                if (pallets == null) return barcode;

                foreach (var pallet in pallets)
                {
                    if (pallet == null) continue;
                    var crates = pallet.Crates;
                    if (crates == null) continue;
                    foreach (var crate in crates)
                    {
                        if (crate == null) continue;
                        var b = crate.Barcode;
                        if (b != null && b.ID == barcode)
                            return crate.name ?? barcode;
                    }
                }
            }
            catch { }
            return barcode;
        }

        // ══════════════════════════════════════════════════════════
        //  FULL RIG RESET (Wobbly Avatar + Frozen Rig)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fixes wobbly avatar / frozen rig by resetting physics pose and restarting the rig.
        /// Does NOT touch hand state.
        /// </summary>
        public static void FixWobblyAvatar()
        {
            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null)
                {
                    NotificationHelper.Send(NotificationType.Warning, "No RigManager found");
                    return;
                }

                var physRig = rigManager.physicsRig;
                if (physRig == null)
                {
                    NotificationHelper.Send(NotificationType.Warning, "No PhysicsRig found");
                    return;
                }

                // 1. Cap excessive avatar mass — heavy avatars have disproportionate
                // limb mass that overwhelms the fixed hand joint springs, causing wobble
                try
                {
                    var avatar = rigManager.avatar;
                    if (avatar != null)
                    {
                        float origTotal = avatar._massTotal;
                        bool capped = false;

                        // Cap individual body parts to sane maximums
                        // Normal 82kg avatar: head~5, chest~15, pelvis~12, arm~5, leg~10
                        if (avatar._massHead > 8f) { avatar._massHead = 8f; capped = true; }
                        if (avatar._massChest > 20f) { avatar._massChest = 20f; capped = true; }
                        if (avatar._massPelvis > 18f) { avatar._massPelvis = 18f; capped = true; }
                        if (avatar._massArm > 8f) { avatar._massArm = 8f; capped = true; }
                        if (avatar._massLeg > 15f) { avatar._massLeg = 15f; capped = true; }

                        if (capped)
                        {
                            // Recalculate total: chest + pelvis + head + (arm + leg) * 2
                            avatar._massTotal = avatar._massChest + avatar._massPelvis +
                                                avatar._massHead + (avatar._massArm + avatar._massLeg) * 2f;
                            Main.MelonLog.Msg($"Mass capped: {origTotal:F1}kg → {avatar._massTotal:F1}kg " +
                                $"(head={avatar._massHead:F1}, chest={avatar._massChest:F1}, " +
                                $"pelvis={avatar._massPelvis:F1}, arm={avatar._massArm:F1}, leg={avatar._massLeg:F1})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"Mass cap step skipped: {ex.Message}");
                }

                // 2. Reset MarrowEntity pose — resets all body positions + zeroes velocity
                try
                {
                    var marrowEntity = physRig.marrowEntity;
                    if (marrowEntity != null)
                    {
                        var defaultPoseCache = marrowEntity._defaultPoseCache;
                        var bodies = marrowEntity.Bodies;
                        if (defaultPoseCache != null && bodies != null)
                        {
                            for (int i = 0; i < bodies.Length && i < defaultPoseCache.Length; i++)
                            {
                                var body = bodies[i];
                                if (body == null) continue;
                                var pose = defaultPoseCache[i];
                                ((Component)body).transform.localPosition = pose.position;
                                ((Component)body).transform.localRotation = pose.rotation;
                                var rb = body._rigidbody;
                                if (rb != null)
                                {
                                    rb.velocity = Vector3.zero;
                                    rb.angularVelocity = Vector3.zero;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"ResetPose step skipped: {ex.Message}");
                }

                // 3. Full rig shutdown + restart cycle (applies capped masses to rigidbodies)
                physRig.ShutdownRig();
                physRig.TurnOnRig();
                physRig.UnRagdollRig();

                Main.MelonLog.Msg("Wobbly avatar fix applied (mass cap + pose reset + rig restart)");
                NotificationHelper.Send(NotificationType.Success, "Wobbly avatar fixed (mass capped)");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"FixWobblyAvatar error: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Fix failed: {ex.Message}");
            }
        }


    }
}
