using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Bonelab.SaveData;
using Il2CppSLZ.Marrow.SaveData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BonelabUtilityMod
{
    // ═══════════════════════════════════════════════════
    // ENUMS — Avatar search configuration
    // ═══════════════════════════════════════════════════
    public enum AvatarSearchType
    {
        ChangeInto,
        CopyDetailsToClipboard,
        SetBodyLog
    }

    public enum AvatarSearchMethod
    {
        CrateNames,
        BarcodeIDNames,
        PalletName,
        PalletAuthor
    }

    public static class AvatarSearchController
    {
        // ═══════════════════════════════════════════════════
        // Cached avatar list (FusionProtector approach)
        // ═══════════════════════════════════════════════════
        private static HashSet<AvatarCrateReference> _avatarsStored = new HashSet<AvatarCrateReference>();

        // ═══════════════════════════════════════════════════
        // Search state
        // ═══════════════════════════════════════════════════
        private static bool _isSearching = false;
        private static string _searchQuery = "";
        private static AvatarSearchMethod _searchMethod = AvatarSearchMethod.CrateNames;
        private static AvatarSearchType _searchType = AvatarSearchType.ChangeInto;
        private static int _bodyLogSlot = 1;
        private static int _resultCount = 0;

        // Overlay menu results cache
        private static List<(string name, string barcode)> _overlayResults = new List<(string, string)>();

        // Results & history page refs (set by menu builder)
        private static Page _resultsPage = null;
        private static Page _historyPage = null;
        private static Action<string> _customAction = null;

        // Search history
        private struct HistoryEntry
        {
            public string Query;
        }
        private static List<HistoryEntry> _searchHistory = new List<HistoryEntry>();
        private static int _maxHistorySize = 30;

        // ═══════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════
        public static bool IsSearching => _isSearching;
        public static int ResultCount => _resultCount;
        public static int CachedAvatarCount => _avatarsStored.Count;

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static AvatarSearchMethod CurrentSearchMethod
        {
            get => _searchMethod;
            set => _searchMethod = value;
        }

        public static AvatarSearchType CurrentSearchType
        {
            get => _searchType;
            set => _searchType = value;
        }

        public static int BodyLogSlot
        {
            get => _bodyLogSlot;
            set => _bodyLogSlot = Mathf.Clamp(value, 1, 6);
        }

        // ═══════════════════════════════════════════════════
        // AVATAR CACHE — FusionProtector LoadAssets approach
        // ═══════════════════════════════════════════════════
        public static void LoadAvatarCache()
        {
            _avatarsStored.Clear();

            try
            {
                var pallets = AssetWarehouse.Instance.GetPallets();
                if (pallets == null) return;

                foreach (var pallet in pallets)
                {
                    if (pallet == null) continue;
                    var crates = pallet.Crates;
                    if (crates == null) continue;

                    foreach (var crate in crates)
                    {
                        if (crate == null) continue;
                        string id = crate.Barcode?.ID;
                        if (string.IsNullOrEmpty(id)) continue;

                        var avatarRef = new AvatarCrateReference(id);
                        if (avatarRef.Crate != null)
                        {
                            _avatarsStored.Add(avatarRef);
                        }
                    }
                }

                Main.MelonLog.Msg($"[Avatar Cache] Loaded {_avatarsStored.Count} avatars");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[Avatar Cache] Failed: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        // PUBLIC API — Search methods
        // ═══════════════════════════════════════════════════
        public static void SearchToPage(Page resultsPage, Page historyPage = null)
        {
            _resultsPage = resultsPage;
            _historyPage = historyPage;
            _customAction = null;
            MelonCoroutines.Start(SearchCoroutine());
        }

        public static void SearchToPageWithAction(Page resultsPage, Action<string> action)
        {
            _resultsPage = resultsPage;
            _historyPage = null;
            _customAction = action;
            MelonCoroutines.Start(SearchCoroutine());
        }

        // ═══════════════════════════════════════════════════
        // CORE SEARCH — Uses cached AvatarCrateReference list
        // ═══════════════════════════════════════════════════
        private static IEnumerator SearchCoroutine()
        {
            if (_isSearching)
            {
                NotificationHelper.Send(NotificationType.Warning, "Avatar search already running, please wait...");
                yield break;
            }

            _isSearching = true;
            _resultCount = 0;
            _overlayResults.Clear();

            if (_resultsPage != null)
            {
                try { _resultsPage.RemoveAll(); } catch { }
            }

            // Reload avatar cache each search (like FP does)
            LoadAvatarCache();

            if (_avatarsStored.Count == 0)
            {
                FinishSearch("No avatars loaded - game may not be fully loaded");
                yield break;
            }

            string searchLower = (_searchQuery ?? "").ToLower();

            // For PalletName/PalletAuthor, group by pallet
            if (_searchMethod == AvatarSearchMethod.PalletName || _searchMethod == AvatarSearchMethod.PalletAuthor)
            {
                var palletGroups = new Dictionary<string, List<AvatarCrateReference>>();

                foreach (var avatarRef in _avatarsStored)
                {
                    if (avatarRef == null) continue;
                    var crate = avatarRef.Crate;
                    if (crate == null) continue;
                    var pallet = ((Crate)crate).Pallet;
                    if (pallet == null) continue;

                    string palletName = StripRichText(pallet.name ?? "");
                    string palletAuthor = pallet.Author ?? "";
                    string palletKey = $"{palletName}|{palletAuthor}";

                    bool match = false;
                    if (_searchMethod == AvatarSearchMethod.PalletName)
                        match = palletName.ToLower().Contains(searchLower);
                    else if (_searchMethod == AvatarSearchMethod.PalletAuthor)
                        match = !string.IsNullOrEmpty(palletAuthor) && palletAuthor.ToLower().Contains(searchLower);

                    if (match)
                    {
                        if (!palletGroups.ContainsKey(palletKey))
                            palletGroups[palletKey] = new List<AvatarCrateReference>();
                        palletGroups[palletKey].Add(avatarRef);
                    }
                }

                foreach (var kvp in palletGroups)
                {
                    string[] parts = kvp.Key.Split('|');
                    string displayName = _searchMethod == AvatarSearchMethod.PalletAuthor
                        ? $"+ {parts[0]} ({parts[1]})"
                        : $"+ {parts[0]}";

                    Page palletPage = null;
                    try { palletPage = _resultsPage?.CreatePage(displayName, Color.green); } catch { }

                    foreach (var avatarRef in kvp.Value)
                    {
                        var crate = avatarRef.Crate;
                        if (crate == null) continue;
                        string name = StripRichText(((UnityEngine.Object)crate).name ?? "");
                        string barcodeId = ((ScannableReference)avatarRef).Barcode?.ID ?? "";
                        if (string.IsNullOrEmpty(barcodeId)) continue;

                        string capturedBarcode = barcodeId;
                        string capturedName = name;
                        try { palletPage?.CreateFunction(capturedName, Color.green, () => OnAvatarSelected(capturedBarcode)); } catch { }
                        _overlayResults.Add((capturedName, capturedBarcode));
                        _resultCount++;
                    }
                }
            }
            else
            {
                // CrateNames and BarcodeIDNames
                int processed = 0;
                foreach (var avatarRef in _avatarsStored)
                {
                    processed++;
                    if (avatarRef == null) continue;
                    var crate = avatarRef.Crate;
                    if (crate == null) continue;

                    string crateName = StripRichText(((UnityEngine.Object)crate).name ?? "");
                    string barcodeId = ((ScannableReference)avatarRef).Barcode?.ID ?? "";
                    if (string.IsNullOrEmpty(barcodeId)) continue;

                    bool match = false;
                    if (_searchMethod == AvatarSearchMethod.CrateNames)
                        match = crateName.ToLower().Contains(searchLower);
                    else if (_searchMethod == AvatarSearchMethod.BarcodeIDNames)
                        match = barcodeId.ToLower().Contains(searchLower);

                    if (match)
                    {
                        string capturedBarcode = barcodeId;
                        string capturedName = crateName;
                        try { _resultsPage?.CreateFunction(capturedName, Color.green, () => OnAvatarSelected(capturedBarcode)); } catch { }
                        _overlayResults.Add((capturedName, capturedBarcode));
                        _resultCount++;
                    }

                    if (processed >= 100)
                    {
                        processed = 0;
                        yield return null;
                    }
                }
            }

            try { AddToHistory(_searchQuery); } catch { }
            FinishSearch(null);
        }

        private static void FinishSearch(string errorMsg)
        {
            _isSearching = false;
            if (errorMsg != null)
            {
                NotificationHelper.Send(NotificationType.Error, errorMsg);
                Main.MelonLog.Warning($"Avatar search: {errorMsg}");
            }
            else
            {
                Main.MelonLog.Msg($"[Avatar Searcher] Completed - Found {_resultCount} Results.");
                NotificationHelper.Send(NotificationType.Success, $"Avatar Search: Found {_resultCount} results");
            }
        }

        // ═══════════════════════════════════════════════════
        // ACTION — What happens when an avatar result is clicked
        // ═══════════════════════════════════════════════════
        private static void OnAvatarSelected(string barcode)
        {
            if (_customAction != null)
            {
                _customAction.Invoke(barcode);
                return;
            }

            switch (_searchType)
            {
                case AvatarSearchType.ChangeInto:
                    SwapAvatar(barcode);
                    break;

                case AvatarSearchType.CopyDetailsToClipboard:
                    CopyAvatarDetails(barcode);
                    break;

                case AvatarSearchType.SetBodyLog:
                    SetBodyLogSlot(_bodyLogSlot, barcode);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════
        // SEARCH HISTORY
        // ═══════════════════════════════════════════════════
        private static void AddToHistory(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            if (_searchHistory.Any(h => h.Query.Equals(query, StringComparison.OrdinalIgnoreCase)))
                return;
            _searchHistory.Add(new HistoryEntry { Query = query });
            if (_searchHistory.Count > _maxHistorySize)
                _searchHistory.RemoveAt(0);

            if (_historyPage != null)
            {
                string queryCapture = query;
                try
                {
                    _historyPage.CreateFunction(queryCapture, Color.yellow, () =>
                    {
                        _searchQuery = queryCapture;
                        SearchToPage(_resultsPage, _historyPage);
                    });
                }
                catch { }
            }
        }

        // ═══════════════════════════════════════════════════
        // OVERLAY MENU API
        // ═══════════════════════════════════════════════════
        public static void SearchAvatars(string query)
        {
            _searchQuery = query ?? "";
            MelonCoroutines.Start(SearchCoroutine());
        }

        public static List<(string name, string barcode)> GetLastResults()
        {
            return _overlayResults;
        }

        // ═══════════════════════════════════════════════════
        // AVATAR ACTIONS — Direct API calls (FusionProtector approach)
        // ═══════════════════════════════════════════════════
        public static void SwapAvatar(string avatarBarcode)
        {
            if (string.IsNullOrEmpty(avatarBarcode)) return;

            try
            {
                var rigManager = Player.RigManager;
                if (rigManager == null) return;

                // FusionProtector approach: create AvatarCrateReference, use direct API
                var avatarRef = new AvatarCrateReference(avatarBarcode);
                var barcode = ((ScannableReference)avatarRef).Barcode;

                rigManager.SwapAvatarCrate(barcode, false, (Il2CppSystem.Action<bool>)null);

                // Save to DataManager directly
                try
                {
                    DataManager.ActiveSave.PlayerSettings.CurrentAvatar = barcode.ID;
                    DataManager.TrySaveActiveSave((SaveFlags)2);
                }
                catch (Exception saveEx)
                {
                    Main.MelonLog.Warning($"Avatar save failed (swap still applied): {saveEx.Message}");
                }

                Main.MelonLog.Msg($"Avatar swapped to: {avatarBarcode}");
                NotificationHelper.Send(NotificationType.Success, $"Avatar changed!");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Avatar swap failed: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Avatar swap failed: {ex.Message}");
            }
        }

        public static void CopyAvatarDetails(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return;
            try
            {
                string details = $"Barcode ID: {barcode}";

                try
                {
                    var avatarRef = new AvatarCrateReference(barcode);
                    var crate = avatarRef.Crate;
                    if (crate != null)
                    {
                        var pallet = ((Crate)crate).Pallet;
                        string crateName = StripRichText(((UnityEngine.Object)crate).name ?? "Unknown");
                        string palletName = pallet != null ? StripRichText(pallet.name ?? "") : "";
                        string author = pallet?.Author ?? "";
                        details = $"Barcode ID: {barcode}\nCrate Name: {crateName}\nPallet Name: {palletName}\nPallet Author: {author}";
                    }
                }
                catch { }

                GUIUtility.systemCopyBuffer = details;
                NotificationHelper.Send(NotificationType.Success, "Avatar details copied to clipboard!");
            }
            catch (Exception ex)
            {
                GUIUtility.systemCopyBuffer = barcode;
                NotificationHelper.Send(NotificationType.Success, "Barcode copied to clipboard!");
            }
        }

        public static void SetBodyLogSlot(int slot, string barcode)
        {
            try
            {
                var save = DataManager.ActiveSave;
                if (save == null)
                {
                    NotificationHelper.Send(NotificationType.Error, "No active save data");
                    return;
                }

                var playerSettings = save.PlayerSettings;
                if (playerSettings == null)
                {
                    NotificationHelper.Send(NotificationType.Error, "No player settings");
                    return;
                }

                // Find body log property via reflection (property name varies)
                bool slotSet = false;
                try
                {
                    var settingsType = playerSettings.GetType();
                    var bodyLogProp = settingsType.GetProperty("BodyLogSlots")
                        ?? settingsType.GetProperty("BodyLog")
                        ?? settingsType.GetProperty("BodyLogBarCodes");
                    if (bodyLogProp != null)
                    {
                        var bodyLog = bodyLogProp.GetValue(playerSettings);
                        var indexer = bodyLog?.GetType().GetProperty("Item");
                        if (indexer != null)
                        {
                            indexer.SetValue(bodyLog, barcode, new object[] { slot - 1 });
                            slotSet = true;
                        }
                    }
                }
                catch (Exception blEx)
                {
                    Main.MelonLog.Warning($"Body log slot set failed: {blEx.Message}");
                }

                DataManager.TrySaveActiveSave((SaveFlags)2);
                if (slotSet)
                    NotificationHelper.Send(NotificationType.Success, $"Body log slot {slot} set to: {barcode}");
                else
                    NotificationHelper.Send(NotificationType.Warning, $"Body log slot property not found - avatar saved anyway");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Set body log failed: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, "Failed to set body log slot");
            }
        }

        // ═══════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════
        private static string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return System.Text.RegularExpressions.Regex.Replace(input, @"<[^>]+>", "").Trim();
        }
    }
}
