using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Pool;
using LabFusion.Entities;
using LabFusion.Marrow.Extenders;
using LabFusion.Network;
using LabFusion.RPC;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Filter types for Despawn All — mirrors FusionProtector's DespawnerAll enum.
    /// </summary>
    public enum DespawnFilter
    {
        NoFilter,
        Guns,
        Melees,
        Npcs,
        EverythingButGuns,
        EverythingButMelees,
        EverythingButNpcs,
        NetworkProps,
        AllNotButtonsLevers
    }

    /// <summary>
    /// Despawn All Controller — based on FusionProtector's despawn system.
    /// Uses NetworkAssetSpawner.Despawn to network-despawn entities.
    /// Removes the Owner permission requirement so ANY player can despawn.
    /// Includes auto-despawn timer (MelonCoroutines) and filter support.
    /// </summary>
    public static class DespawnAllController
    {
        // ── Settings ──────────────────────────────────────────────
        private static DespawnFilter _filter = DespawnFilter.NoFilter;
        private static bool _autoDespawnEnabled = false;
        private static float _autoDespawnIntervalMins = 5f;
        private static bool _keepHolsteredItems = false;
        private static bool _keepOnlyMyHolsters = true;

        // ── Timer state ───────────────────────────────────────────
        private static object _timerCoroutine = null;

        // ── Properties ────────────────────────────────────────────

        public static DespawnFilter Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                Main.MelonLog.Msg($"Despawn filter set to: {value}");
            }
        }

        public static bool AutoDespawnEnabled
        {
            get => _autoDespawnEnabled;
            set
            {
                _autoDespawnEnabled = value;
                Main.MelonLog.Msg($"Auto-Despawn {(value ? "ENABLED" : "DISABLED")}");
                if (value)
                    StartTimer();
                else
                    StopTimer();
            }
        }

        public static float AutoDespawnIntervalMins
        {
            get => _autoDespawnIntervalMins;
            set
            {
                _autoDespawnIntervalMins = Mathf.Clamp(value, 0f, 60f);
                Main.MelonLog.Msg($"Auto-Despawn interval set to {_autoDespawnIntervalMins} min");
                // Restart timer with new interval if running
                if (_autoDespawnEnabled)
                {
                    StopTimer();
                    StartTimer();
                }
            }
        }

        public static bool KeepHolsteredItems
        {
            get => _keepHolsteredItems;
            set
            {
                _keepHolsteredItems = value;
                Main.MelonLog.Msg($"Keep Holstered Items: {(value ? "ON" : "OFF")}");
            }
        }

        public static bool KeepOnlyMyHolsters
        {
            get => _keepOnlyMyHolsters;
            set
            {
                _keepOnlyMyHolsters = value;
                Main.MelonLog.Msg($"Keep Only My Holsters: {(value ? "ON" : "OFF")}");
            }
        }

        // ── Initialization ────────────────────────────────────────

        public static void Initialize()
        {
            Main.MelonLog.Msg("DespawnAllController initialized (FusionProtector-style, no permission check)");
        }

        /// <summary>
        /// Called every frame from Main.OnUpdate. Checks for Backspace keybind.
        /// </summary>
        public static void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
            {
                DespawnAll();
            }
        }

        // ── Timer (SimpleTimer pattern from FusionProtector) ──────

        private static void StartTimer()
        {
            StopTimer();
            _timerCoroutine = MelonCoroutines.Start(RunEveryXMins());
        }

        private static void StopTimer()
        {
            if (_timerCoroutine != null)
            {
                MelonCoroutines.Stop(_timerCoroutine);
                _timerCoroutine = null;
            }
        }

        private static IEnumerator RunEveryXMins()
        {
            while (true)
            {
                float waitSeconds = _autoDespawnIntervalMins * 60f;
                yield return new WaitForSecondsRealtime(waitSeconds);
                try
                {
                    if (_autoDespawnEnabled && NetworkInfo.HasServer)
                    {
                        Main.MelonLog.Msg("Auto-Despawn timer fired — despawning all...");
                        DespawnAll();
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"Auto-Despawn timer error: {ex.Message}");
                }
            }
        }

        // ── Holster Detection ─────────────────────────────────────

        /// <summary>
        /// Collect the instance IDs of all GameObjects currently sitting in holster slots.
        /// If onlyLocal is true, only checks the local player's holsters.
        /// If false, checks ALL RigManagers in the scene (all players).
        /// </summary>
        private static HashSet<int> GetHolsteredObjectIds(bool onlyLocal)
        {
            var ids = new HashSet<int>();
            try
            {
                if (onlyLocal)
                {
                    // Local player only
                    var rigManager = Player.RigManager;
                    if (rigManager != null)
                    {
                        CollectHolsteredIds(rigManager, ids);
                    }
                }
                else
                {
                    // All players in the scene
                    var allRigs = UnityEngine.Object.FindObjectsOfType<RigManager>();
                    foreach (var rig in allRigs)
                    {
                        if (rig != null)
                        {
                            CollectHolsteredIds(rig, ids);
                        }
                    }
                }
                Main.MelonLog.Msg($"[Holster] Found {ids.Count} holstered object(s) (onlyLocal={onlyLocal})");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Holster] Detection error: {ex.Message}");
            }
            return ids;
        }

        /// <summary>
        /// For a given RigManager, find all InventorySlotReceiver components and
        /// add the instance IDs of their holstered items' root GameObjects.
        /// </summary>
        private static void CollectHolsteredIds(RigManager rigManager, HashSet<int> ids)
        {
            try
            {
                var slots = ((Component)rigManager).GetComponentsInChildren<InventorySlotReceiver>(true);
                if (slots == null) return;

                foreach (var slot in slots)
                {
                    if (slot == null) continue;
                    try
                    {
                        // _slottedWeapon is the WeaponSlot of the holstered item
                        var weaponSlot = slot._slottedWeapon;
                        if (weaponSlot == null) continue;

                        // Get the holstered item's GameObject
                        var itemGo = ((Component)weaponSlot).gameObject;
                        if (itemGo == null) continue;

                        ids.Add(itemGo.GetInstanceID());

                        // Also add all parent GameObjects up to but not including the slot itself,
                        // because the NetworkEntity's MarrowEntity may be on a parent object
                        var parent = itemGo.transform.parent;
                        while (parent != null)
                        {
                            // Stop if we've reached the slot or the rig itself
                            if (parent.GetComponent<InventorySlotReceiver>() != null) break;
                            if (parent.GetComponent<RigManager>() != null) break;
                            ids.Add(parent.gameObject.GetInstanceID());
                            parent = parent.parent;
                        }

                        // Also add InteractableHost's GameObject if available
                        try
                        {
                            var host = weaponSlot.interactableHost;
                            if (host != null)
                            {
                                ids.Add(((Component)host).gameObject.GetInstanceID());
                            }
                        }
                        catch { }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Holster] CollectHolsteredIds error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a NetworkEntity's GameObject matches any holstered object ID.
        /// </summary>
        private static bool IsEntityHolstered(NetworkEntity entity, HashSet<int> holsteredIds)
        {
            if (holsteredIds == null || holsteredIds.Count == 0) return false;
            try
            {
                var extender = entity.GetExtender<IMarrowEntityExtender>();
                if (extender == null) return false;
                var marrow = extender.MarrowEntity;
                if (marrow == null) return false;

                var go = ((Component)marrow).gameObject;
                if (go == null) return false;

                // Check the entity's own GameObject
                if (holsteredIds.Contains(go.GetInstanceID())) return true;

                // Check all children (holstered item might be a child of the entity root)
                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && holsteredIds.Contains(child.gameObject.GetInstanceID()))
                        return true;
                }
            }
            catch { }
            return false;
        }

        // ── Core: NetworkEntities (from FusionProtector) ──────────

        /// <summary>
        /// Collect all registered NetworkEntities that are NOT player rigs.
        /// Mirrors FusionProtector's NetworkEntities() helper.
        /// </summary>
        private static HashSet<NetworkEntity> GetNetworkEntities()
        {
            try
            {
                var idManager = NetworkEntityManager.IDManager;
                if (idManager?.RegisteredEntities?.EntityIDLookup == null)
                    return new HashSet<NetworkEntity>();

                return idManager.RegisteredEntities.EntityIDLookup.Keys
                    .Where(entity =>
                    {
                        // Must have a MarrowEntity (real world object)
                        var extender = entity.GetExtender<IMarrowEntityExtender>();
                        if (extender == null) return false;
                        var marrow = extender.MarrowEntity;
                        if (marrow == null) return false;

                        // Skip player rigs (same check FusionProtector uses via NetworkPlayer extender)
                        if (entity.GetExtender<NetworkPlayer>() != null) return false;

                        return true;
                    })
                    .ToHashSet();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"GetNetworkEntities error: {ex.Message}");
                return new HashSet<NetworkEntity>();
            }
        }

        // ── Core: DespawnNow (FusionProtector style, NO permission check) ──

        /// <summary>
        /// Despawn a single NetworkEntity via the network.
        /// FusionProtector checks PermissionLevel == Owner here — we skip that.
        /// </summary>
        private static void DespawnNow(NetworkEntity entity)
        {
            try
            {
                NetworkAssetSpawner.Despawn(new NetworkAssetSpawner.DespawnRequestInfo
                {
                    EntityID = entity.ID,
                    DespawnEffect = false
                });
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"DespawnNow error: {ex.Message}");
            }
        }

        // ── Filter helpers (from FusionProtector's NetworkEntityExtensions) ──

        private static bool IsGun(NetworkEntity entity)
        {
            try
            {
                var marrow = entity.GetExtender<IMarrowEntityExtender>()?.MarrowEntity;
                if (marrow == null) return false;
                return ((Component)marrow).gameObject.GetComponent<Gun>() != null;
            }
            catch { return false; }
        }

        private static bool IsMelee(NetworkEntity entity)
        {
            try
            {
                var marrow = entity.GetExtender<IMarrowEntityExtender>()?.MarrowEntity;
                if (marrow == null) return false;
                return ((Component)marrow).gameObject.GetComponent<StabSlash>() != null;
            }
            catch { return false; }
        }

        private static bool IsNPC(NetworkEntity entity)
        {
            try
            {
                var marrow = entity.GetExtender<IMarrowEntityExtender>()?.MarrowEntity;
                if (marrow == null) return false;
                return ((Component)marrow).gameObject.GetComponent<AIBrain>() != null;
            }
            catch { return false; }
        }

        private static bool IsNetworkProp(NetworkEntity entity)
        {
            try
            {
                return entity.GetExtender<NetworkProp>() != null
                    && entity.GetExtender<PooleeExtender>() != null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if entity is a Magazine whose plug is currently locked into a gun socket.
        /// </summary>
        private static bool IsMagazineInGun(NetworkEntity entity)
        {
            try
            {
                var marrow = entity.GetExtender<IMarrowEntityExtender>()?.MarrowEntity;
                if (marrow == null) return false;
                var go = ((Component)marrow).gameObject;

                var magazine = go.GetComponent<Magazine>();
                if (magazine == null) return false;
                if (go.GetComponent<Gun>() != null) return false; // gun itself, not a loose magazine

                var plug = magazine.magazinePlug;
                if (plug != null && plug._isLocked)
                    return true;

                return false;
            }
            catch { return false; }
        }

        private static bool IsButtonOrLever(NetworkEntity entity)
        {
            try
            {
                var marrow = entity.GetExtender<IMarrowEntityExtender>()?.MarrowEntity;
                if (marrow == null) return false;
                var go = ((Component)marrow).gameObject;

                // ButtonNode and HingeController are Il2Cpp runtime types — resolve by name
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetIl2CppType()?.Name;
                    if (typeName == "ButtonNode" || typeName == "HingeController")
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if an entity passes the current filter (should it be despawned?).
        /// Logic mirrors FusionProtector's DespawnAll switch block.
        /// </summary>
        private static bool PassesFilter(NetworkEntity entity, DespawnFilter filter)
        {
            switch (filter)
            {
                case DespawnFilter.NoFilter:
                    return true;

                case DespawnFilter.Guns:
                    return IsGun(entity);

                case DespawnFilter.Melees:
                    return IsMelee(entity);

                case DespawnFilter.Npcs:
                    return IsNPC(entity);

                case DespawnFilter.EverythingButGuns:
                    return !IsGun(entity);

                case DespawnFilter.EverythingButMelees:
                    return !IsMelee(entity);

                case DespawnFilter.EverythingButNpcs:
                    return !IsNPC(entity);

                case DespawnFilter.NetworkProps:
                    return IsNetworkProp(entity);

                case DespawnFilter.AllNotButtonsLevers:
                    return !IsButtonOrLever(entity);

                default:
                    return true;
            }
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Despawn all network entities matching the current filter.
        /// No permission check — works for any player, not just Owner.
        /// </summary>
        public static void DespawnAll()
        {
            try
            {
                if (!NetworkInfo.HasServer)
                {
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning,
                        "Not connected to a server");
                    return;
                }

                // Collect holstered item IDs if protection is enabled
                HashSet<int> holsteredIds = null;
                if (_keepHolsteredItems)
                {
                    holsteredIds = GetHolsteredObjectIds(_keepOnlyMyHolsters);
                }

                var entities = GetNetworkEntities();
                int count = 0;
                int skippedHolster = 0;
                int skippedMag = 0;

                foreach (var entity in entities)
                {
                    if (PassesFilter(entity, _filter))
                    {
                        // Skip holstered items if protection is on
                        if (holsteredIds != null && IsEntityHolstered(entity, holsteredIds))
                        {
                            skippedHolster++;
                            continue;
                        }

                        // Skip magazines currently inserted in a gun
                        if (IsMagazineInGun(entity))
                        {
                            skippedMag++;
                            continue;
                        }

                        DespawnNow(entity);
                        count++;
                    }
                }

                string holsterMsg = skippedHolster > 0 ? $", kept {skippedHolster} holstered" : "";
                string magMsg = skippedMag > 0 ? $", kept {skippedMag} mags in guns" : "";
                Main.MelonLog.Msg($"Despawned {count} entities (filter: {_filter}{holsterMsg}{magMsg})");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success,
                    $"Despawned {count} entities ({_filter}{holsterMsg}{magMsg})");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"DespawnAll error: {ex.Message}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error,
                    $"Despawn failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to network-despawn a single object by finding its NetworkEntity.
        /// Returns true if successfully despawned via network, false if not found or failed.
        /// Used by ObjectLauncher cleanup to FusionSync its despawn operations.
        /// </summary>
        public static bool TryDespawnObject(GameObject go)
        {
            try
            {
                if (go == null) return false;
                if (!NetworkInfo.HasServer) return false;

                var entities = GetNetworkEntities();
                foreach (var entity in entities)
                {
                    var extender = entity.GetExtender<IMarrowEntityExtender>();
                    if (extender == null) continue;
                    var marrow = extender.MarrowEntity;
                    if (marrow == null) continue;

                    var entityGo = ((Component)marrow).gameObject;
                    if (entityGo == null) continue;

                    // Check if this entity matches the target object (direct, parent, or child)
                    if (entityGo == go
                        || entityGo.transform.IsChildOf(go.transform)
                        || go.transform.IsChildOf(entityGo.transform))
                    {
                        DespawnNow(entity);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
