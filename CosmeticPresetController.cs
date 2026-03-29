using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Save/load fusion cosmetic presets.
    /// Uses reflection for LabFusion PointItemManager/PointSaveManager.
    /// </summary>
    public static class CosmeticPresetController
    {
        // ═══════════════════════════════════════════════════
        // PRESET DATA
        // ═══════════════════════════════════════════════════
        public struct CosmeticPreset
        {
            public string Name;
            public List<string> Barcodes;
        }

        private static List<CosmeticPreset> _presets = new List<CosmeticPreset>();
        private static int _maxPresets = 20;

        public static List<CosmeticPreset> Presets => _presets;

        // ═══════════════════════════════════════════════════
        // SAVE CURRENT COSMETICS
        // ═══════════════════════════════════════════════════
        public static void SaveCurrentAsPreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                NotificationHelper.Send(NotificationType.Warning, "Enter a preset name first");
                return;
            }

            if (_presets.Count >= _maxPresets)
            {
                NotificationHelper.Send(NotificationType.Warning, $"Max {_maxPresets} presets reached");
                return;
            }

            var barcodes = GetEquippedBarcodes();
            if (barcodes.Count == 0)
            {
                NotificationHelper.Send(NotificationType.Warning, "No cosmetics currently equipped");
                return;
            }

            _presets.Add(new CosmeticPreset { Name = name, Barcodes = barcodes });
            SettingsManager.MarkDirty();
            NotificationHelper.Send(NotificationType.Success, $"Saved preset '{name}' ({barcodes.Count} items)");
        }

        // ═══════════════════════════════════════════════════
        // APPLY PRESET
        // ═══════════════════════════════════════════════════
        public static void ApplyPreset(int index)
        {
            if (index < 0 || index >= _presets.Count)
            {
                NotificationHelper.Send(NotificationType.Error, "Invalid preset index");
                return;
            }

            var preset = _presets[index];
            MelonCoroutines.Start(ApplyPresetCoroutine(preset));
        }

        private static IEnumerator ApplyPresetCoroutine(CosmeticPreset preset)
        {
            NotificationHelper.Send(NotificationType.Information, $"Applying preset '{preset.Name}'...");

            var pointItemManagerType = FindTypeByName("PointItemManager");
            var pointSaveManagerType = FindTypeByName("PointSaveManager");
            if (pointItemManagerType == null || pointSaveManagerType == null)
            {
                NotificationHelper.Send(NotificationType.Error, "Fusion PointItemManager not found");
                yield break;
            }

            var tryGetMethod = pointItemManagerType.GetMethod("TryGetPointItem", BindingFlags.Public | BindingFlags.Static);
            var setEquippedMethod = pointItemManagerType.GetMethod("SetEquipped", BindingFlags.Public | BindingFlags.Static);
            var unlockMethod = pointSaveManagerType.GetMethod("UnlockItem", BindingFlags.Public | BindingFlags.Static);
            var loadedItemsProp = pointItemManagerType.GetProperty("LoadedItems", BindingFlags.Public | BindingFlags.Static);

            if (tryGetMethod == null || setEquippedMethod == null)
            {
                NotificationHelper.Send(NotificationType.Error, "PointItemManager methods not found");
                yield break;
            }

            // Unequip all current cosmetics
            var loadedItems = loadedItemsProp?.GetValue(null) as System.Collections.IEnumerable;
            if (loadedItems != null)
            {
                foreach (var item in loadedItems)
                {
                    try
                    {
                        var isEquippedProp = item.GetType().GetProperty("IsEquipped", BindingFlags.Public | BindingFlags.Instance);
                        if (isEquippedProp != null && (bool)isEquippedProp.GetValue(item))
                        {
                            setEquippedMethod.Invoke(null, new object[] { item, false });
                        }
                    }
                    catch { }
                }
            }

            yield return new WaitForSeconds(0.2f);

            // Equip preset barcodes
            int equipped = 0;
            foreach (var barcode in preset.Barcodes)
            {
                try
                {
                    if (unlockMethod != null)
                        unlockMethod.Invoke(null, new object[] { barcode });

                    var args = new object[] { barcode, null };
                    if ((bool)tryGetMethod.Invoke(null, args) && args[1] != null)
                    {
                        setEquippedMethod.Invoke(null, new object[] { args[1], true });
                        equipped++;
                    }
                }
                catch { }
            }

            NotificationHelper.Send(NotificationType.Success, $"Applied '{preset.Name}' ({equipped}/{preset.Barcodes.Count})");
        }

        // ═══════════════════════════════════════════════════
        // DELETE PRESET
        // ═══════════════════════════════════════════════════
        public static void DeletePreset(int index)
        {
            if (index < 0 || index >= _presets.Count) return;
            string name = _presets[index].Name;
            _presets.RemoveAt(index);
            SettingsManager.MarkDirty();
            NotificationHelper.Send(NotificationType.Success, $"Deleted preset '{name}'");
        }

        // ═══════════════════════════════════════════════════
        // GET CURRENTLY EQUIPPED COSMETICS
        // ═══════════════════════════════════════════════════
        private static List<string> GetEquippedBarcodes()
        {
            var result = new List<string>();
            try
            {
                var pointItemManagerType = FindTypeByName("PointItemManager");
                if (pointItemManagerType == null) return result;

                var loadedItemsProp = pointItemManagerType.GetProperty("LoadedItems", BindingFlags.Public | BindingFlags.Static);
                var loadedItems = loadedItemsProp?.GetValue(null) as System.Collections.IEnumerable;
                if (loadedItems == null) return result;

                foreach (var item in loadedItems)
                {
                    var isEquippedProp = item.GetType().GetProperty("IsEquipped", BindingFlags.Public | BindingFlags.Instance);
                    if (isEquippedProp != null && (bool)isEquippedProp.GetValue(item))
                    {
                        var barcodeProp = item.GetType().GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                        var barcode = barcodeProp?.GetValue(item)?.ToString();
                        if (!string.IsNullOrEmpty(barcode))
                            result.Add(barcode);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[CosmeticPreset] Get equipped error: {ex.Message}");
            }
            return result;
        }

        // ═══════════════════════════════════════════════════
        // POPULATE MENU
        // ═══════════════════════════════════════════════════
        public static void PopulatePresetsPage(Page page)
        {
            try { page.RemoveAll(); } catch { }

            if (_presets.Count == 0)
            {
                page.CreateFunction("(No presets saved)", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _presets.Count; i++)
            {
                int idx = i;
                var preset = _presets[i];
                var presetPage = page.CreatePage($"{preset.Name} ({preset.Barcodes.Count})", Color.green);
                presetPage.CreateFunction("Apply", Color.green, () => ApplyPreset(idx));
                presetPage.CreateFunction("Delete", Color.red, () =>
                {
                    DeletePreset(idx);
                    PopulatePresetsPage(page);
                });

                // Show barcodes
                foreach (var bc in preset.Barcodes)
                {
                    presetPage.CreateFunction(bc.Length > 40 ? bc.Substring(0, 40) + "..." : bc, Color.white, () =>
                    {
                        GUIUtility.systemCopyBuffer = bc;
                        NotificationHelper.Send(NotificationType.Success, "Barcode copied");
                    });
                }
            }
        }

        // ═══════════════════════════════════════════════════
        // PERSISTENCE — encode/decode presets for SettingsManager
        // ═══════════════════════════════════════════════════
        public static string SerializePresets()
        {
            // Format: name|barcode1,barcode2;name|barcode1,barcode2
            var parts = new List<string>();
            foreach (var p in _presets)
            {
                string barcodes = string.Join(",", p.Barcodes);
                parts.Add($"{p.Name}|{barcodes}");
            }
            return string.Join(";", parts);
        }

        public static void DeserializePresets(string data)
        {
            _presets.Clear();
            if (string.IsNullOrEmpty(data)) return;

            var entries = data.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                int sep = entry.IndexOf('|');
                if (sep < 0) continue;

                string name = entry.Substring(0, sep);
                string barcodesStr = entry.Substring(sep + 1);
                var barcodes = new List<string>();
                foreach (var bc in barcodesStr.Split(','))
                {
                    if (!string.IsNullOrWhiteSpace(bc))
                        barcodes.Add(bc.Trim());
                }

                if (barcodes.Count > 0)
                    _presets.Add(new CosmeticPreset { Name = name, Barcodes = barcodes });
            }
        }

        // ═══════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════
        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                        if (t.Name == typeName) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
