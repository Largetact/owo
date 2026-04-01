using MelonLoader;
using UnityEngine;
using BoneLib;
using HarmonyLib;
using System;
using System.Reflection;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppInterop.Runtime.InteropTypes;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Force Spawner - Keeps the spawn menu open, shows a mesh preview of the selected
    /// spawnable, and double-tap spawns at a distance in front of the player.
    /// Ported from Dynamic_Bone_Extender (Sharki).
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

        // Preview state
        private static Mesh _previewMesh;
        private static GameObject _previewObj;
        private static GameObject _spawnGunHolder;
        private static Vector3 _currentPos;

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

        // ── Preview Methods (from DBE) ──

        /// <summary>
        /// Creates/updates the mesh preview object at the current spawn position.
        /// </summary>
        private static void UpdatePreview()
        {
            if (_previewMesh == null) return;

            DeletePreview();
            var obj = new GameObject("ForceSpawnerPreview");
            obj.transform.localScale = Vector3.one;
            obj.transform.position = _currentPos;

            var meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.mesh = _previewMesh;

            var meshRenderer = obj.AddComponent<MeshRenderer>();

            // Steal a material from the player rig or scene, clear its textures, tint green
            try
            {
                var rigMgr = Player.RigManager;
                MeshRenderer sourceMr = (rigMgr != null)
                    ? rigMgr.GetComponentInChildren<MeshRenderer>()
                    : null;
                if (sourceMr == null)
                    sourceMr = UnityEngine.Object.FindObjectOfType<MeshRenderer>();

                if (sourceMr != null)
                {
                    meshRenderer.material = new Material(sourceMr.material);
                    var shader = meshRenderer.material.shader;
                    int propCount = shader.GetPropertyCount();
                    for (int i = 0; i < propCount; i++)
                    {
                        if ((int)shader.GetPropertyType(i) == 4) // Texture
                            meshRenderer.material.SetTexture(shader.GetPropertyName(i), null);
                    }
                    meshRenderer.material.color = new Color(0f, 1f, 0.5f, 1f);
                }
            }
            catch { }

            _previewObj = obj;
        }

        /// <summary>
        /// Destroys the current preview object.
        /// </summary>
        public static void DeletePreview()
        {
            if (_previewObj != null)
            {
                UnityEngine.Object.Destroy(_previewObj);
                _previewObj = null;
            }
        }

        /// <summary>
        /// Updates the spawn position in front of the player's head and moves the preview.
        /// </summary>
        private static void UpdateHeadPos()
        {
            if (Player.Head == null) return;

            Transform head = Player.Head;
            Vector3 pos = head.position + head.forward * _distance;
            // Apply offset (relative to head orientation)
            pos += head.right * _offsetX + head.up * _offsetY + head.forward * _offsetZ;
            _currentPos = pos;

            if (_previewObj != null)
                _previewObj.transform.position = _currentPos;
        }

        /// <summary>
        /// Call this every frame from the main update loop.
        /// </summary>
        public static void Update()
        {
            if (!_enabled) return;
            UpdateHeadPos();
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
        /// Postfix on PanelView.Activate - Create a dummy SpawnGun so the spawnable panel works.
        /// </summary>
        [HarmonyPatch]
        public static class PanelViewActivatePatch
        {
            static MethodBase TargetMethod()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "PanelView")
                            {
                                var method = t.GetMethod("Activate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                try
                {
                    // Check if this is a SpawnablesPanelView
                    var spvType = __instance.GetType().Assembly.GetType("Il2CppSLZ.Marrow.SpawnablesPanelView")
                               ?? __instance.GetType().Assembly.GetType("SLZ.Marrow.SpawnablesPanelView");
                    if (spvType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            try
                            {
                                foreach (var t in asm.GetTypes())
                                {
                                    if (t.Name == "SpawnablesPanelView")
                                    { spvType = t; break; }
                                }
                                if (spvType != null) break;
                            }
                            catch { }
                        }
                    }
                    if (spvType == null) return;

                    // TryCast to SpawnablesPanelView
                    var il2cppObj = __instance as Il2CppObjectBase;
                    if (il2cppObj == null) return;
                    var tryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast")?.MakeGenericMethod(spvType);
                    var spv = tryCastMethod?.Invoke(il2cppObj, null);
                    if (spv == null) return;

                    // Create a dummy SpawnGun and assign it to the panel
                    var holder = new GameObject("ForceSpawnerGunHolder");
                    Type spawnGunType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var t in asm.GetTypes())
                            {
                                if (t.Name == "SpawnGun")
                                { spawnGunType = t; break; }
                            }
                            if (spawnGunType != null) break;
                        }
                        catch { }
                    }
                    if (spawnGunType != null)
                    {
                        var spawnGun = holder.AddComponent(Il2CppInterop.Runtime.Il2CppType.From(spawnGunType));
                        var spawnGunField = spvType.GetProperty("spawnGun", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                         ?? (MemberInfo)spvType.GetField("spawnGun", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (spawnGunField is PropertyInfo pi)
                            pi.SetValue(spv, spawnGun);
                        else if (spawnGunField is FieldInfo fi)
                            fi.SetValue(spv, spawnGun);
                    }
                    _spawnGunHolder = holder;
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[ForceSpawner] PanelView.Activate error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Postfix on PanelView.Deactivate - Clean up when panel closes.
        /// </summary>
        [HarmonyPatch]
        public static class PanelViewDeactivatePatch
        {
            static MethodBase TargetMethod()
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "PanelView")
                            {
                                var method = t.GetMethod("Deactivate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                try
                {
                    // Check if this is a SpawnablesPanelView
                    Type spvType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var t in asm.GetTypes())
                            {
                                if (t.Name == "SpawnablesPanelView")
                                { spvType = t; break; }
                            }
                            if (spvType != null) break;
                        }
                        catch { }
                    }
                    if (spvType == null) return;

                    var il2cppObj = __instance as Il2CppObjectBase;
                    if (il2cppObj == null) return;
                    var tryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast")?.MakeGenericMethod(spvType);
                    var spv = tryCastMethod?.Invoke(il2cppObj, null);
                    if (spv == null) return;

                    // It's a SpawnablesPanelView closing — clean up
                    _selectedTimes = 0;
                    _currentCrateSelected = "";
                    if (_spawnGunHolder != null)
                    {
                        UnityEngine.Object.Destroy(_spawnGunHolder);
                        _spawnGunHolder = null;
                    }
                    DeletePreview();
                }
                catch { }
            }
        }

        /// <summary>
        /// Postfix on SpawnablesPanelView.SelectItem - First tap shows preview, second tap spawns.
        /// Matches Dynamic_Bone_Extender's spawncreate method.
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
                    var selectedObjProp = __instance.GetType().GetProperty("selectedObject",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (selectedObjProp == null) return;
                    var selectedObj = selectedObjProp.GetValue(__instance);
                    if (selectedObj == null) return;

                    var crate = selectedObj as SpawnableCrate;
                    if (crate == null) return;

                    string barcode = ((Scannable)crate).Barcode?.ToString() ?? "";
                    if (string.IsNullOrEmpty(barcode)) return;

                    Transform head = Player.Head;
                    if (head == null) return;
                    Vector3 spawnPos = head.position + head.forward * _distance;
                    spawnPos += head.right * _offsetX + head.up * _offsetY + head.forward * _offsetZ;

                    if (_currentCrateSelected != barcode)
                    {
                        // Different crate selected — load preview mesh
                        _currentCrateSelected = barcode;
                        _selectedTimes = 1;

                        // Load PreviewMesh from GameObjectCrate (SpawnableCrate extends it)
                        try
                        {
                            LoadPreviewMesh(crate, barcode, spawnPos);
                        }
                        catch (Exception ex)
                        {
                            Main.MelonLog.Warning($"[ForceSpawner] Preview load error: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Same crate selected again
                        _selectedTimes++;
                        DeletePreview();

                        if (_selectedTimes >= 2)
                        {
                            _selectedTimes = 0;
                            SpawnAtDistance(barcode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[ForceSpawner] SelectItem error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Loads the preview mesh from the crate's PreviewMesh asset.
        /// </summary>
        private static void LoadPreviewMesh(SpawnableCrate crate, string barcode, Vector3 spawnPos)
        {
            // SpawnableCrate -> GameObjectCrate -> PreviewMesh (MarrowAssetT<Mesh>)
            // We need to call PreviewMesh.LoadAsset(Action<Mesh>)
            var gameObjCrateType = crate.GetType();

            // Walk up to find PreviewMesh property
            PropertyInfo previewMeshProp = null;
            var searchType = gameObjCrateType;
            while (searchType != null && previewMeshProp == null)
            {
                previewMeshProp = searchType.GetProperty("PreviewMesh",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                searchType = searchType.BaseType;
            }

            if (previewMeshProp == null) return;

            var previewMeshAsset = previewMeshProp.GetValue(crate);
            if (previewMeshAsset == null) return;

            // Get LoadAsset method with Action<Mesh> parameter
            var loadAssetMethod = previewMeshAsset.GetType().GetMethod("LoadAsset",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Il2CppSystem.Action<Mesh>) }, null);

            if (loadAssetMethod == null)
            {
                // Try with Action<UnityEngine.Object> or generic overloads
                foreach (var m in previewMeshAsset.GetType().GetMethods())
                {
                    if (m.Name == "LoadAsset")
                    {
                        loadAssetMethod = m;
                        break;
                    }
                }
            }

            if (loadAssetMethod == null) return;

            // Create the callback
            System.Action<Mesh> callback = (Mesh mesh) =>
            {
                if (mesh != null)
                {
                    _previewMesh = mesh;
                    _currentPos = spawnPos;
                    DeletePreview();
                    UpdatePreview();
                }
            };

            // Convert to Il2Cpp Action
            var il2cppAction = (Il2CppSystem.Action<Mesh>)callback;
            loadAssetMethod.Invoke(previewMeshAsset, new object[] { il2cppAction });
        }

        private static void UnredactAllCrates()
        {
            try
            {
                var warehouse = AssetWarehouse.Instance;
                if (warehouse == null) return;

                var pallets = warehouse.GetPallets();
                if (pallets == null) return;

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

                        bool backingVal = scannable._redacted;
                        bool propVal = scannable.Redacted;

                        if (backingVal || propVal)
                        {
                            scannable._redacted = false;
                            scannable.Redacted = false;
                            count++;
                        }
                    }
                }

                Main.MelonLog.Msg($"[ForceSpawner] Unredact complete: {count}/{totalCrates} crates unredacted");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceSpawner] Unredact error: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn an item at a distance in front of the player's head.
        /// </summary>
        private static void SpawnAtDistance(string barcode)
        {
            try
            {
                Transform head = Player.Head;
                if (head == null) return;

                Vector3 spawnPos = head.position + head.forward * _distance;
                spawnPos += head.right * _offsetX + head.up * _offsetY + head.forward * _offsetZ;

                SpawnableCrateReference crateRef = new SpawnableCrateReference(barcode);
                Spawnable spawnable = new Spawnable
                {
                    crateRef = crateRef
                };

                bool isNetworked = false;
                try { isNetworked = LabFusion.Scene.NetworkSceneManager.IsLevelNetworked; } catch { }

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
                        AssetSpawner.Register(spawnable);
                        AssetSpawner.Spawn(spawnable, spawnPos, head.rotation);
                    }
                }
                else
                {
                    AssetSpawner.Register(spawnable);
                    AssetSpawner.Spawn(spawnable, spawnPos, head.rotation);
                }

                DeletePreview();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[ForceSpawner] Spawn error: {ex.Message}");
            }
        }
    }
}
