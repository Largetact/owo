using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BonelabUtilityMod
{
    /// <summary>
    /// BodyLog Preset system - Save/load named body log page configurations.
    /// Ported from FusionProtector's BodyLogPage system.
    /// Uses simple pipe-delimited format: Name|Slot1|Slot2|Slot3|Slot4|Slot5|Slot6
    /// </summary>
    public static class BodylogPresetController
    {
        private const string DefaultBarcode = "c3534c5a-94b2-40a4-912a-24a8506f6c79";
        private static readonly string SaveFilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "DooberUtilsBodyLogPresets.txt");

        private static List<BodylogPreset> _presets = new List<BodylogPreset>();

        public static IReadOnlyList<BodylogPreset> Presets => _presets;

        public static void Initialize()
        {
            LoadPresets();
        }

        public static void LoadPresets()
        {
            try
            {
                string loadPath = SaveFilePath;
                if (!File.Exists(loadPath))
                {
                    string bak = SaveFilePath + ".bak";
                    if (File.Exists(bak))
                    {
                        Main.MelonLog.Msg("[BodylogPreset] File missing — restoring from backup");
                        loadPath = bak;
                    }
                    else return;
                }

                _presets.Clear();
                foreach (string line in File.ReadAllLines(loadPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length >= 7)
                    {
                        _presets.Add(new BodylogPreset(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6]));
                    }
                }

                if (loadPath != SaveFilePath)
                    try { File.Copy(loadPath, SaveFilePath, true); } catch { }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[BodylogPreset] Load error: {ex.Message}");
            }
        }

        public static void SavePresets()
        {
            try
            {
                // Backup before write
                if (File.Exists(SaveFilePath))
                    try { File.Copy(SaveFilePath, SaveFilePath + ".bak", true); } catch { }

                var lines = new List<string>();
                foreach (var p in _presets)
                {
                    lines.Add($"{p.Name}|{p.Slot1}|{p.Slot2}|{p.Slot3}|{p.Slot4}|{p.Slot5}|{p.Slot6}");
                }
                File.WriteAllLines(SaveFilePath, lines);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Error($"[BodylogPreset] Save error: {ex.Message}");
            }
        }

        public static void CreatePreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                NotificationHelper.Send(NotificationType.Warning, "Preset name cannot be empty");
                return;
            }

            // Read current bodylog barcodes from the game
            string[] slots = ReadCurrentBodyLogSlots();
            var preset = new BodylogPreset(name, slots[0], slots[1], slots[2], slots[3], slots[4], slots[5]);
            _presets.Add(preset);
            SavePresets();
            NotificationHelper.Send(NotificationType.Success, $"Created preset: {name}");
        }

        public static void ApplyPreset(BodylogPreset preset)
        {
            if (preset == null) return;
            try
            {
                WriteBodyLogSlots(preset);
                NotificationHelper.Send(NotificationType.Success, $"Applied preset: {preset.Name}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[BodylogPreset] Apply error: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Failed to apply: {ex.Message}");
            }
        }

        public static void RemovePreset(string name)
        {
            var preset = _presets.FirstOrDefault(p => p.Name == name);
            if (preset != null)
            {
                _presets.Remove(preset);
                SavePresets();
                NotificationHelper.Send(NotificationType.Success, $"Removed preset: {name}");
            }
        }

        public static void PopulatePresetsPage(Page page)
        {
            try { page?.RemoveAll(); } catch { }
            foreach (var preset in _presets)
            {
                string capturedName = preset.Name;
                var presetPage = page.CreatePage(capturedName, Color.cyan);
                presetPage.CreateFunction("Apply", Color.green, () => ApplyPreset(preset));
                presetPage.CreateFunction("Remove", Color.red, () =>
                {
                    RemovePreset(capturedName);
                });

                // Show slots
                for (int i = 0; i < 6; i++)
                {
                    int slotNum = i + 1;
                    string barcode = preset.GetSlot(slotNum);
                    string display = barcode.Length > 30 ? barcode.Substring(0, 27) + "..." : barcode;
                    presetPage.CreateFunction($"Slot {slotNum}: {display}", Color.white, () => { });
                }
            }
        }

        /// <summary>
        /// Read current body log barcodes from the game's BodyLog system
        /// </summary>
        private static string[] ReadCurrentBodyLogSlots()
        {
            string[] slots = new string[6];
            for (int i = 0; i < 6; i++)
                slots[i] = DefaultBarcode;

            try
            {
                // Try to access BodyLogManager or DataManager to get current bodylog barcodes
                var dataManagerType = FindType("DataManager");
                if (dataManagerType != null)
                {
                    var instanceProp = dataManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        // Try to get ActiveBodyLog data
                        var bodyLogProp = dataManagerType.GetProperty("ActiveBodyLog") ??
                                         dataManagerType.GetProperty("BodyLog");
                        var bodyLog = bodyLogProp?.GetValue(instance);
                        if (bodyLog != null)
                        {
                            // Read slots from bodylog
                            for (int i = 0; i < 6; i++)
                            {
                                var slotProp = bodyLog.GetType().GetProperty($"Slot{i + 1}") ??
                                               bodyLog.GetType().GetProperty($"slot{i + 1}");
                                if (slotProp != null)
                                {
                                    var val = slotProp.GetValue(bodyLog);
                                    if (val != null)
                                        slots[i] = val.ToString();
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return slots;
        }

        /// <summary>
        /// Write bodylog preset barcodes back to the game
        /// </summary>
        private static void WriteBodyLogSlots(BodylogPreset preset)
        {
            try
            {
                var dataManagerType = FindType("DataManager");
                if (dataManagerType == null) return;

                var instanceProp = dataManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null) return;

                var bodyLogProp = dataManagerType.GetProperty("ActiveBodyLog") ??
                                 dataManagerType.GetProperty("BodyLog");
                var bodyLog = bodyLogProp?.GetValue(instance);
                if (bodyLog == null) return;

                for (int i = 0; i < 6; i++)
                {
                    string barcode = preset.GetSlot(i + 1);
                    var slotProp = bodyLog.GetType().GetProperty($"Slot{i + 1}") ??
                                   bodyLog.GetType().GetProperty($"slot{i + 1}");
                    if (slotProp != null && slotProp.CanWrite)
                    {
                        // Need to create the correct barcode type
                        var paramType = slotProp.PropertyType;
                        if (paramType == typeof(string))
                        {
                            slotProp.SetValue(bodyLog, barcode);
                        }
                        else
                        {
                            var ctor = paramType.GetConstructor(new[] { typeof(string) });
                            if (ctor != null)
                                slotProp.SetValue(bodyLog, ctor.Invoke(new object[] { barcode }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[BodylogPreset] WriteBodyLogSlots error: {ex.Message}");
            }
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == name) return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }

    public class BodylogPreset
    {
        public string Name { get; set; }
        public string Slot1 { get; set; }
        public string Slot2 { get; set; }
        public string Slot3 { get; set; }
        public string Slot4 { get; set; }
        public string Slot5 { get; set; }
        public string Slot6 { get; set; }

        public BodylogPreset(string Name, string Slot1, string Slot2, string Slot3, string Slot4, string Slot5, string Slot6)
        {
            this.Name = Name;
            this.Slot1 = Slot1;
            this.Slot2 = Slot2;
            this.Slot3 = Slot3;
            this.Slot4 = Slot4;
            this.Slot5 = Slot5;
            this.Slot6 = Slot6;
        }

        public string GetSlot(int num)
        {
            switch (num)
            {
                case 1: return Slot1;
                case 2: return Slot2;
                case 3: return Slot3;
                case 4: return Slot4;
                case 5: return Slot5;
                case 6: return Slot6;
                default: return "c3534c5a-94b2-40a4-912a-24a8506f6c79";
            }
        }

        public void SetSlot(int num, string barcode)
        {
            switch (num)
            {
                case 1: Slot1 = barcode; break;
                case 2: Slot2 = barcode; break;
                case 3: Slot3 = barcode; break;
                case 4: Slot4 = barcode; break;
                case 5: Slot5 = barcode; break;
                case 6: Slot6 = barcode; break;
            }
        }
    }
}
