using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using BoneLib.BoneMenu;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Map Change Controller — based on FusionProtector's level-loading approach.
    /// Uses direct Il2Cpp types (AssetWarehouse, LevelCrate, SceneStreamer) — NOT reflection.
    /// No permission checks. When you ARE the host, the level change syncs to all
    /// players via LabFusion. When you are a client, it loads the level locally.
    /// 
    /// NOTE: FusionProtector is NOT LabFusion. This code is modeled after FusionProtector's
    /// LevelSearcherLessCode and Search methods from decompiled_FusionProtector.
    /// </summary>
    public static class MapChangeController
    {
        private static List<LevelEntry> _allLevels = new List<LevelEntry>();
        private static List<LevelEntry> _filteredLevels = new List<LevelEntry>();
        private static string _searchQuery = "";
        private static bool _levelsLoaded = false;

        public struct LevelEntry
        {
            public string Title;
            public string BarcodeID;
            public string PalletName;
            public Barcode BarcodeRef; // Keep the actual Il2Cpp Barcode reference
        }

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("MapChangeController initialized (FusionProtector style, no permission check)");
        }

        // ── Level Loading (FusionProtector's LevelSearcherLessCode) ───────

        /// <summary>
        /// Load a level by barcode string (and optionally its Il2Cpp Barcode ref).
        /// Tries multiple approaches to maximize compatibility:
        ///   1. Direct Il2Cpp Barcode reference from discovery
        ///   2. LevelCrateReference → ScannableReference → Barcode → SceneStreamer.Load
        ///   3. new Barcode(string) → SceneStreamer.Load
        /// </summary>
        public static void LoadLevel(string barcode, Barcode barcodeRef = null)
        {
            if (string.IsNullOrEmpty(barcode))
            {
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Warning, "No barcode specified");
                return;
            }

            Main.MelonLog.Msg($"[MapChange] LoadLevel requested: '{barcode}'");

            // Approach 1: Use stored Il2Cpp Barcode reference directly (safest)
            if (barcodeRef != null)
            {
                try
                {
                    Main.MelonLog.Msg($"[MapChange] Approach 1: Using stored Barcode ref (ID: '{barcodeRef.ID}')");
                    SceneStreamer.Load(barcodeRef, (Barcode)null);
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Loading level...");
                    return;
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[MapChange] Approach 1 failed: {ex.Message}");
                }
            }

            // Approach 2: LevelCrateReference (FP's LevelSearcherLessCode pattern)
            try
            {
                Main.MelonLog.Msg($"[MapChange] Approach 2: LevelCrateReference");
                var levelRef = new LevelCrateReference(barcode);
                var resolvedBarcode = ((ScannableReference)levelRef).Barcode;
                Main.MelonLog.Msg($"[MapChange] Resolved barcode: '{resolvedBarcode?.ID}'");
                SceneStreamer.Load(resolvedBarcode, (Barcode)null);
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Loading level...");
                return;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[MapChange] Approach 2 failed: {ex.Message}");
            }

            // Approach 3: Direct Barcode constructor (FP's MakeOWNERSAutoChangeMap pattern)
            try
            {
                Main.MelonLog.Msg($"[MapChange] Approach 3: new Barcode(string)");
                SceneStreamer.Load(new Barcode(barcode), (Barcode)null);
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Loading level (fallback)...");
                return;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[MapChange] Approach 3 failed: {ex.Message}");
            }

            // Approach 4: Look up barcode from AssetWarehouse directly
            try
            {
                Main.MelonLog.Msg($"[MapChange] Approach 4: AssetWarehouse lookup");
                var crate = LabFusion.Marrow.CrateFilterer.GetCrate<LevelCrate>(new Barcode(barcode));
                if (crate != null)
                {
                    var crateBarcode = ((Scannable)crate).Barcode;
                    Main.MelonLog.Msg($"[MapChange] Found crate, barcode: '{crateBarcode?.ID}'");
                    SceneStreamer.Load(crateBarcode, (Barcode)null);
                    NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Loading level...");
                    return;
                }
                Main.MelonLog.Warning($"[MapChange] Approach 4: Crate not found");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[MapChange] Approach 4 failed: {ex.Message}");
            }

            Main.MelonLog.Error($"[MapChange] All approaches failed for barcode: '{barcode}'");
            NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error, "Failed to load level");
        }

        /// <summary>
        /// Reload the current level.
        /// </summary>
        public static void ReloadLevel()
        {
            try
            {
                Main.MelonLog.Msg("[MapChange] Reloading current level...");

                // Log current level info
                try
                {
                    var session = SceneStreamer.Session;
                    if (session != null)
                    {
                        var level = session.Level;
                        if (level != null)
                        {
                            var bc = ((Scannable)level).Barcode;
                            Main.MelonLog.Msg($"[MapChange] Current level: '{bc?.ID}'");
                        }
                        else
                        {
                            Main.MelonLog.Msg("[MapChange] Session.Level is null");
                        }
                    }
                    else
                    {
                        Main.MelonLog.Msg("[MapChange] SceneStreamer.Session is null");
                    }
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Msg($"[MapChange] Could not read current level info: {ex.Message}");
                }

                SceneStreamer.Reload();
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, "Reloading level...");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[MapChange] ReloadLevel error: {ex.Message}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Error, $"Reload failed: {ex.Message}");
            }
        }

        // ── Level Discovery (FusionProtector's Search pattern) ────────────

        /// <summary>
        /// Discover all installed levels using direct Il2Cpp types.
        /// Mirrors FusionProtector's Search method:
        ///   List&lt;Pallet&gt; pallets = AssetWarehouse.Instance.GetPallets();
        ///   foreach pallet → foreach crate → CrateFilterer.GetCrate&lt;LevelCrate&gt;(barcode)
        /// </summary>
        public static void LoadAllLevels()
        {
            _allLevels.Clear();
            _levelsLoaded = false;

            try
            {
                // Direct Il2Cpp API — same as FP
                var warehouse = AssetWarehouse.Instance;
                if (warehouse == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse.Instance is null — game not fully loaded?");
                    return;
                }

                var pallets = warehouse.GetPallets();
                if (pallets == null)
                {
                    Main.MelonLog.Warning("GetPallets returned null");
                    return;
                }

                Main.MelonLog.Msg($"Scanning {pallets.Count} pallets for levels...");

                // Iterate pallets → crates, same as FP's Search method
                var enumerator = pallets.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Pallet pallet = enumerator.Current;
                    if (pallet == null) continue;

                    string palletName = "Unknown";
                    try { palletName = ((Scannable)pallet).Title ?? "Unknown"; } catch { }

                    var crateEnum = pallet.Crates.GetEnumerator();
                    while (crateEnum.MoveNext())
                    {
                        Crate crate = crateEnum.Current;
                        if (crate == null) continue;

                        try
                        {
                            // FP pattern: CrateFilterer.GetCrate<LevelCrate>(barcode) to check if it's a level
                            Barcode barcodeObj = ((Scannable)crate).Barcode;
                            if (barcodeObj == null) continue;

                            LevelCrate levelCrate = null;
                            try
                            {
                                levelCrate = LabFusion.Marrow.CrateFilterer.GetCrate<LevelCrate>(barcodeObj);
                            }
                            catch { }

                            if (levelCrate == null) continue;

                            string barcodeId = barcodeObj.ID;
                            if (string.IsNullOrEmpty(barcodeId)) continue;

                            string title = ((UnityEngine.Object)crate).name ?? "";
                            if (string.IsNullOrEmpty(title))
                                title = barcodeId;

                            _allLevels.Add(new LevelEntry
                            {
                                Title = title,
                                BarcodeID = barcodeId,
                                PalletName = palletName,
                                BarcodeRef = barcodeObj // Store Il2Cpp reference for direct use
                            });
                        }
                        catch { }
                    }
                }

                _allLevels = _allLevels.OrderBy(x => x.Title).ToList();
                _levelsLoaded = true;
                Main.MelonLog.Msg($"Loaded {_allLevels.Count} levels for map changer");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"LoadAllLevels error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void Search()
        {
            _filteredLevels.Clear();
            string query = (_searchQuery ?? "").ToLower().Trim();

            foreach (var level in _allLevels)
            {
                if (string.IsNullOrEmpty(query) ||
                    level.Title.ToLower().Contains(query) ||
                    level.PalletName.ToLower().Contains(query) ||
                    level.BarcodeID.ToLower().Contains(query))
                {
                    _filteredLevels.Add(level);
                }
            }
        }

        /// <summary>
        /// Returns a copy of the current filtered level list for external use (QuickMenu).
        /// </summary>
        public static List<LevelEntry> GetFilteredLevels()
        {
            return new List<LevelEntry>(_filteredLevels);
        }

        /// <summary>
        /// Populate a BoneMenu page with search results. Clicking loads the level.
        /// </summary>
        public static void PopulateResultsPage(Page resultsPage, int maxItems = 50)
        {
            if (resultsPage == null) return;

            // Always re-scan levels when populating (fix for stale/empty results)
            LoadAllLevels();
            Search();

            try { resultsPage.RemoveAll(); } catch { }

            int count = 0;
            foreach (var level in _filteredLevels)
            {
                if (count >= maxItems) break;

                string displayName = $"[{count + 1}] {level.Title}";
                string barcode = level.BarcodeID;
                Barcode barcodeRef = level.BarcodeRef;

                resultsPage.CreateFunction(displayName, Color.green, () => LoadLevel(barcode, barcodeRef));
                count++;
            }

            if (count == 0)
            {
                resultsPage.CreateFunction("No levels found", Color.gray, () => { });
            }

            Main.MelonLog.Msg($"Showing {count} level results");
            NotificationHelper.Send(BoneLib.Notifications.NotificationType.Information, $"{count} levels found");
        }
    }
}
