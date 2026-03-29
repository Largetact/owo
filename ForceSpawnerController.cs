using MelonLoader;
using UnityEngine;
using BoneLib;
using HarmonyLib;
using System;
using System.Reflection;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Force Spawner - Keeps the spawn menu open and allows double-tap spawning
    /// at a distance in front of the player. Ported from Dynamic_Bone_Extender.
    /// </summary>
    [HarmonyPatch]
    public static class ForceSpawnerController
    {
        private static bool _enabled = false;
        private static bool _unredactAll = false;
        private static int _distance = 2;
        private static float _offsetX = 0f;
        private static float _offsetY = 0f;
        private static float _offsetZ = 0f;

        private static int _selectedTimes = 0;
        private static string _currentCrateSelected = "";
        private static bool _earlyUnredactDone = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static bool UnredactAll
        {
            get => _unredactAll;
            set => _unredactAll = value;
        }

        public static int Distance
        {
            get => _distance;
            set => _distance = Mathf.Clamp(value, 1, 50);
        }

        public static float OffsetX
        {
            get => _offsetX;
            set => _offsetX = value;
        }

        public static float OffsetY
        {
            get => _offsetY;
            set => _offsetY = value;
        }

        public static float OffsetZ
        {
            get => _offsetZ;
            set => _offsetZ = value;
        }

        public static void Initialize()
        {
            try
            {
                AssetWarehouse.OnReady((Il2CppSystem.Action)new System.Action(() =>
                {
                    if (_unredactAll && !_earlyUnredactDone)
                    {
                        Main.MelonLog.Msg("[ForceSpawner] AssetWarehouse ready — running early unredact");
                        UnredactAllCrates();
                        _earlyUnredactDone = true;
                    }
                }));
            }
            catch (System.Exception ex)
            {
                Main.MelonLog.Warning($"[ForceSpawner] Failed to hook AssetWarehouse.OnReady: {ex.Message}");
            }
        }

        // ── Harmony Patches ──

        /// <summary>
        /// PostFix on PopUpMenuView.Start - Force add the spawn menu
        /// </summary>
        [HarmonyPatch]
        public static class PopUpMenuViewStartPatch
        {
            static MethodBase TargetMethod()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "PopUpMenuView")
                            {
                                var method = t.GetMethod("Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (method != null) return method;
                            }
                        }
                    }
                    catch { }
                }
                return null;
            }

            static void Postfix(object __instance)
            {
                if (!_enabled) return;

                // Unredact BEFORE spawning the menu so the panel sees updated state
                if (_unredactAll)
                    UnredactAllCrates();

                try
                {
                    var addSpawnMethod = __instance.GetType().GetMethod("AddSpawnMenu",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    addSpawnMethod?.Invoke(__instance, null);
                }
                catch { }
            }
        }

        /// <summary>
        /// Prefix on PopUpMenuView.RemoveSpawnMenu - Block removal when enabled
        /// </summary>
        [HarmonyPatch]
        public static class PopUpMenuViewRemoveSpawnMenuPatch
        {
            static MethodBase TargetMethod()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "PopUpMenuView")
                            {
                                var method = t.GetMethod("RemoveSpawnMenu", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (method != null) return method;
                            }
                        }
                    }
                    catch { }
                }
                return null;
            }

            static bool Prefix()
            {
                return !_enabled;
            }
        }

        /// <summary>
        /// Postfix on SpawnablesPanelView.SelectItem - Double-tap to spawn at distance.
        /// Matches Dynamic_Bone_Extender's spawncreate method exactly.
        /// </summary>
        [HarmonyPatch]
        public static class SpawnablesPanelViewSelectItemPatch
        {
            static MethodBase TargetMethod()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "SpawnablesPanelView")
                            {
                                var method = t.GetMethod("SelectItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (method != null) return method;
                            }
                        }
                    }
                    catch { }
                }
                return null;
            }

            static void Postfix(object __instance, int idx)
            {
                if (!_enabled) return;
                try
                {
                    // Get selectedObject (SpawnableCrate) via reflection since SpawnablesPanelView
                    // may not be directly referenceable
                    var selectedObjProp = __instance.GetType().GetProperty("selectedObject",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (selectedObjProp == null) return;
                    var selectedObj = selectedObjProp.GetValue(__instance);
                    if (selectedObj == null) return;

                    // Cast to SpawnableCrate (extends Crate extends Scannable)
                    var crate = selectedObj as SpawnableCrate;
                    if (crate == null) return;

                    string barcode = ((Scannable)crate).Barcode?.ToString() ?? "";
                    if (string.IsNullOrEmpty(barcode)) return;

                    // Track double-tap
                    if (_currentCrateSelected != barcode)
                    {
                        _currentCrateSelected = barcode;
                        _selectedTimes = 1;
                    }
                    else
                    {
                        _selectedTimes++;
                    }

                    if (_selectedTimes == 2)
                    {
                        _selectedTimes = 0;
                        SpawnAtDistance(barcode);
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[ForceSpawner] SelectItem error: {ex.Message}");
                }
            }
        }

        private static void UnredactAllCrates()
        {
            try
            {
                var warehouse = AssetWarehouse.Instance;
                if (warehouse == null)
                {
                    Main.MelonLog.Warning("[ForceSpawner] Unredact: AssetWarehouse.Instance is null");
                    return;
                }

                var pallets = warehouse.GetPallets();
                if (pallets == null)
                {
                    Main.MelonLog.Warning("[ForceSpawner] Unredact: GetPallets() returned null");
                    return;
                }

                int count = 0;
                int totalCrates = 0;
                for (int i = 0; i < pallets.Count; i++)
                {
                    var pallet = pallets[i];
                    if (pallet == null) continue;

                    var crates = pallet.Crates;
                    if (crates == null) continue;

                    for (int j = 0; j < crates.Count; j++)
                    {
                        var crate = crates[j];
                        if (crate == null) continue;

                        totalCrates++;
                        var scannable = (Scannable)crate;

                        // Read backing field directly (bypasses native getter)
                        bool backingVal = scannable._redacted;
                        bool propVal = scannable.Redacted;

                        if (backingVal || propVal)
                        {
                            // Write backing field directly (bypasses native setter)
                            scannable._redacted = false;
                            // Also set the property in case native code reads differently
                            scannable.Redacted = false;
                            count++;

                            Main.MelonLog.Msg($"[ForceSpawner]   Unredacted: {scannable.Title} (field={backingVal}, prop={propVal})");
                        }
                    }
                }

                Main.MelonLog.Msg($"[ForceSpawner] Unredact complete: {count}/{totalCrates} crates unredacted");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceSpawner] Unredact error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Spawn an item at a distance in front of the player's head.
        /// Matches Dynamic_Bone_Extender's spawncreate logic.
        /// </summary>
        private static void SpawnAtDistance(string barcode)
        {
            try
            {
                Transform head = Player.Head;
                if (head == null) return;

                Vector3 spawnPos = head.position + head.forward * _distance;

                // Apply offset (relative to head orientation)
                spawnPos += head.right * _offsetX + head.up * _offsetY + head.forward * _offsetZ;

                SpawnableCrateReference crateRef = new SpawnableCrateReference(barcode);
                Spawnable spawnable = new Spawnable
                {
                    crateRef = crateRef
                };

                // Check if level is networked — try LabFusion's NetworkSceneManager
                bool isNetworked = false;
                try
                {
                    isNetworked = LabFusion.Scene.NetworkSceneManager.IsLevelNetworked;
                }
                catch { }

                if (isNetworked)
                {
                    try
                    {
                        var spawnReq = new LabFusion.RPC.NetworkAssetSpawner.SpawnRequestInfo
                        {
                            Spawnable = spawnable,
                            Position = spawnPos,
                            Rotation = head.rotation
                        };
                        LabFusion.RPC.NetworkAssetSpawner.Spawn(spawnReq);
                    }
                    catch
                    {
                        // Fallback to local spawn if network spawn fails
                        AssetSpawner.Register(spawnable);
                        AssetSpawner.Spawn(spawnable, spawnPos, head.rotation);
                    }
                }
                else
                {
                    AssetSpawner.Register(spawnable);
                    AssetSpawner.Spawn(spawnable, spawnPos, head.rotation);
                }

                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success,
                    $"Spawned at distance {_distance}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceSpawner] Spawn error: {ex.Message}");
            }
        }
    }
}
