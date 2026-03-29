using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BonelabUtilityMod
{
    public static class SpawnMenuController
    {
        // Item cache
        private static List<SpawnableItem> _allItems = new List<SpawnableItem>();
        private static List<SpawnableItem> _filteredItems = new List<SpawnableItem>();

        // Search and pagination
        private static string _searchQuery = "";
        private static int _currentPage = 0;
        private static int _itemsPerPage = 10;
        private static int _selectedIndex = 0;

        // Current selected item
        private static SpawnableItem _selectedItem = default;

        // Spawn settings
        private static float _spawnDistance = 1.5f;

        public struct SpawnableItem
        {
            public string Title;
            public string BarcodeID;
            public string PalletName;

            public bool IsValid => !string.IsNullOrEmpty(BarcodeID);
        }

        // Regex to strip Unity rich text tags like <color=#FF0000>, </color>, <b>, </b>, etc.
        private static readonly Regex RichTextRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        private static string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return RichTextRegex.Replace(input, string.Empty).Trim();
        }

        public static float SpawnDistance
        {
            get => _spawnDistance;
            set => _spawnDistance = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value ?? "";
                _currentPage = 0;
                _selectedIndex = 0;
                FilterItems();
            }
        }

        public static int CurrentPage => _currentPage;
        public static int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_filteredItems.Count / _itemsPerPage));
        public static int FilteredItemCount => _filteredItems.Count;
        public static int TotalItemCount => _allItems.Count;

        public static SpawnableItem? SelectedItem => _selectedItem.IsValid ? _selectedItem : (SpawnableItem?)null;

        public static void Initialize()
        {
            Main.MelonLog.Msg("Spawn Menu controller initialized");
        }

        public static void RefreshItemList()
        {
            try
            {
                Main.MelonLog.Msg("Refreshing spawnable items list...");
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
                    Main.MelonLog.Error("AssetWarehouse.Instance property not found!");
                    return;
                }

                var warehouseInstance = instanceProp.GetValue(null);
                if (warehouseInstance == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse.Instance is null - game may not be fully loaded");
                    SendNotification(NotificationType.Warning, "Game not fully loaded yet");
                    return;
                }

                // Find SpawnableCrate type
                var spawnableCrateType = FindTypeByName("SpawnableCrate");
                if (spawnableCrateType == null)
                {
                    Main.MelonLog.Error("SpawnableCrate type not found!");
                    return;
                }

                // Call GetCrates<SpawnableCrate>(null) using reflection
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

                // crates is Il2CppSystem.Collections.Generic.List<SpawnableCrate>
                // We need to iterate through it
                var cratesType = crates.GetType();
                var countProp = cratesType.GetProperty("Count");
                var itemProp = cratesType.GetProperty("Item");

                if (countProp == null || itemProp == null)
                {
                    // Try using GetEnumerator instead
                    Main.MelonLog.Msg("Trying enumerator approach...");
                    TryEnumerateCrates(crates, spawnableCrateType);
                }
                else
                {
                    int count = (int)countProp.GetValue(crates);
                    Main.MelonLog.Msg($"Found {count} spawnable crates");

                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var crate = itemProp.GetValue(crates, new object[] { i });
                            if (crate != null)
                            {
                                AddCrateToList(crate);
                            }
                        }
                        catch (Exception ex)
                        {
                            Main.MelonLog.Warning($"Error accessing crate at index {i}: {ex.Message}");
                        }
                    }
                }

                // Sort items alphabetically
                _allItems = _allItems.OrderBy(x => x.Title).ToList();

                // Apply current filter
                FilterItems();

                Main.MelonLog.Msg($"Loaded {_allItems.Count} spawnable items");
                SendNotification(NotificationType.Success, $"Loaded {_allItems.Count} items");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to refresh item list: {ex.Message}\n{ex.StackTrace}");
                SendNotification(NotificationType.Error, "Failed to load items");
            }
        }

        private static void TryEnumerateCrates(object crates, Type spawnableCrateType)
        {
            try
            {
                var getEnumeratorMethod = crates.GetType().GetMethod("GetEnumerator");
                if (getEnumeratorMethod == null)
                {
                    Main.MelonLog.Error("GetEnumerator not found on crates list");
                    return;
                }

                var enumerator = getEnumeratorMethod.Invoke(crates, null);
                var enumeratorType = enumerator.GetType();
                var moveNextMethod = enumeratorType.GetMethod("MoveNext");
                var currentProp = enumeratorType.GetProperty("Current");

                if (moveNextMethod == null || currentProp == null)
                {
                    Main.MelonLog.Error("Enumerator methods not found");
                    return;
                }

                int count = 0;
                while ((bool)moveNextMethod.Invoke(enumerator, null))
                {
                    var crate = currentProp.GetValue(enumerator);
                    if (crate != null)
                    {
                        AddCrateToList(crate);
                        count++;
                    }
                }

                Main.MelonLog.Msg($"Enumerated {count} crates");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Enumerator approach failed: {ex.Message}");
            }
        }

        private static void AddCrateToList(object crate)
        {
            try
            {
                var crateType = crate.GetType();

                // Get Title from Scannable base class
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

                // Get Pallet name
                string palletName = null;
                var palletProp = crateType.GetProperty("Pallet", BindingFlags.Public | BindingFlags.Instance);
                if (palletProp != null)
                {
                    var pallet = palletProp.GetValue(crate);
                    if (pallet != null)
                    {
                        var palletTitleProp = pallet.GetType().GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                        if (palletTitleProp != null)
                        {
                            var palletTitleObj = palletTitleProp.GetValue(pallet);
                            palletName = palletTitleObj?.ToString();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(barcodeId))
                {
                    // Use title, or extract from barcode if no title
                    if (string.IsNullOrEmpty(title))
                    {
                        int lastDot = barcodeId.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot + 1 < barcodeId.Length)
                        {
                            title = barcodeId.Substring(lastDot + 1);
                            // Add spaces between camelCase
                            title = System.Text.RegularExpressions.Regex.Replace(title, "([a-z])([A-Z])", "$1 $2");
                        }
                        else
                        {
                            title = barcodeId;
                        }
                    }

                    _allItems.Add(new SpawnableItem
                    {
                        Title = StripRichText(title),
                        BarcodeID = barcodeId,
                        PalletName = StripRichText(palletName) ?? "Unknown"
                    });
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to add crate: {ex.Message}");
            }
        }

        private static void FilterItems()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredItems = new List<SpawnableItem>(_allItems);
            }
            else
            {
                string query = _searchQuery.ToLowerInvariant();
                _filteredItems = _allItems
                    .Where(item =>
                        item.Title.ToLowerInvariant().Contains(query) ||
                        item.BarcodeID.ToLowerInvariant().Contains(query) ||
                        item.PalletName.ToLowerInvariant().Contains(query))
                    .ToList();
            }

            // Reset selection if out of bounds
            if (_selectedIndex >= _filteredItems.Count)
            {
                _selectedIndex = 0;
            }

            // Update selected item
            if (_filteredItems.Count > 0 && _selectedIndex < _filteredItems.Count)
            {
                _selectedItem = _filteredItems[_selectedIndex];
            }
            else
            {
                _selectedItem = default;
            }
        }

        public static void NextPage()
        {
            if (_currentPage < TotalPages - 1)
            {
                _currentPage++;
                _selectedIndex = _currentPage * _itemsPerPage;
                UpdateSelectedItem();
                Main.MelonLog.Msg($"Page {_currentPage + 1}/{TotalPages}");
            }
        }

        public static void PreviousPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                _selectedIndex = _currentPage * _itemsPerPage;
                UpdateSelectedItem();
                Main.MelonLog.Msg($"Page {_currentPage + 1}/{TotalPages}");
            }
        }

        public static void NextItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }

            _selectedIndex = (_selectedIndex + 1) % _filteredItems.Count;
            _currentPage = _selectedIndex / _itemsPerPage;
            UpdateSelectedItem();
            SendNotification(NotificationType.Information, $"{_selectedItem.Title} [{_selectedIndex + 1}/{_filteredItems.Count}]");
        }

        public static void PreviousItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }

            _selectedIndex = (_selectedIndex - 1 + _filteredItems.Count) % _filteredItems.Count;
            _currentPage = _selectedIndex / _itemsPerPage;
            UpdateSelectedItem();
            SendNotification(NotificationType.Information, $"{_selectedItem.Title} [{_selectedIndex + 1}/{_filteredItems.Count}]");
        }

        private static void UpdateSelectedItem()
        {
            if (_filteredItems.Count > 0 && _selectedIndex < _filteredItems.Count)
            {
                _selectedItem = _filteredItems[_selectedIndex];
                Main.MelonLog.Msg($"Selected: {_selectedItem.Title}");
            }
        }

        public static List<SpawnableItem> GetCurrentPageItems()
        {
            int startIndex = _currentPage * _itemsPerPage;
            int count = Math.Min(_itemsPerPage, _filteredItems.Count - startIndex);

            if (startIndex >= _filteredItems.Count)
                return new List<SpawnableItem>();

            return _filteredItems.GetRange(startIndex, count);
        }

        public static string GetSelectedItemName()
        {
            if (!_selectedItem.IsValid)
                return "(None)";
            return $"{_selectedItem.Title} [{_selectedIndex + 1}/{_filteredItems.Count}]";
        }

        public static string GetPageInfo()
        {
            return $"Page {_currentPage + 1}/{TotalPages} ({_filteredItems.Count} items)";
        }

        public static void SpawnSelectedItem()
        {
            if (!_selectedItem.IsValid)
            {
                Main.MelonLog.Warning("No item selected");
                SendNotification(NotificationType.Warning, "No item selected");
                return;
            }

            SpawnItem(_selectedItem.BarcodeID, _selectedItem.Title);
        }

        public static void SpawnItem(string barcodeId, string itemName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(barcodeId))
                {
                    Main.MelonLog.Warning("Invalid barcode ID");
                    return;
                }

                // Get spawn position in front of player's head
                Vector3 spawnPos;
                Quaternion spawnRot = Quaternion.identity;

                var playerHead = Player.Head;
                if (playerHead != null)
                {
                    spawnPos = playerHead.position + playerHead.forward * _spawnDistance;
                }
                else
                {
                    var rigManager = Player.RigManager;
                    if (rigManager != null)
                    {
                        spawnPos = rigManager.transform.position + Vector3.forward * _spawnDistance + Vector3.up * 1f;
                    }
                    else
                    {
                        Main.MelonLog.Error("Cannot determine spawn position - no player reference");
                        return;
                    }
                }

                // Try LabFusion's NetworkAssetSpawner first (for multiplayer sync)
                bool spawned = TryNetworkedSpawn(barcodeId, spawnPos, spawnRot);

                if (!spawned)
                {
                    // Fallback to BoneLib spawn
                    spawned = TryBoneLibSpawn(barcodeId, spawnPos, spawnRot);
                }

                if (spawned)
                {
                    string name = itemName ?? barcodeId;
                    Main.MelonLog.Msg($"Spawned: {name}");
                    SendNotification(NotificationType.Success, $"Spawned: {name}");
                }
                else
                {
                    Main.MelonLog.Warning($"Failed to spawn: {barcodeId}");
                    SendNotification(NotificationType.Error, "Spawn failed");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Spawn error: {ex.Message}");
                SendNotification(NotificationType.Error, "Spawn error");
            }
        }

        private static bool TryNetworkedSpawn(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                // Find required LabFusion types
                var networkAssetSpawnerType = FindTypeByName("NetworkAssetSpawner");
                var spawnableType = FindTypeByName("Spawnable", "LabFusion");
                var spawnRequestType = FindTypeByName("SpawnRequestInfo");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                {
                    return false;
                }

                // Create SpawnableCrateReference
                var crateRefType = FindTypeByName("SpawnableCrateReference");
                if (crateRefType == null) return false;

                var crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null) return false;

                var crateRef = crateCtor.Invoke(new object[] { barcode });

                // Create Spawnable and set crateRef
                var spawnable = Activator.CreateInstance(spawnableType);
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi)
                    fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi)
                    pi.SetValue(spawnable, crateRef);

                // Create SpawnRequestInfo
                var spawnReq = Activator.CreateInstance(spawnRequestType);

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

                // Find and invoke Spawn method
                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null) return false;

                spawnMethod.Invoke(null, new object[] { spawnReq });
                return true;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"NetworkAssetSpawner failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryBoneLibSpawn(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                var helperMethodsType = FindTypeByName("HelperMethods", "BoneLib");
                if (helperMethodsType == null) return false;

                var spawnMethods = helperMethodsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "SpawnCrate")
                    .ToArray();

                foreach (var method in spawnMethods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length >= 2)
                    {
                        var firstParamType = parameters[0].ParameterType;

                        if (firstParamType == typeof(string))
                        {
                            // Build argument list
                            var args = new List<object>();
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var p = parameters[i];
                                if (i == 0) args.Add(barcode);
                                else if (p.ParameterType == typeof(Vector3) && i == 1) args.Add(position);
                                else if (p.ParameterType == typeof(Quaternion)) args.Add(rotation);
                                else if (p.ParameterType == typeof(Vector3)) args.Add(Vector3.one);
                                else if (p.ParameterType == typeof(bool)) args.Add(true);
                                else args.Add(null);
                            }

                            method.Invoke(null, args.ToArray());
                            return true;
                        }
                        else if (firstParamType.Name.Contains("SpawnableCrateReference"))
                        {
                            var ctor = firstParamType.GetConstructor(new[] { typeof(string) });
                            if (ctor != null)
                            {
                                var crateRef = ctor.Invoke(new object[] { barcode });

                                var args = new List<object>();
                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    var p = parameters[i];
                                    if (i == 0) args.Add(crateRef);
                                    else if (p.ParameterType == typeof(Vector3) && i == 1) args.Add(position);
                                    else if (p.ParameterType == typeof(Quaternion)) args.Add(rotation);
                                    else if (p.ParameterType == typeof(Vector3)) args.Add(Vector3.one);
                                    else if (p.ParameterType == typeof(bool)) args.Add(true);
                                    else args.Add(null);
                                }

                                method.Invoke(null, args.ToArray());
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"BoneLib spawn failed: {ex.Message}");
                return false;
            }
        }

        private static Type FindTypeByName(string typeName, string preferredAssembly = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(preferredAssembly))
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == preferredAssembly);
                    if (assembly != null)
                    {
                        var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                        if (type != null) return type;
                    }
                }

                // Search all assemblies
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
