using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace BonelabUtilityMod
{
    public static class AvatarCopierController
    {
        public struct PlayerAvatarInfo
        {
            public byte SmallID;
            public string DisplayName;
            public string Username;
            public string Nickname;
            public string Description;
            public string AvatarBarcode;
            public string AvatarTitle;
            public object PlayerIDObj;
            public List<string> EquippedCosmetics;
        }

        private static List<PlayerAvatarInfo> cachedPlayers = new List<PlayerAvatarInfo>();
        private static int currentPlayerIndex = 0;
        private static string lastCopiedUsername = "";
        private static string lastCopiedAvatarTitle = "";

        // Revert state — saved before copying
        private static string _previousAvatarBarcode = "";
        private static string _previousNickname = "";
        private static string _previousDescription = "";
        private static List<string> _previousCosmetics = new List<string>();
        private static bool _hasRevertState = false;

        // Search system (FusionProtector style)
        private static string _searchQuery = "";
        private static Page _resultsPage = null;
        private static bool _copyNickname = true;
        private static bool _copyDescription = true;
        private static bool _copyCosmetics = true;
        private static object _pendingCosmeticsCoroutine = null;

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static bool CopyNickname
        {
            get => _copyNickname;
            set => _copyNickname = value;
        }

        public static bool CopyDescription
        {
            get => _copyDescription;
            set => _copyDescription = value;
        }

        public static bool CopyCosmetics
        {
            get => _copyCosmetics;
            set => _copyCosmetics = value;
        }

        public static bool HasRevertState => _hasRevertState;

        public static string CurrentPlayerName
        {
            get
            {
                if (cachedPlayers.Count == 0)
                    return "No Players";
                if (currentPlayerIndex >= cachedPlayers.Count)
                    currentPlayerIndex = 0;
                return cachedPlayers[currentPlayerIndex].DisplayName;
            }
        }

        public static string CurrentAvatarTitle
        {
            get
            {
                if (cachedPlayers.Count == 0)
                    return "N/A";
                if (currentPlayerIndex >= cachedPlayers.Count)
                    currentPlayerIndex = 0;
                return cachedPlayers[currentPlayerIndex].AvatarTitle;
            }
        }

        public static string LastCopiedInfo => string.IsNullOrEmpty(lastCopiedUsername)
            ? "None"
            : $"{lastCopiedUsername} ({lastCopiedAvatarTitle})";

        public static int PlayerCount => cachedPlayers.Count;
        public static IReadOnlyList<PlayerAvatarInfo> Players => cachedPlayers;

        public static void SelectAndCopy(int index)
        {
            if (index < 0 || index >= cachedPlayers.Count) return;
            currentPlayerIndex = index;
            CopySelectedAvatar();
        }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Avatar Copier controller initialized");
        }

        /// <summary>
        /// Search players and populate results page (FusionProtector style)
        /// Clicking a player copies their avatar
        /// </summary>
        public static void SearchToPage(Page resultsPage)
        {
            _resultsPage = resultsPage;

            // Refresh player list first
            RefreshPlayerListInternal();

            // Clear page
            try { _resultsPage?.RemoveAll(); } catch { }

            if (cachedPlayers.Count == 0)
            {
                SendNotification(NotificationType.Warning, "Not in multiplayer or no other players");
                return;
            }

            string searchLower = (_searchQuery ?? "").ToLower();
            int resultCount = 0;

            foreach (var player in cachedPlayers)
            {
                // Filter by search query if provided
                if (!string.IsNullOrEmpty(searchLower))
                {
                    if (!player.DisplayName.ToLower().Contains(searchLower) &&
                        !(player.Nickname ?? "").ToLower().Contains(searchLower) &&
                        !(player.Username ?? "").ToLower().Contains(searchLower) &&
                        !player.AvatarTitle.ToLower().Contains(searchLower))
                        continue;
                }

                // Capture for closure
                var capturedPlayer = player;

                string displayText = $"{capturedPlayer.DisplayName} ({capturedPlayer.AvatarTitle})";

                _resultsPage?.CreateFunction(displayText, Color.green, () =>
                {
                    CopyAvatarFromPlayer(capturedPlayer);
                });
                resultCount++;
            }

            Main.MelonLog.Msg($"[Avatar Search] Found {resultCount} player(s)");
            SendNotification(NotificationType.Success, $"{resultCount} player(s) found");
        }

        /// <summary>
        /// Save current state for revert (avatar barcode, nickname, cosmetics)
        /// </summary>
        private static void SaveRevertState()
        {
            try
            {
                // Save current avatar barcode
                var rigManager = Player.RigManager;
                if (rigManager != null)
                {
                    var avatarCrateProp = rigManager.GetType().GetProperty("AvatarCrate", BindingFlags.Public | BindingFlags.Instance);
                    if (avatarCrateProp != null)
                    {
                        var avatarCrate = avatarCrateProp.GetValue(rigManager);
                        if (avatarCrate != null)
                        {
                            var barcodeProp = avatarCrate.GetType().GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                            var barcode = barcodeProp?.GetValue(avatarCrate);
                            var idProp = barcode?.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                            _previousAvatarBarcode = idProp?.GetValue(barcode) as string ?? "";
                        }
                    }
                }

                // Save current nickname
                try
                {
                    var localPlayerType = FindTypeByName("LocalPlayer");
                    var metadataProp = localPlayerType?.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
                    var metadata = metadataProp?.GetValue(null);
                    var nickProp = metadata?.GetType().GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                    var nickObj = nickProp?.GetValue(metadata);
                    var getValueMethods = nickObj?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod).ToArray();
                    _previousNickname = getValueMethods?.Length > 0 ? getValueMethods[0].Invoke(nickObj, null) as string ?? "" : "";
                }
                catch { _previousNickname = ""; }

                // Save current description
                try
                {
                    var localPlayerType = FindTypeByName("LocalPlayer");
                    var metadataProp = localPlayerType?.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
                    var metadata = metadataProp?.GetValue(null);
                    var descProp = metadata?.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                    var descObj = descProp?.GetValue(metadata);
                    var getValueMethods = descObj?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod).ToArray();
                    _previousDescription = getValueMethods?.Length > 0 ? getValueMethods[0].Invoke(descObj, null) as string ?? "" : "";
                }
                catch { _previousDescription = ""; }

                // Save current equipped cosmetics
                _previousCosmetics.Clear();
                try
                {
                    var pointItemManagerType = FindTypeByName("PointItemManager");
                    var loadedItemsProp = pointItemManagerType?.GetProperty("LoadedItems", BindingFlags.Public | BindingFlags.Static);
                    var loadedItems = loadedItemsProp?.GetValue(null) as System.Collections.IEnumerable;
                    if (loadedItems != null)
                    {
                        foreach (var item in loadedItems)
                        {
                            var isEquippedProp = item.GetType().GetProperty("IsEquipped", BindingFlags.Public | BindingFlags.Instance);
                            if (isEquippedProp != null && (bool)isEquippedProp.GetValue(item))
                            {
                                var barcodePropItem = item.GetType().GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                                var bc = barcodePropItem?.GetValue(item) as string;
                                if (!string.IsNullOrEmpty(bc))
                                    _previousCosmetics.Add(bc);
                            }
                        }
                    }
                }
                catch { }

                _hasRevertState = true;
                Main.MelonLog.Msg($"Saved revert state: avatar={_previousAvatarBarcode}, cosmetics={_previousCosmetics.Count}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to save revert state: {ex.Message}");
            }
        }

        /// <summary>
        /// Revert to previous avatar, nickname, and cosmetics.
        /// </summary>
        public static void RevertAvatar()
        {
            if (!_hasRevertState)
            {
                SendNotification(NotificationType.Warning, "No revert state saved");
                return;
            }

            try
            {
                // Revert avatar
                if (!string.IsNullOrEmpty(_previousAvatarBarcode))
                {
                    SwapToAvatar(_previousAvatarBarcode);
                    Main.MelonLog.Msg($"Reverted avatar to: {_previousAvatarBarcode}");
                }

                // Revert nickname
                if (!string.IsNullOrEmpty(_previousNickname))
                {
                    SetLocalNickname(_previousNickname);
                    Main.MelonLog.Msg($"Reverted nickname to: {_previousNickname}");
                }
                else
                {
                    // Clear nickname
                    SetLocalNickname("");
                }

                // Revert description
                SetLocalDescription(_previousDescription ?? "");
                Main.MelonLog.Msg($"Reverted description to: {(_previousDescription?.Length > 0 ? _previousDescription : "(empty)")}");

                // Revert cosmetics — delayed to wait for avatar swap (async, 2-3+ frames)
                if (_pendingCosmeticsCoroutine != null)
                    MelonCoroutines.Stop(_pendingCosmeticsCoroutine);
                _pendingCosmeticsCoroutine = MelonCoroutines.Start(
                    DelayedApplyCosmeticsCoroutine(new List<string>(_previousCosmetics)));

                _hasRevertState = false;
                lastCopiedUsername = "";
                lastCopiedAvatarTitle = "";
                SendNotification(NotificationType.Success, "Reverted to previous avatar");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"RevertAvatar error: {ex.Message}");
                SendNotification(NotificationType.Error, $"Revert failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Swap local avatar to a barcode (shared helper)
        /// </summary>
        private static void SwapToAvatar(string avatarBarcode)
        {
            var rigManager = Player.RigManager;
            if (rigManager == null) return;

            var barcodeType = FindTypeByName("Barcode");
            var barcodeCtor = barcodeType?.GetConstructor(new[] { typeof(string) });
            if (barcodeCtor == null) return;
            var barcodeObj = barcodeCtor.Invoke(new object[] { avatarBarcode });

            var swapMethod = rigManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "SwapAvatarCrate" && m.GetParameters().Length >= 2);
            if (swapMethod == null) return;

            var parameters = swapMethod.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == 0) args[i] = barcodeObj;
                else if (parameters[i].ParameterType == typeof(bool)) args[i] = true;
                else args[i] = null;
            }
            swapMethod.Invoke(rigManager, args);
        }

        /// <summary>
        /// Copy avatar (and optionally nickname + cosmetics) from a specific player
        /// </summary>
        private static void CopyAvatarFromPlayer(PlayerAvatarInfo player)
        {
            if (string.IsNullOrEmpty(player.AvatarBarcode))
            {
                SendNotification(NotificationType.Error, "No avatar barcode available");
                return;
            }

            try
            {
                // Save revert state BEFORE copying
                SaveRevertState();

                // Swap avatar
                SwapToAvatar(player.AvatarBarcode);

                // Copy nickname if enabled
                // Priority: nickname first, fall back to username, else clear
                if (_copyNickname)
                {
                    if (!string.IsNullOrWhiteSpace(player.Nickname))
                    {
                        SetLocalNickname(player.Nickname);
                        Main.MelonLog.Msg($"Copied nickname: {player.Nickname}");
                    }
                    else if (!string.IsNullOrWhiteSpace(player.Username) && player.Username != "No Name")
                    {
                        SetLocalNickname(player.Username);
                        Main.MelonLog.Msg($"Copied username as nickname: {player.Username}");
                    }
                    else
                    {
                        // Target has no custom name — clear our nickname to revert to default
                        SetLocalNickname("");
                        Main.MelonLog.Msg("Target has no nickname — cleared local nickname");
                    }
                }

                // Copy cosmetics if enabled — delayed to wait for avatar swap to finish
                // SwapAvatarCrate is async (2-3+ frames), cosmetics need the new rig to be ready
                if (_copyCosmetics)
                {
                    var barcodes = player.EquippedCosmetics ?? new List<string>();
                    if (_pendingCosmeticsCoroutine != null)
                        MelonCoroutines.Stop(_pendingCosmeticsCoroutine);
                    _pendingCosmeticsCoroutine = MelonCoroutines.Start(
                        DelayedApplyCosmeticsCoroutine(barcodes));
                }

                // Copy description if enabled
                if (_copyDescription)
                {
                    string desc = player.Description ?? "";
                    SetLocalDescription(desc);
                    Main.MelonLog.Msg($"Copied description: {(desc.Length > 0 ? desc : "(empty)")}");
                }

                lastCopiedUsername = player.DisplayName;
                lastCopiedAvatarTitle = player.AvatarTitle;

                string msg = $"Copied from {player.DisplayName}\nAvatar: {player.AvatarTitle}";
                if (_copyNickname && !string.IsNullOrWhiteSpace(player.Nickname))
                    msg += $"\nNickname: {player.Nickname}";
                else if (_copyNickname && !string.IsNullOrWhiteSpace(player.Username))
                    msg += $"\nUsername: {player.Username}";
                if (_copyDescription && !string.IsNullOrEmpty(player.Description))
                    msg += $"\nDescription: {player.Description}";
                if (_copyCosmetics)
                    msg += $"\nCosmetics: {(player.EquippedCosmetics?.Count ?? 0)}";

                Main.MelonLog.Msg($"Copied avatar from {player.DisplayName}: {player.AvatarTitle}");
                SendNotification(NotificationType.Success, msg);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"Failed to copy avatar: {ex.Message}\n{ex.StackTrace}");
                SendNotification(NotificationType.Error, "Failed to copy avatar");
            }
        }

        /// <summary>
        /// Copy cosmetics from another player's equipped items list.
        /// Unlocks items locally, then equips them.
        /// </summary>

        /// <summary>
        /// Coroutine that waits for the avatar swap to complete before applying cosmetics.
        /// SwapAvatarCrate is async (UniTask, 2-3+ frames), so cosmetics applied in the same
        /// frame would fail because OnEquipChanged silently returns if the rig is mid-recreation.
        /// </summary>
        private static IEnumerator DelayedApplyCosmeticsCoroutine(List<string> equippedBarcodes)
        {
            // Wait for the avatar swap async pipeline to finish.
            // LabFusion's CoWaitAndSwapAvatarRoutine takes multiple frames:
            //   - asset load (1+ frames)
            //   - SwapAvatar + poll until rm.avatar matches (1+ frames)
            //   - ArtRigPatches.SetArtOutputAvatar adds another 2-frame delay
            // 1 second is conservative and covers all cases.
            yield return new WaitForSeconds(1.0f);

            CopyCosmeticsFromPlayer(equippedBarcodes);
            _pendingCosmeticsCoroutine = null;
        }

        private static void CopyCosmeticsFromPlayer(List<string> equippedBarcodes)
        {
            try
            {
                var pointItemManagerType = FindTypeByName("PointItemManager");
                var pointSaveManagerType = FindTypeByName("PointSaveManager");
                if (pointItemManagerType == null || pointSaveManagerType == null)
                {
                    Main.MelonLog.Warning("PointItemManager or PointSaveManager not found");
                    return;
                }

                var tryGetMethod = pointItemManagerType.GetMethod("TryGetPointItem", BindingFlags.Public | BindingFlags.Static);
                var setEquippedMethod = pointItemManagerType.GetMethod("SetEquipped", BindingFlags.Public | BindingFlags.Static);
                var unlockMethod = pointSaveManagerType.GetMethod("UnlockItem", BindingFlags.Public | BindingFlags.Static);
                var loadedItemsProp = pointItemManagerType.GetProperty("LoadedItems", BindingFlags.Public | BindingFlags.Static);

                if (tryGetMethod == null || setEquippedMethod == null)
                {
                    Main.MelonLog.Warning("PointItemManager methods not found");
                    return;
                }

                // First unequip all current cosmetics
                var loadedItems = loadedItemsProp?.GetValue(null) as System.Collections.IEnumerable;
                if (loadedItems != null)
                {
                    foreach (var item in loadedItems)
                    {
                        var isEquippedProp = item.GetType().GetProperty("IsEquipped", BindingFlags.Public | BindingFlags.Instance);
                        if (isEquippedProp != null && (bool)isEquippedProp.GetValue(item))
                        {
                            try { setEquippedMethod.Invoke(null, new object[] { item, false }); } catch { }
                        }
                    }
                }

                // Equip target player's cosmetics
                int copiedCount = 0;
                int skippedCount = 0;
                foreach (var barcode in equippedBarcodes)
                {
                    try
                    {
                        // Unlock the item first (required for SetEquipped guard)
                        if (unlockMethod != null)
                            unlockMethod.Invoke(null, new object[] { barcode });

                        var args = new object[] { barcode, null };
                        if ((bool)tryGetMethod.Invoke(null, args) && args[1] != null)
                        {
                            setEquippedMethod.Invoke(null, new object[] { args[1], true });
                            copiedCount++;
                        }
                        else
                        {
                            // Item not in LoadedItems — cosmetic mod likely not installed locally
                            Main.MelonLog.Warning($"Cosmetic not found in LoadedItems (mod not installed?): {barcode}");
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.MelonLog.Warning($"Failed to equip cosmetic {barcode}: {ex.Message}");
                    }
                }

                Main.MelonLog.Msg($"Cosmetics: {copiedCount} equipped, {skippedCount} skipped (not found), {equippedBarcodes.Count} total");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"CopyCosmeticsFromPlayer error: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the local player's nickname using LabFusion's LocalPlayer.Metadata.Nickname
        /// </summary>
        private static void SetLocalNickname(string nickname)
        {
            try
            {
                // Get LocalPlayer type
                var localPlayerType = FindTypeByName("LocalPlayer");
                if (localPlayerType == null)
                {
                    Main.MelonLog.Warning("LocalPlayer type not found");
                    return;
                }

                // Get Metadata property
                var metadataProp = localPlayerType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
                if (metadataProp == null)
                {
                    Main.MelonLog.Warning("LocalPlayer.Metadata not found");
                    return;
                }

                var metadata = metadataProp.GetValue(null);
                if (metadata == null)
                {
                    Main.MelonLog.Warning("Metadata is null");
                    return;
                }

                // Get Nickname property
                var nicknameProp = metadata.GetType().GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                if (nicknameProp == null)
                {
                    Main.MelonLog.Warning("Metadata.Nickname not found");
                    return;
                }

                var nicknameObj = nicknameProp.GetValue(metadata);
                if (nicknameObj == null)
                {
                    Main.MelonLog.Warning("Nickname object is null");
                    return;
                }

                // Call SetValue(string)
                var setValueMethod = nicknameObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string));

                if (setValueMethod != null)
                {
                    setValueMethod.Invoke(nicknameObj, new object[] { nickname });
                    Main.MelonLog.Msg($"Set local nickname to: {nickname}");
                }
                else
                {
                    Main.MelonLog.Warning("SetValue method not found on Nickname");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to set nickname: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the local player's description using LabFusion's LocalPlayer.Metadata.Description
        /// </summary>
        private static void SetLocalDescription(string description)
        {
            try
            {
                var localPlayerType = FindTypeByName("LocalPlayer");
                if (localPlayerType == null) return;

                var metadataProp = localPlayerType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
                if (metadataProp == null) return;

                var metadata = metadataProp.GetValue(null);
                if (metadata == null) return;

                var descProp = metadata.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                if (descProp == null)
                {
                    Main.MelonLog.Warning("Metadata.Description not found");
                    return;
                }

                var descObj = descProp.GetValue(metadata);
                if (descObj == null)
                {
                    Main.MelonLog.Warning("Description object is null");
                    return;
                }

                var setValueMethod = descObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string));

                if (setValueMethod != null)
                {
                    setValueMethod.Invoke(descObj, new object[] { description });
                    Main.MelonLog.Msg($"Set local description to: {description}");
                }
                else
                {
                    Main.MelonLog.Warning("SetValue method not found on Description");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to set description: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal refresh (no notification spam)
        /// </summary>
        private static void RefreshPlayerListInternal()
        {
            cachedPlayers.Clear();
            currentPlayerIndex = 0;

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

                    if (smallId == localSmallId) continue;

                    string displayName = $"Player {smallId}";
                    string username = "";
                    string nickname = "";
                    string description = "";
                    string avatarBarcode = "";
                    string avatarTitle = "Unknown";
                    List<string> equippedCosmetics = new List<string>();

                    // Get player info from NetworkPlayerManager
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
                                    var networkPlayerType = networkPlayer.GetType();

                                    // Get Username
                                    var usernameProp = networkPlayerType.GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                                    if (usernameProp != null)
                                    {
                                        username = usernameProp.GetValue(networkPlayer) as string ?? "";
                                        if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                                        {
                                            displayName = username;
                                        }
                                    }

                                    // Get AvatarSetter.AvatarBarcode
                                    var avatarSetterProp = networkPlayerType.GetProperty("AvatarSetter", BindingFlags.Public | BindingFlags.Instance);
                                    if (avatarSetterProp != null)
                                    {
                                        var avatarSetter = avatarSetterProp.GetValue(networkPlayer);
                                        if (avatarSetter != null)
                                        {
                                            var barcodeProp = avatarSetter.GetType().GetProperty("AvatarBarcode", BindingFlags.Public | BindingFlags.Instance);
                                            if (barcodeProp != null)
                                            {
                                                avatarBarcode = barcodeProp.GetValue(avatarSetter) as string ?? "";
                                            }
                                        }
                                    }

                                    // Try to get avatar title from the barcode
                                    if (!string.IsNullOrEmpty(avatarBarcode))
                                    {
                                        avatarTitle = GetAvatarTitleFromBarcode(avatarBarcode);
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // Get nickname from PlayerID.Metadata.Nickname
                    try
                    {
                        var metadataProp = playerIdType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Instance);
                        if (metadataProp != null)
                        {
                            var metadata = metadataProp.GetValue(playerIdObj);
                            if (metadata != null)
                            {
                                var nicknameProp = metadata.GetType().GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                                if (nicknameProp != null)
                                {
                                    var nicknameObj = nicknameProp.GetValue(metadata);
                                    if (nicknameObj != null)
                                    {
                                        var getValueMethods = nicknameObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod)
                                            .ToArray();

                                        if (getValueMethods.Length > 0)
                                        {
                                            nickname = getValueMethods[0].Invoke(nicknameObj, null) as string ?? "";
                                        }
                                    }
                                }

                                // Get description from PlayerID.Metadata.Description
                                var descProp = metadata.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                                if (descProp != null)
                                {
                                    var descObj = descProp.GetValue(metadata);
                                    if (descObj != null)
                                    {
                                        var getValueMethods2 = descObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(m => m.Name == "GetValue" && m.GetParameters().Length == 0 && !m.IsGenericMethod)
                                            .ToArray();

                                        if (getValueMethods2.Length > 0)
                                        {
                                            description = getValueMethods2[0].Invoke(descObj, null) as string ?? "";
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // DisplayName priority: nickname first, then username, then fallback
                    if (!string.IsNullOrWhiteSpace(nickname))
                        displayName = nickname;
                    else if (!string.IsNullOrWhiteSpace(username) && username != "No Name")
                        displayName = username;

                    // Get equipped cosmetics from PlayerID.EquippedItems
                    try
                    {
                        var equippedItemsProp = playerIdType.GetProperty("EquippedItems", BindingFlags.Public | BindingFlags.Instance);
                        if (equippedItemsProp != null)
                        {
                            var equippedItems = equippedItemsProp.GetValue(playerIdObj) as System.Collections.IEnumerable;
                            if (equippedItems != null)
                            {
                                foreach (var item in equippedItems)
                                {
                                    var bc = item as string;
                                    if (!string.IsNullOrEmpty(bc))
                                        equippedCosmetics.Add(bc);
                                }
                            }
                        }
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(avatarBarcode))
                    {
                        cachedPlayers.Add(new PlayerAvatarInfo
                        {
                            SmallID = smallId,
                            DisplayName = displayName,
                            Username = username,
                            Nickname = nickname,
                            Description = description,
                            AvatarBarcode = avatarBarcode,
                            AvatarTitle = avatarTitle,
                            PlayerIDObj = playerIdObj,
                            EquippedCosmetics = equippedCosmetics
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"RefreshPlayerListInternal error: {ex.Message}");
            }
        }

        public static void RefreshPlayerList()
        {
            // Delegate to internal method which collects ALL data (nickname, cosmetics, etc.)
            RefreshPlayerListInternal();

            if (cachedPlayers.Count == 0)
            {
                SendNotification(NotificationType.Warning, "Not in multiplayer");
                return;
            }

            Main.MelonLog.Msg($"Found {cachedPlayers.Count} player(s) with avatars");
            SendNotification(NotificationType.Success, $"{cachedPlayers.Count} player(s) found");
            SendNotification(NotificationType.Information, $"Selected: {CurrentPlayerName}");
        }

        private static string GetAvatarTitleFromBarcode(string barcode)
        {
            try
            {
                // Try to get the AvatarCrate and its title
                var avatarCrateRefType = FindTypeByName("AvatarCrateReference");
                if (avatarCrateRefType != null)
                {
                    var ctor = avatarCrateRefType.GetConstructor(new[] { typeof(string) });
                    if (ctor != null)
                    {
                        var crateRef = ctor.Invoke(new object[] { barcode });

                        // Get the Crate property
                        var crateProp = avatarCrateRefType.GetProperty("Crate", BindingFlags.Public | BindingFlags.Instance);
                        if (crateProp != null)
                        {
                            var crate = crateProp.GetValue(crateRef);
                            if (crate != null)
                            {
                                var titleProp = crate.GetType().GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                                if (titleProp != null)
                                {
                                    var title = titleProp.GetValue(crate);
                                    if (title != null)
                                    {
                                        return title.ToString();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Could not get avatar title: {ex.Message}");
            }

            // Fallback: extract from barcode
            int lastDot = barcode.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < barcode.Length)
            {
                var name = barcode.Substring(lastDot + 1);
                // Add spaces between camelCase
                name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
                return name;
            }

            return barcode;
        }

        public static void NextPlayer()
        {
            if (cachedPlayers.Count == 0)
            {
                RefreshPlayerList();
                return;
            }

            currentPlayerIndex = (currentPlayerIndex + 1) % cachedPlayers.Count;
            var player = cachedPlayers[currentPlayerIndex];
            SendNotification(NotificationType.Information, $"{player.DisplayName}\nAvatar: {player.AvatarTitle}");
        }

        public static void PreviousPlayer()
        {
            if (cachedPlayers.Count == 0)
            {
                RefreshPlayerList();
                return;
            }

            currentPlayerIndex--;
            if (currentPlayerIndex < 0)
                currentPlayerIndex = cachedPlayers.Count - 1;

            var player = cachedPlayers[currentPlayerIndex];
            SendNotification(NotificationType.Information, $"{player.DisplayName}\nAvatar: {player.AvatarTitle}");
        }

        public static void CopySelectedAvatar()
        {
            if (cachedPlayers.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No players - Refresh first");
                return;
            }

            if (currentPlayerIndex >= cachedPlayers.Count)
                currentPlayerIndex = 0;

            var targetPlayer = cachedPlayers[currentPlayerIndex];
            CopyAvatarFromPlayer(targetPlayer);
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
