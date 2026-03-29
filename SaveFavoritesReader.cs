using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Reads the player's favorited spawnables from the BONELAB save file.
    /// Save files are located at Application.persistentDataPath/Saves/
    /// and contain player_settings.favorite_spawnables as a JSON array of barcode strings.
    /// </summary>
    public static class SaveFavoritesReader
    {
        // Cached favorites from save file
        private struct SaveFavoriteEntry
        {
            public string Barcode;
            public string DisplayName;
        }

        private static List<SaveFavoriteEntry> _gameFavorites = new List<SaveFavoriteEntry>();
        private static bool _loaded = false;
        private static string _lastLoadedFile = "";

        /// <summary>Number of favorites currently loaded from the save file.</summary>
        public static int Count => _gameFavorites.Count;

        /// <summary>Whether favorites have been loaded at least once.</summary>
        public static bool IsLoaded => _loaded;

        /// <summary>
        /// Get the BONELAB saves directory path.
        /// Uses Application.persistentDataPath which resolves to
        /// %USERPROFILE%\AppData\LocalLow\Stress Level Zero\BONELAB
        /// </summary>
        private static string GetSavesDirectory()
        {
            string basePath = Application.persistentDataPath;
            return Path.Combine(basePath, "Saves");
        }

        /// <summary>
        /// Find the most recent timestamped save file.
        /// Pattern: save_YYYYMMDD.HHMMSS.save.json
        /// Falls back to slot_*.save.json if no timestamped saves exist.
        /// </summary>
        private static string FindLatestSaveFile()
        {
            string savesDir = GetSavesDirectory();

            if (!Directory.Exists(savesDir))
            {
                Main.MelonLog.Warning($"[SaveFavoritesReader] Saves directory not found: {savesDir}");
                return null;
            }

            // Look for timestamped saves first (most recent)
            var timestampedSaves = Directory.GetFiles(savesDir, "save_*.save.json")
                .OrderByDescending(f => Path.GetFileName(f))
                .ToArray();

            if (timestampedSaves.Length > 0)
            {
                return timestampedSaves[0];
            }

            // Fallback to slot saves
            var slotSaves = Directory.GetFiles(savesDir, "slot_*.save.json")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();

            if (slotSaves.Length > 0)
            {
                return slotSaves[0];
            }

            Main.MelonLog.Warning("[SaveFavoritesReader] No save files found");
            return null;
        }

        /// <summary>
        /// Read favorite spawnable barcodes from a save file's JSON.
        /// Expects: { "player_settings": { "favorite_spawnables": ["barcode1", "barcode2", ...] } }
        /// </summary>
        private static List<string> ReadFavoriteBarcodesFromFile(string filePath)
        {
            var barcodes = new List<string>();

            try
            {
                string json = File.ReadAllText(filePath);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("player_settings", out var playerSettings))
                    {
                        if (playerSettings.TryGetProperty("favorite_spawnables", out var favorites))
                        {
                            if (favorites.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in favorites.EnumerateArray())
                                {
                                    string barcode = item.GetString();
                                    if (!string.IsNullOrEmpty(barcode))
                                    {
                                        barcodes.Add(barcode);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Main.MelonLog.Warning("[SaveFavoritesReader] 'favorite_spawnables' not found in player_settings");
                        }
                    }
                    else
                    {
                        Main.MelonLog.Warning("[SaveFavoritesReader] 'player_settings' not found in save file");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[SaveFavoritesReader] Failed to parse save file: {ex.Message}");
            }

            return barcodes;
        }

        /// <summary>
        /// Resolve a barcode to its display name using AssetWarehouse.
        /// Returns the barcode itself if resolution fails.
        /// </summary>
        private static string ResolveBarcodeName(string barcode)
        {
            try
            {
                var warehouse = AssetWarehouse.Instance;
                if (warehouse == null) return barcode;

                var pallets = warehouse.GetPallets();
                if (pallets == null) return barcode;

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
                            return crate.name ?? barcode;
                        }
                    }
                }
            }
            catch { }

            // If barcode can't be resolved, extract a readable name from the barcode itself
            // e.g. "Doctor.AdvancedUtilityGun.Spawnable.AUG" → "AUG"
            // e.g. "fa534c5a83ee4ec6bd641fec424c4142.Spawnable.VehicleGokart" → "VehicleGokart"
            return ExtractReadableName(barcode);
        }

        /// <summary>
        /// Extract a human-readable name from a barcode string.
        /// Takes the last segment after the final dot.
        /// </summary>
        private static string ExtractReadableName(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return barcode;

            // Split by dots and take the last meaningful segment
            var parts = barcode.Split('.');
            if (parts.Length >= 2)
            {
                // Usually format is: Author.ModName.Spawnable.ItemName
                // Return the last part as the readable name
                string last = parts[parts.Length - 1];
                if (!string.IsNullOrWhiteSpace(last))
                {
                    return last;
                }
            }

            return barcode;
        }

        /// <summary>
        /// Load favorites from the most recent save file.
        /// Call this to refresh the cached data.
        /// </summary>
        public static void LoadFromSaveFile()
        {
            _gameFavorites.Clear();
            _loaded = false;

            string saveFile = FindLatestSaveFile();
            if (saveFile == null)
            {
                Main.MelonLog.Msg("[SaveFavoritesReader] No save file found to load favorites from");
                return;
            }

            _lastLoadedFile = Path.GetFileName(saveFile);
            Main.MelonLog.Msg($"[SaveFavoritesReader] Loading favorites from: {_lastLoadedFile}");

            var barcodes = ReadFavoriteBarcodesFromFile(saveFile);

            if (barcodes.Count == 0)
            {
                Main.MelonLog.Msg("[SaveFavoritesReader] No favorited spawnables found in save file");
                _loaded = true;
                return;
            }

            // Resolve each barcode to a display name
            foreach (var barcode in barcodes)
            {
                string displayName = ResolveBarcodeName(barcode);
                _gameFavorites.Add(new SaveFavoriteEntry
                {
                    Barcode = barcode,
                    DisplayName = displayName
                });
            }

            _loaded = true;
            Main.MelonLog.Msg($"[SaveFavoritesReader] Loaded {_gameFavorites.Count} favorites from save file");
        }

        /// <summary>
        /// Populate a BoneMenu page with the game's saved favorites.
        /// Items can be spawned, copied, or set in launcher based on SpawnableSearcher's current spawn type.
        /// </summary>
        public static void PopulateGameFavoritesPage(Page page)
        {
            if (page == null) return;

            try { page.RemoveAll(); } catch { }

            // Auto-load if not yet loaded
            if (!_loaded)
            {
                LoadFromSaveFile();
            }

            if (_gameFavorites.Count == 0)
            {
                page.CreateFunction("No game favorites found in save file", Color.gray, () => { });
                page.CreateFunction("Refresh", Color.cyan, () =>
                {
                    LoadFromSaveFile();
                    PopulateGameFavoritesPage(page);
                });
                return;
            }

            // Info header
            page.CreateFunction($"{_gameFavorites.Count} Game Favorites ({_lastLoadedFile})", Color.gray, () => { });

            // List all favorites
            foreach (var entry in _gameFavorites)
            {
                string capturedBarcode = entry.Barcode;
                string capturedName = entry.DisplayName;

                page.CreateFunction($"★ {capturedName}", Color.yellow, () =>
                {
                    switch (SpawnableSearcher.CurrentSpawnType)
                    {
                        case SpawnableSearcher.SpawnableSearchType.Spawn:
                            SpawnableSearcher.SpawnItem(capturedBarcode);
                            break;
                        case SpawnableSearcher.SpawnableSearchType.CopyDetailsToClipboard:
                            GUIUtility.systemCopyBuffer = capturedBarcode;
                            SendNotification(NotificationType.Success, $"Copied: {capturedBarcode}");
                            break;
                        case SpawnableSearcher.SpawnableSearchType.SetInLauncher:
                            ObjectLauncherController.CurrentBarcodeID = capturedBarcode;
                            ObjectLauncherController.CurrentItemName = capturedName;
                            SendNotification(NotificationType.Success, $"Set in launcher: {capturedName}");
                            break;
                    }
                });
            }

            // Refresh button
            page.CreateFunction("Refresh from Save File", Color.cyan, () =>
            {
                LoadFromSaveFile();
                PopulateGameFavoritesPage(page);
            });

            SendNotification(NotificationType.Success, $"{_gameFavorites.Count} game favorites loaded");
        }

        /// <summary>
        /// Send a BoneLib notification.
        /// </summary>
        private static void SendNotification(NotificationType type, string message)
        {
            try
            {
                var notif = new Notification
                {
                    Title = "Save Favorites",
                    Message = message,
                    Type = type,
                    PopupLength = 2f,
                    ShowTitleOnPopup = true
                };
                Notifier.Send(notif);
            }
            catch { }
        }
    }
}
