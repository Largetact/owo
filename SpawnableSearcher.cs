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
using System.Collections;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Spawnable Searcher - Direct port from FusionProtector's approach
    /// Uses AssetWarehouse.Instance.GetPallets() directly
    /// Favorites are persisted via MelonPreferences.
    /// </summary>
    public static class SpawnableSearcher
    {
        public enum SearchMethod
        {
            CrateNames,
            BarcodeIDNames,
            PalletName,
            PalletAuthor
        }

        public enum SpawnableSearchType
        {
            Spawn,
            CopyDetailsToClipboard,
            SetInLauncher,
            SetToFavorite,
            SetToCustomPunch
        }

        // State
        private static bool _isSearching = false;
        private static string _searchQuery = "";
        private static SearchMethod _searchMethod = SearchMethod.CrateNames;
        private static SpawnableSearchType _spawnType = SpawnableSearchType.Spawn;
        private static bool _excludeAmmo = true;

        // Results page reference
        private static Page _resultsPage = null;
        private static int _resultCount = 0;
        private static Action<string> _customAction = null;

        // Search history - tracks selected items
        private struct HistoryEntry
        {
            public string DisplayName;
            public string Barcode;
        }
        private static List<HistoryEntry> _searchHistory = new List<HistoryEntry>();
        private static int _maxHistorySize = 50;

        // Favorites - user-marked items
        private struct FavoriteEntry
        {
            public string DisplayName;
            public string Barcode;
        }
        private static List<FavoriteEntry> _favorites = new List<FavoriteEntry>();
        private static int _maxFavoritesSize = 100;

        public static bool IsSearching => _isSearching;
        public static int ResultCount => _resultCount;

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static SearchMethod CurrentSearchMethod
        {
            get => _searchMethod;
            set => _searchMethod = value;
        }

        public static SpawnableSearchType CurrentSpawnType
        {
            get => _spawnType;
            set => _spawnType = value;
        }

        public static bool ExcludeAmmo
        {
            get => _excludeAmmo;
            set => _excludeAmmo = value;
        }

        /// <summary>
        /// Set the results page and start a search
        /// </summary>
        public static void SearchToPage(Page resultsPage)
        {
            _resultsPage = resultsPage;
            _customAction = null;
            MelonCoroutines.Start(SearchCoroutine());
        }

        /// <summary>
        /// Search and populate results page, calling a custom action on item click instead of OnItemSelected
        /// </summary>
        public static void SearchToPageWithAction(Page resultsPage, Action<string> action)
        {
            _resultsPage = resultsPage;
            _customAction = action;
            MelonCoroutines.Start(SearchCoroutine());
        }

        /// <summary>
        /// Direct search without page - just start the coroutine
        /// </summary>
        public static void Search()
        {
            MelonCoroutines.Start(SearchCoroutine());
        }

        /// <summary>
        /// Check if a crate is a SpawnableCrate
        /// </summary>
        private static bool IsSpawnableCrate(Crate crate)
        {
            if (crate == null) return false;

            try
            {
                // Try to cast to SpawnableCrate
                var spawnableCrate = crate.TryCast<SpawnableCrate>();
                return spawnableCrate != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Main search coroutine - directly ported from FusionProtector
        /// </summary>
        private static IEnumerator SearchCoroutine()
        {
            if (_isSearching)
            {
                SendNotification(NotificationType.Warning, "[Searcher] Already running, please wait!");
                yield break;
            }

            _isSearching = true;
            _resultCount = 0;

            // Clear results page safely
            if (_resultsPage != null)
            {
                try { _resultsPage.RemoveAll(); } catch { }
            }

            string searchLower = (_searchQuery ?? "").ToLower();
            Main.MelonLog.Msg($"[Searcher] Starting search for '{searchLower}' using {_searchMethod}");

            int batchSize = 100;
            int processed = 0;

            // Get all pallets from AssetWarehouse
            Il2CppSystem.Collections.Generic.List<Pallet> pallets = null;
            try
            {
                pallets = AssetWarehouse.Instance.GetPallets();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[Searcher] Failed to get pallets: {ex.Message}");
            }

            if (pallets == null)
            {
                Main.MelonLog.Warning("[Searcher] No pallets found");
                _isSearching = false;
                yield break;
            }

            foreach (var pallet in pallets)
            {
                if (pallet == null) continue;

                string palletName = "";
                string palletAuthor = "";

                try
                {
                    palletName = pallet.name ?? "";
                    palletAuthor = pallet.Author ?? "";
                }
                catch { continue; }

                // PalletName search - create subpage for matching pallets
                if (_searchMethod == SearchMethod.PalletName && palletName.ToLower().Contains(searchLower))
                {
                    ProcessPalletForSearch(pallet, palletName, "");
                }

                // PalletAuthor search - create subpage for matching authors
                if (_searchMethod == SearchMethod.PalletAuthor && !string.IsNullOrEmpty(palletAuthor) && palletAuthor.ToLower().Contains(searchLower))
                {
                    ProcessPalletForSearch(pallet, palletName, palletAuthor);
                }

                // CrateName and BarcodeIDNames search
                if (_searchMethod == SearchMethod.CrateNames || _searchMethod == SearchMethod.BarcodeIDNames)
                {
                    Il2CppSystem.Collections.Generic.List<Crate> crates = null;
                    try { crates = pallet.Crates; } catch { continue; }

                    if (crates != null)
                    {
                        foreach (var crate in crates)
                        {
                            processed++;

                            if (crate == null) continue;
                            if (!IsSpawnableCrate(crate)) continue;

                            try
                            {
                                if (_excludeAmmo && !ExcludeThis(crate)) continue;

                                string crateName = (crate.name ?? "").ToLower();
                                string crateTitle = "";
                                try { crateTitle = (crate.Title ?? "").ToLower(); } catch { }
                                string barcodeId = crate.Barcode?.ID ?? "";
                                string barcodeLower = barcodeId.ToLower();
                                bool isRedacted = false;
                                try { isRedacted = crate.Redacted; } catch { }

                                bool match = false;

                                if (_searchMethod == SearchMethod.CrateNames)
                                {
                                    match = crateName.Contains(searchLower) || crateName.StartsWith(searchLower)
                                        || (!string.IsNullOrEmpty(crateTitle) && crateTitle.Contains(searchLower));
                                }
                                else if (_searchMethod == SearchMethod.BarcodeIDNames)
                                {
                                    match = barcodeLower.Contains(searchLower) || barcodeLower.StartsWith(searchLower);
                                }

                                if (match)
                                {
                                    string displayName = crate.name ?? barcodeId;
                                    if (isRedacted) displayName = "[R] " + displayName;
                                    string capturedBarcode = barcodeId;
                                    _resultsPage?.CreateFunction(displayName, isRedacted ? Color.yellow : Color.green, () =>
                                    {
                                        if (_customAction != null)
                                            _customAction(capturedBarcode);
                                        else
                                            OnItemSelected(capturedBarcode);
                                    });
                                    _resultCount++;
                                }
                            }
                            catch { }
                        }
                    }
                }

                // Yield periodically to prevent frame lag
                if (processed >= batchSize)
                {
                    processed = 0;
                    yield return null;
                }
            }

            Main.MelonLog.Msg($"[Searcher] Completed! Found {_resultCount} Results.");
            SendNotification(NotificationType.Success, $"[Searcher] Found {_resultCount} Results.");
            _isSearching = false;
        }

        /// <summary>
        /// Process a pallet for PalletName/PalletAuthor searches
        /// </summary>
        private static void ProcessPalletForSearch(Pallet pallet, string palletName, string palletAuthor)
        {
            try
            {
                string pageTitle = string.IsNullOrEmpty(palletAuthor)
                    ? "+ " + palletName
                    : $"+ {palletName} ({palletAuthor})";

                Page palletPage = _resultsPage?.CreatePage(pageTitle, Color.green);

                var crates = pallet.Crates;
                if (crates == null) return;

                foreach (var crate in crates)
                {
                    if (crate == null) continue;
                    if (!IsSpawnableCrate(crate)) continue;

                    if (_excludeAmmo && !ExcludeThis(crate)) continue;

                    string crateName = crate.name ?? "";
                    string barcodeId = crate.Barcode?.ID ?? "";
                    string capturedBarcode = barcodeId;
                    bool isRedacted = false;
                    try { isRedacted = crate.Redacted; } catch { }
                    string displayName = isRedacted ? "[R] " + crateName : crateName;

                    palletPage?.CreateFunction(displayName, isRedacted ? Color.yellow : Color.green, () =>
                    {
                        if (_customAction != null)
                            _customAction(capturedBarcode);
                        else
                            OnItemSelected(capturedBarcode);
                    });
                    _resultCount++;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Searcher] Process pallet error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when an item is selected from the results
        /// </summary>
        private static void OnItemSelected(string barcode)
        {
            // Add to history
            string displayName = ResolveDisplayName(barcode);

            // Check if already in history to avoid duplicates
            bool exists = false;
            foreach (var entry in _searchHistory)
            {
                if (entry.Barcode == barcode)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                _searchHistory.Insert(0, new HistoryEntry { DisplayName = displayName, Barcode = barcode });
                if (_searchHistory.Count > _maxHistorySize)
                {
                    _searchHistory.RemoveAt(_searchHistory.Count - 1);
                }
            }

            switch (_spawnType)
            {
                case SpawnableSearchType.Spawn:
                    SpawnItem(barcode);
                    break;

                case SpawnableSearchType.CopyDetailsToClipboard:
                    UnityEngine.GUIUtility.systemCopyBuffer = barcode;
                    SendNotification(NotificationType.Success, $"Copied: {barcode}");
                    Main.MelonLog.Msg($"Copied barcode: {barcode}");
                    break;

                case SpawnableSearchType.SetInLauncher:
                    ObjectLauncherController.CurrentBarcodeID = barcode;
                    ObjectLauncherController.CurrentItemName = displayName;
                    SendNotification(NotificationType.Success, $"Set in launcher: {displayName}");
                    Main.MelonLog.Msg($"Set in launcher: {displayName} ({barcode})");
                    break;

                case SpawnableSearchType.SetToFavorite:
                    ToggleFavorite(barcode, displayName);
                    break;

                case SpawnableSearchType.SetToCustomPunch:
                    ExplosivePunchController.CustomPunchBarcode = barcode;
                    SettingsManager.MarkDirty();
                    SendNotification(NotificationType.Success, $"Custom Punch set: {displayName}");
                    Main.MelonLog.Msg($"Custom Punch barcode set: {barcode}");
                    break;
            }
        }

        /// <summary>
        /// Resolve a barcode to a display name
        /// </summary>
        private static string ResolveDisplayName(string barcode)
        {
            string displayName = barcode;
            try
            {
                var warehouse = AssetWarehouse.Instance;
                if (warehouse != null)
                {
                    var pallets = warehouse.GetPallets();
                    if (pallets != null)
                    {
                        foreach (var pallet in pallets)
                        {
                            if (pallet == null) continue;
                            var crates = pallet.Crates;
                            if (crates == null) continue;
                            foreach (var crate in crates)
                            {
                                if (crate == null) continue;
                                string bid = crate.Barcode?.ID ?? "";
                                if (bid == barcode)
                                {
                                    displayName = crate.name ?? barcode;
                                    break;
                                }
                            }
                            if (displayName != barcode) break;
                        }
                    }
                }
            }
            catch { }
            return displayName;
        }

        /// <summary>
        /// Toggle an item as favorite. If already favorite, remove it; otherwise add it.
        /// </summary>
        public static void ToggleFavorite(string barcode, string displayName = null)
        {
            // Check if already in favorites
            for (int i = 0; i < _favorites.Count; i++)
            {
                if (_favorites[i].Barcode == barcode)
                {
                    _favorites.RemoveAt(i);
                    SendNotification(NotificationType.Success, $"Unfavorited: {displayName ?? barcode}");
                    SaveFavorites();
                    return;
                }
            }

            // Not in favorites - add it
            if (string.IsNullOrEmpty(displayName) || displayName == barcode)
            {
                displayName = ResolveDisplayName(barcode);
            }
            _favorites.Add(new FavoriteEntry { DisplayName = displayName, Barcode = barcode });
            if (_favorites.Count > _maxFavoritesSize)
            {
                _favorites.RemoveAt(0); // Remove oldest
            }
            SendNotification(NotificationType.Success, $"Favorited: {displayName}");
            SaveFavorites();
        }

        /// <summary>
        /// Favorite the item currently held in either hand.
        /// Checks both hands, extracts barcode via Poolee → SpawnableCrate → Barcode.ID.
        /// </summary>
        public static void FavoriteItemFromHand()
        {
            try
            {
                string barcode = null;
                string itemName = null;

                // Try both hands
                barcode = TryGetHeldItemBarcode(true, out itemName);
                if (string.IsNullOrEmpty(barcode))
                {
                    barcode = TryGetHeldItemBarcode(false, out itemName);
                }

                if (string.IsNullOrEmpty(barcode))
                {
                    SendNotification(NotificationType.Warning, "No spawnable item in either hand");
                    return;
                }

                ToggleFavorite(barcode, itemName);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"FavoriteItemFromHand error: {ex.Message}");
                SendNotification(NotificationType.Error, "Failed to read held item");
            }
        }

        /// <summary>
        /// Try to get the barcode of item held in a specific hand via Poolee → SpawnableCrate → Barcode.ID.
        /// </summary>
        private static string TryGetHeldItemBarcode(bool leftHand, out string displayName)
        {
            displayName = null;
            try
            {
                var hand = leftHand ? Player.LeftHand : Player.RightHand;
                if (hand == null) return null;

                // Find Poolee type
                Type pooleeType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "Poolee")
                            {
                                pooleeType = t;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (pooleeType != null) break;
                }
                if (pooleeType == null) return null;

                // Player.GetComponentInHand<Poolee>(hand)
                var playerType = typeof(Player);
                var getCompMethod = playerType.GetMethod("GetComponentInHand",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getCompMethod == null) return null;

                var genericMethod = getCompMethod.MakeGenericMethod(pooleeType);
                var poolee = genericMethod.Invoke(null, new object[] { hand });
                if (poolee == null) return null;

                // Poolee.SpawnableCrate
                var crateProp = pooleeType.GetProperty("SpawnableCrate",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (crateProp == null) return null;

                var crate = crateProp.GetValue(poolee);
                if (crate == null) return null;

                // Crate.Barcode.ID
                var crateType = crate.GetType();
                var barcodeProp = crateType.GetProperty("Barcode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (barcodeProp == null) return null;

                var barcodeObj = barcodeProp.GetValue(crate);
                if (barcodeObj == null) return null;

                var idProp = barcodeObj.GetType().GetProperty("ID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (idProp == null) return null;

                string barcode = idProp.GetValue(barcodeObj) as string;
                if (string.IsNullOrEmpty(barcode)) return null;

                // Try to get display name
                try
                {
                    var titleProp = crateType.GetProperty("Title",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (titleProp != null)
                    {
                        var titleVal = titleProp.GetValue(crate);
                        if (titleVal != null) displayName = titleVal.ToString();
                    }
                    if (string.IsNullOrEmpty(displayName))
                    {
                        var nameProp = crateType.GetProperty("name",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            var nameVal = nameProp.GetValue(crate);
                            if (nameVal != null) displayName = nameVal.ToString();
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(displayName)) displayName = barcode;
                return barcode;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Populate a BoneMenu page with favorited items.
        /// Favorites are loaded from MelonPreferences on startup and saved on change.
        /// </summary>
        public static void PopulateFavoritesPage(Page favoritesPage)
        {
            if (favoritesPage == null) return;

            try { favoritesPage.RemoveAll(); } catch { }

            if (_favorites.Count == 0)
            {
                favoritesPage.CreateFunction("No favorites yet - Use 'Toggle Favorite' on items", Color.gray, () => { });
                return;
            }

            foreach (var entry in _favorites)
            {
                string capturedBarcode = entry.Barcode;
                string capturedName = entry.DisplayName;

                favoritesPage.CreateFunction($"★ {capturedName}", Color.yellow, () =>
                {
                    // Execute the current spawn type action
                    switch (_spawnType)
                    {
                        case SpawnableSearchType.Spawn:
                            SpawnItem(capturedBarcode);
                            break;
                        case SpawnableSearchType.CopyDetailsToClipboard:
                            UnityEngine.GUIUtility.systemCopyBuffer = capturedBarcode;
                            SendNotification(NotificationType.Success, $"Copied: {capturedBarcode}");
                            break;
                        case SpawnableSearchType.SetInLauncher:
                            ObjectLauncherController.CurrentBarcodeID = capturedBarcode;
                            ObjectLauncherController.CurrentItemName = capturedName;
                            SendNotification(NotificationType.Success, $"Set in launcher: {capturedName}");
                            break;
                        case SpawnableSearchType.SetToCustomPunch:
                            ExplosivePunchController.CustomPunchBarcode = capturedBarcode;
                            SettingsManager.MarkDirty();
                            SendNotification(NotificationType.Success, $"Custom Punch set: {capturedBarcode}");
                            break;
                    }
                });
            }

            // Unfavorite button
            favoritesPage.CreateFunction("Remove Last Favorite", Color.red, () =>
            {
                if (_favorites.Count > 0)
                {
                    string name = _favorites[_favorites.Count - 1].DisplayName;
                    _favorites.RemoveAt(_favorites.Count - 1);
                    SaveFavorites();
                    SendNotification(NotificationType.Success, $"Removed: {name}");
                }
            });

            favoritesPage.CreateFunction("Clear All Favorites", Color.red, () =>
            {
                _favorites.Clear();
                SaveFavorites();
                SendNotification(NotificationType.Success, "All favorites cleared");
                try { favoritesPage.RemoveAll(); } catch { }
            });

            SendNotification(NotificationType.Success, $"{_favorites.Count} favorites");
        }

        /// <summary>
        /// Serialize favorites to a pipe-delimited string for MelonPreferences storage.
        /// Format: "barcode::name|||barcode2::name2|||..."
        /// </summary>
        public static string SerializeFavorites()
        {
            var parts = new List<string>();
            foreach (var fav in _favorites)
            {
                parts.Add($"{fav.Barcode}::{fav.DisplayName}");
            }
            return string.Join("|||", parts);
        }

        /// <summary>
        /// Deserialize favorites from MelonPreferences stored string.
        /// </summary>
        public static void DeserializeFavorites(string data)
        {
            _favorites.Clear();
            if (string.IsNullOrEmpty(data)) return;

            var entries = data.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split(new[] { "::" }, 2, StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    _favorites.Add(new FavoriteEntry
                    {
                        Barcode = parts[0],
                        DisplayName = parts[1]
                    });
                }
            }
            Main.MelonLog.Msg($"Loaded {_favorites.Count} favorites from preferences");
        }

        /// <summary>
        /// Save favorites via SettingsManager
        /// </summary>
        private static void SaveFavorites()
        {
            SettingsManager.MarkDirty();
        }

        /// <summary>
        /// Spawn an item in front of the player's head.
        /// Uses network spawn in multiplayer, falls back to local spawn in singleplayer.
        /// </summary>
        public static void SpawnItem(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
            {
                SendNotification(NotificationType.Warning, "No barcode specified");
                return;
            }

            try
            {
                var head = Player.Head;
                if (head == null)
                {
                    SendNotification(NotificationType.Error, "Cannot access player head");
                    return;
                }

                Vector3 spawnPos = head.position + head.forward * 1.5f;
                Quaternion rotation = Quaternion.identity;

                // Check if we're in a multiplayer server before trying network spawn
                bool inServer = IsInMultiplayerServer();

                if (inServer)
                {
                    bool networkSpawned = false;
                    try
                    {
                        networkSpawned = TryNetworkSpawn(barcode, spawnPos, rotation);
                    }
                    catch { }

                    if (networkSpawned)
                    {
                        SendNotification(NotificationType.Success, "Spawned (networked)");
                        return;
                    }

                    Main.MelonLog.Msg("[Searcher] Network spawn failed, falling back to local");
                }

                // Local spawn (singleplayer or network spawn failed)
                var spawnable = new Spawnable
                {
                    crateRef = new SpawnableCrateReference(barcode)
                };
                SpawnLocal(spawnable, spawnPos, rotation);
                SendNotification(NotificationType.Success, "Spawned (local)");
                Main.MelonLog.Msg($"Spawned (local): {barcode}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Spawn failed: {ex.Message}");
                SendNotification(NotificationType.Error, "Spawn failed");
            }
        }

        /// <summary>
        /// Check if we are currently in a multiplayer server (Fusion is loaded and has an active session).
        /// Returns false if Fusion DLL is not present or no active server.
        /// </summary>
        private static bool IsInMultiplayerServer()
        {
            try
            {
                // Check if LabFusion assembly is loaded at all
                var networkInfoType = FindTypeByName("NetworkInfo");
                if (networkInfoType == null) return false;

                var hasServerProp = networkInfoType.GetProperty("HasServer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (hasServerProp == null) return false;

                return (bool)hasServerProp.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Spawn locally using AssetSpawner from pool
        /// </summary>
        private static void SpawnLocal(Spawnable spawnable, Vector3 position, Quaternion rotation)
        {
            try
            {
                // Use AssetSpawner from Il2CppSLZ.Marrow.Pool
                AssetSpawner.Register(spawnable);

                // Use SpawnAsync with minimal parameters
                var task = AssetSpawner.SpawnAsync(spawnable, position, rotation,
                    default, null, false, default, null, null, null);
                // Fire and forget - we don't need the result
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Local spawn error: {ex.Message}");

                // Fallback: direct pool spawn
                try
                {
                    var crateRef = spawnable.crateRef;
                    if (crateRef != null && crateRef.Crate != null)
                    {
                        var mainAsset = crateRef.Crate.MainAsset;
                        if (mainAsset != null)
                        {
                            var go = UnityEngine.Object.Instantiate(mainAsset.Asset.Cast<GameObject>(), position, rotation);
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Main.MelonLog.Error($"Fallback spawn error: {ex2.Message}");
                }
            }
        }

        /// <summary>
        /// Try to spawn using LabFusion's NetworkAssetSpawner
        /// </summary>
        private static bool TryNetworkSpawn(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                // Check if we're in a network session using reflection
                var networkInfoType = FindTypeByName("NetworkInfo");
                if (networkInfoType == null) return false;

                var hasServerProp = networkInfoType.GetProperty("HasServer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (hasServerProp == null) return false;

                bool hasServer = (bool)hasServerProp.GetValue(null);
                if (!hasServer) return false;

                // Get NetworkAssetSpawner.Spawn
                var networkAssetSpawnerType = FindTypeByName("NetworkAssetSpawner");
                if (networkAssetSpawnerType == null) return false;

                var spawnRequestInfoType = FindTypeByName("SpawnRequestInfo");
                if (spawnRequestInfoType == null) return false;

                // Create SpawnRequestInfo
                var spawnRequest = Activator.CreateInstance(spawnRequestInfoType);

                // Create Spawnable
                var spawnable = new Spawnable
                {
                    crateRef = new SpawnableCrateReference(barcode)
                };

                // Set Spawnable field
                var spawnableField = spawnRequestInfoType.GetField("Spawnable");
                spawnableField?.SetValue(spawnRequest, spawnable);

                // Set Position
                var positionField = spawnRequestInfoType.GetField("Position");
                positionField?.SetValue(spawnRequest, position);

                // Set Rotation
                var rotationField = spawnRequestInfoType.GetField("Rotation");
                rotationField?.SetValue(spawnRequest, rotation);

                // Call Spawn
                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                spawnMethod?.Invoke(null, new object[] { spawnRequest });

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a crate should be excluded (is a magazine/cartridge)
        /// Returns true if item should be INCLUDED, false if excluded
        /// Direct port from FusionProtector's ExcludeThis
        /// </summary>
        private static bool ExcludeThis(Crate crate)
        {
            if (crate == null) return false;

            string name = crate.name ?? "";
            string barcodeId = crate.Barcode?.ID ?? "";
            var tags = crate.Tags;

            bool isMagOrCartridge =
                name.Contains(" Mag ") ||
                name.EndsWith(" MAG") ||
                name.EndsWith(" Mag") ||
                name.EndsWith("_MAG") ||
                name.StartsWith("Mag ") ||
                name.StartsWith("Mag_") ||
                name.StartsWith("MAG_") ||
                name.Contains("Magazine") ||
                name.StartsWith("Cartridge") ||
                name.Contains("Cartridge") ||
                name.Contains("cartridge") ||
                barcodeId.EndsWith("Mag") ||
                barcodeId.EndsWith("Cartridge") ||
                barcodeId.EndsWith("cartridge") ||
                barcodeId.StartsWith("Cartridge") ||
                barcodeId.StartsWith("cartridge") ||
                barcodeId.Contains("Cartridge") ||
                barcodeId.Contains("cartridge");

            // Check tags
            if (tags != null)
            {
                try
                {
                    foreach (var tag in tags)
                    {
                        string tagStr = tag ?? "";
                        if (tagStr == "Mag" || tagStr == "Magazine" || tagStr == "Magazines" ||
                            tagStr == "Cartridge" || tagStr == "cartridge")
                        {
                            isMagOrCartridge = true;
                            break;
                        }
                    }
                }
                catch { }
            }

            return !isMagOrCartridge; // Return true to INCLUDE (i.e., NOT a mag/cartridge)
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

        /// <summary>
        /// Populate a BoneMenu page with search history (previously selected items)
        /// </summary>
        public static void PopulateHistoryPage(Page historyPage)
        {
            if (historyPage == null) return;

            try { historyPage.RemoveAll(); } catch { }

            if (_searchHistory.Count == 0)
            {
                historyPage.CreateFunction("No history yet - Select items first", Color.gray, () => { });
                return;
            }

            foreach (var entry in _searchHistory)
            {
                string capturedBarcode = entry.Barcode;
                string capturedName = entry.DisplayName;

                historyPage.CreateFunction(capturedName, Color.green, () =>
                {
                    OnItemSelected(capturedBarcode);
                });
            }

            historyPage.CreateFunction("Clear History", Color.red, () =>
            {
                _searchHistory.Clear();
                SendNotification(NotificationType.Success, "History cleared");
                try { historyPage.RemoveAll(); } catch { }
            });

            SendNotification(NotificationType.Success, $"{_searchHistory.Count} items in history");
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}
