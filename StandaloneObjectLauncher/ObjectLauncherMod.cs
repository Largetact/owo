using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;

[assembly: MelonInfo(typeof(StandaloneObjectLauncher.ObjectLauncherMod), "Object Launcher", "1.0.0", "DOOBER")]
[assembly: MelonColor(255, 255, 0, 255)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace StandaloneObjectLauncher
{
    // ════════════════════════════════════════════════════════════════
    //  MelonMod entry point
    // ════════════════════════════════════════════════════════════════
    public class ObjectLauncherMod : MelonMod
    {
        private static MelonPreferences_Category _prefCategory;
        private static MelonPreferences_Entry<string> _prefPresets;
        private static MelonPreferences_Entry<string> _prefBarcode;
        private static MelonPreferences_Entry<string> _prefItemName;
        private static MelonPreferences_Entry<float> _prefLaunchForce;
        private static MelonPreferences_Entry<float> _prefSpawnDist;
        private static MelonPreferences_Entry<bool> _prefSafety;
        private static MelonPreferences_Entry<bool> _prefLeftHand;
        private static MelonPreferences_Entry<bool> _prefFullAuto;
        private static MelonPreferences_Entry<float> _prefFullAutoDelay;
        private static MelonPreferences_Entry<bool> _prefTrajectory;
        private static MelonPreferences_Entry<float> _prefSpawnScale;
        private static MelonPreferences_Entry<float> _prefSpinVelocity;
        private static MelonPreferences_Entry<float> _prefForceDelay;
        private static MelonPreferences_Entry<bool> _prefPreActivate;
        private static MelonPreferences_Entry<bool> _prefAutoCleanup;
        private static MelonPreferences_Entry<float> _prefAutoCleanupInterval;
        private static MelonPreferences_Entry<bool> _prefAutoDespawn;
        private static MelonPreferences_Entry<float> _prefAutoDespawnDelay;

        public override void OnInitializeMelon()
        {
            _prefCategory = MelonPreferences.CreateCategory("ObjectLauncher");
            _prefPresets = _prefCategory.CreateEntry("Presets", "");
            _prefBarcode = _prefCategory.CreateEntry("Barcode", ObjectLauncherController.CurrentBarcodeID);
            _prefItemName = _prefCategory.CreateEntry("ItemName", ObjectLauncherController.CurrentItemName);
            _prefLaunchForce = _prefCategory.CreateEntry("LaunchForce", 50f);
            _prefSpawnDist = _prefCategory.CreateEntry("SpawnDistance", 1f);
            _prefSafety = _prefCategory.CreateEntry("Safety", true);
            _prefLeftHand = _prefCategory.CreateEntry("LeftHand", false);
            _prefFullAuto = _prefCategory.CreateEntry("FullAuto", false);
            _prefFullAutoDelay = _prefCategory.CreateEntry("FullAutoDelay", 0.15f);
            _prefTrajectory = _prefCategory.CreateEntry("Trajectory", true);
            _prefSpawnScale = _prefCategory.CreateEntry("SpawnScale", 1f);
            _prefSpinVelocity = _prefCategory.CreateEntry("SpinVelocity", 0f);
            _prefForceDelay = _prefCategory.CreateEntry("ForceDelay", 0.02f);
            _prefPreActivate = _prefCategory.CreateEntry("PreActivateMenuTap", false);
            _prefAutoCleanup = _prefCategory.CreateEntry("AutoCleanup", false);
            _prefAutoCleanupInterval = _prefCategory.CreateEntry("AutoCleanupInterval", 30f);
            _prefAutoDespawn = _prefCategory.CreateEntry("AutoDespawn", false);
            _prefAutoDespawnDelay = _prefCategory.CreateEntry("AutoDespawnDelay", 10f);

            // Load saved settings
            ObjectLauncherController.CurrentBarcodeID = _prefBarcode.Value;
            ObjectLauncherController.CurrentItemName = _prefItemName.Value;
            ObjectLauncherController.LaunchForce = _prefLaunchForce.Value;
            ObjectLauncherController.SpawnDistance = _prefSpawnDist.Value;
            ObjectLauncherController.SafetyEnabled = _prefSafety.Value;
            ObjectLauncherController.UseLeftHand = _prefLeftHand.Value;
            ObjectLauncherController.IsFullAuto = _prefFullAuto.Value;
            ObjectLauncherController.FullAutoDelay = _prefFullAutoDelay.Value;
            ObjectLauncherController.ShowTrajectory = _prefTrajectory.Value;
            ObjectLauncherController.SpawnScale = _prefSpawnScale.Value;
            ObjectLauncherController.SpinVelocity = _prefSpinVelocity.Value;
            ObjectLauncherController.ForceDelay = _prefForceDelay.Value;
            ObjectLauncherController.PreActivateMenuTap = _prefPreActivate.Value;
            ObjectLauncherController.AutoCleanupEnabled = _prefAutoCleanup.Value;
            ObjectLauncherController.AutoCleanupInterval = _prefAutoCleanupInterval.Value;
            ObjectLauncherController.AutoDespawnEnabled = _prefAutoDespawn.Value;
            ObjectLauncherController.AutoDespawnDelay = _prefAutoDespawnDelay.Value;
            ObjectLauncherController.DeserializePresets(_prefPresets.Value);

            ObjectLauncherController.Initialize();
            LoggerInstance.Msg("Object Launcher mod loaded");
        }

        public override void OnLateInitializeMelon()
        {
            BuildMenu();
        }

        public override void OnUpdate()
        {
            ObjectLauncherController.Update();
            LauncherOverlay.CheckInput();
        }

        public override void OnGUI()
        {
            LauncherOverlay.Draw();
        }

        public override void OnApplicationQuit()
        {
            SavePreferences();
        }

        public static void SavePreferences()
        {
            if (_prefCategory == null) return;
            _prefPresets.Value = ObjectLauncherController.SerializePresets();
            _prefBarcode.Value = ObjectLauncherController.CurrentBarcodeID;
            _prefItemName.Value = ObjectLauncherController.CurrentItemName;
            _prefLaunchForce.Value = ObjectLauncherController.LaunchForce;
            _prefSpawnDist.Value = ObjectLauncherController.SpawnDistance;
            _prefSafety.Value = ObjectLauncherController.SafetyEnabled;
            _prefLeftHand.Value = ObjectLauncherController.UseLeftHand;
            _prefFullAuto.Value = ObjectLauncherController.IsFullAuto;
            _prefFullAutoDelay.Value = ObjectLauncherController.FullAutoDelay;
            _prefTrajectory.Value = ObjectLauncherController.ShowTrajectory;
            _prefSpawnScale.Value = ObjectLauncherController.SpawnScale;
            _prefSpinVelocity.Value = ObjectLauncherController.SpinVelocity;
            _prefForceDelay.Value = ObjectLauncherController.ForceDelay;
            _prefPreActivate.Value = ObjectLauncherController.PreActivateMenuTap;
            _prefAutoCleanup.Value = ObjectLauncherController.AutoCleanupEnabled;
            _prefAutoCleanupInterval.Value = ObjectLauncherController.AutoCleanupInterval;
            _prefAutoDespawn.Value = ObjectLauncherController.AutoDespawnEnabled;
            _prefAutoDespawnDelay.Value = ObjectLauncherController.AutoDespawnDelay;
            _prefCategory.SaveToFile();
        }

        // ────────────────────────────────────────────
        //  BoneMenu
        // ────────────────────────────────────────────
        private static Page _presetLoadPage;
        private static Page _presetDeletePage;

        private void BuildMenu()
        {
            var root = Page.Root.CreatePage("Object Launcher", Color.cyan);

            root.CreateBool("Enabled", Color.white, ObjectLauncherController.IsLauncherEnabled,
                v => ObjectLauncherController.IsLauncherEnabled = v);
            root.CreateBool("Safety (Grip+Trigger)", Color.red, ObjectLauncherController.SafetyEnabled,
                v => ObjectLauncherController.SafetyEnabled = v);
            root.CreateBool("Left Hand", Color.yellow, ObjectLauncherController.UseLeftHand,
                v => ObjectLauncherController.UseLeftHand = v);
            root.CreateBool("Full-Auto", Color.yellow, ObjectLauncherController.IsFullAuto,
                v => ObjectLauncherController.IsFullAuto = v);
            root.CreateBool("Show Trajectory", Color.white, ObjectLauncherController.ShowTrajectory,
                v => ObjectLauncherController.ShowTrajectory = v);
            root.CreateFloat("Full-Auto Delay", Color.yellow, ObjectLauncherController.FullAutoDelay,
                0.01f, 0.01f, 1f, v => ObjectLauncherController.FullAutoDelay = v);
            root.CreateFloat("Launch Force", Color.yellow, ObjectLauncherController.LaunchForce,
                50f, 50f, 10000f, v => ObjectLauncherController.LaunchForce = v);
            root.CreateFloat("Spawn Distance", Color.cyan, ObjectLauncherController.SpawnDistance,
                0.5f, 0.5f, 10f, v => ObjectLauncherController.SpawnDistance = v);
            root.CreateFloat("Spawn Offset X", Color.cyan, ObjectLauncherController.SpawnOffsetX,
                0.5f, -10f, 10f, v => ObjectLauncherController.SpawnOffsetX = v);
            root.CreateFloat("Spawn Offset Y", Color.cyan, ObjectLauncherController.SpawnOffsetY,
                0.5f, -10f, 10f, v => ObjectLauncherController.SpawnOffsetY = v);
            root.CreateFloat("Projectile Count", Color.magenta, ObjectLauncherController.ProjectileCount,
                1f, 1f, 25f, v => ObjectLauncherController.ProjectileCount = (int)v);
            root.CreateFloat("Projectile Spacing", Color.magenta, ObjectLauncherController.ProjectileSpacing,
                0.1f, 0.1f, 100f, v => ObjectLauncherController.ProjectileSpacing = v);
            root.CreateFloat("Spin Velocity", Color.green, ObjectLauncherController.SpinVelocity,
                5f, 0f, 5000f, v => ObjectLauncherController.SpinVelocity = v);
            root.CreateFloat("Spawn Scale", Color.magenta, ObjectLauncherController.SpawnScale,
                0.1f, 0.1f, 10f, v => ObjectLauncherController.SpawnScale = v);
            root.CreateBool("Aim Rotation", Color.green, ObjectLauncherController.AimRotationEnabled,
                v => ObjectLauncherController.AimRotationEnabled = v);
            root.CreateBool("Pre-Activate (Menu Tap)", Color.magenta, ObjectLauncherController.PreActivateMenuTap,
                v => ObjectLauncherController.PreActivateMenuTap = v);
            root.CreateFloat("Force Delay", Color.cyan, ObjectLauncherController.ForceDelay,
                0.01f, 0f, 2f, v => ObjectLauncherController.ForceDelay = v);

            // Auto-Cleanup sub-page
            var cleanupPage = root.CreatePage("Auto-Cleanup", Color.yellow);
            cleanupPage.CreateBool("Auto-Cleanup Enabled", Color.yellow, ObjectLauncherController.AutoCleanupEnabled,
                v => ObjectLauncherController.AutoCleanupEnabled = v);
            cleanupPage.CreateFloat("Cleanup Interval (s)", Color.yellow, ObjectLauncherController.AutoCleanupInterval,
                5f, 1f, 300f, v => ObjectLauncherController.AutoCleanupInterval = v);
            cleanupPage.CreateBool("Auto-Despawn Enabled", Color.cyan, ObjectLauncherController.AutoDespawnEnabled,
                v => ObjectLauncherController.AutoDespawnEnabled = v);
            cleanupPage.CreateFloat("Despawn Delay (s)", Color.cyan, ObjectLauncherController.AutoDespawnDelay,
                1f, 1f, 300f, v => ObjectLauncherController.AutoDespawnDelay = v);
            cleanupPage.CreateFunction("Despawn All Launched", Color.red, () => ObjectLauncherController.DespawnLaunchedObjects());

            // Rotation sub-page
            var rotPage = root.CreatePage("Rotation", Color.green);
            rotPage.CreateFloat("Rotation X", Color.red, ObjectLauncherController.RotationX,
                15f, -180f, 180f, v => ObjectLauncherController.RotationX = v);
            rotPage.CreateFloat("Rotation Y", Color.green, ObjectLauncherController.RotationY,
                15f, -180f, 180f, v => ObjectLauncherController.RotationY = v);
            rotPage.CreateFloat("Rotation Z", Color.blue, ObjectLauncherController.RotationZ,
                15f, -180f, 180f, v => ObjectLauncherController.RotationZ = v);
            rotPage.CreateFunction("Reset Rotation", Color.white, () =>
            {
                ObjectLauncherController.RotationX = 0f;
                ObjectLauncherController.RotationY = 0f;
                ObjectLauncherController.RotationZ = 0f;
            });

            // Spawn Search sub-page
            var searchPage = root.CreatePage("Spawn Search", Color.yellow);
            var searchResults = searchPage.CreatePage("+ Results", Color.yellow);
            searchPage.CreateFunction("Refresh Items", Color.green, () => ObjectLauncherController.RefreshSearchList());
            searchPage.CreateString("Search", Color.white, "",
                v => ObjectLauncherController.SearchQuery = v);
            searchPage.CreateFunction("Next", Color.yellow, () => ObjectLauncherController.NextSearchItem());
            searchPage.CreateFunction("Previous", Color.yellow, () => ObjectLauncherController.PreviousSearchItem());
            searchPage.CreateFunction("Select Item", Color.green, () => ObjectLauncherController.CopyBarcodeFromSearch());

            root.CreateFunction("Add Item (Left Hand)", Color.green, () => ObjectLauncherController.AddItemFromLeftHand());
            root.CreateFunction("Launch!", Color.red, () => ObjectLauncherController.LaunchObject());

            // Presets sub-page
            var presetPage = root.CreatePage("Presets", Color.magenta);
            _presetLoadPage = presetPage.CreatePage("+ Load Preset", Color.green);
            _presetDeletePage = presetPage.CreatePage("+ Delete Preset", Color.red);
            presetPage.CreateString("Preset Name", Color.white, ObjectLauncherController.PresetName,
                v => ObjectLauncherController.PresetName = v);
            presetPage.CreateFunction("Save Current Settings", Color.green, () =>
            {
                ObjectLauncherController.SavePreset(ObjectLauncherController.PresetName);
                ObjectLauncherController.PopulatePresetLoadPage(_presetLoadPage);
                ObjectLauncherController.PopulatePresetDeletePage(_presetDeletePage);
                SavePreferences();
            });
            presetPage.CreateFunction("Refresh Preset List", Color.yellow, () =>
            {
                ObjectLauncherController.PopulatePresetLoadPage(_presetLoadPage);
                ObjectLauncherController.PopulatePresetDeletePage(_presetDeletePage);
            });
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  IMGUI Overlay
    // ════════════════════════════════════════════════════════════════
    public static class LauncherOverlay
    {
        private static bool _visible = false;
        private static int _currentTab = 0;
        private static float _scroll = 0f;
        private static string _overlaySearchQuery = "";

        private static readonly string[] TabNames = new string[]
        {
            "Settings",
            "Advanced",
            "Search",
            "Presets"
        };

        private const int WIN_W = 500;
        private const int WIN_H = 560;
        private const int TAB_H = 30;
        private const float ROW = 28f;
        private const float SLD = 20f;
        private const float PAD = 12f;

        public static bool IsVisible => _visible;

        public static void CheckInput()
        {
            if (Input.GetKeyDown(KeyCode.F3))
                _visible = !_visible;
        }

        public static void Draw()
        {
            if (!_visible) return;

            if (Event.current != null && Event.current.type == EventType.ScrollWheel)
            {
                _scroll += Event.current.delta.y * 30f;
                if (_scroll < 0f) _scroll = 0f;
            }

            int x = (Screen.width - WIN_W) / 2;
            int y = (Screen.height - WIN_H) / 2;

            // Dark background
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x - 2, y - 2, WIN_W + 4, WIN_H + 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Window(55800, new Rect(x, y, WIN_W, WIN_H), (Action<int>)DrawWindow, "Object Launcher  |  F3 Close  |  Scroll");
            GUI.BringWindowToFront(55800);
        }

        private static void DrawWindow(int id)
        {
            float y = 22f;
            float w = WIN_W - PAD * 2f;

            // Tab bar
            float tabW = w / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                GUI.color = (i == _currentTab) ? Color.cyan : Color.gray;
                if (GUI.Button(new Rect(PAD + i * tabW, y, tabW - 2f, TAB_H), TabNames[i]))
                {
                    _currentTab = i;
                    _scroll = 0f;
                }
            }
            GUI.color = Color.white;
            y += TAB_H + 6f;

            // Content area (scrollable via _scroll)
            float contentY = y - _scroll;
            switch (_currentTab)
            {
                case 0: DrawSettingsTab(contentY, w); break;
                case 1: DrawAdvancedTab(contentY, w); break;
                case 2: DrawSearchTab(contentY, w); break;
                case 3: DrawPresetsTab(contentY, w); break;
            }
        }

        // ── Helpers ──
        private static float Header(string text, float y, float w)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            s.normal.textColor = Color.cyan;
            GUI.Label(new Rect(PAD, y, w, 28f), text, s);
            return y + 28f;
        }

        private static float Label(string text, float y, float w)
        {
            GUI.Label(new Rect(PAD, y, w, ROW), text);
            return y + ROW;
        }

        private static float Toggle(ref bool value, string label, float y, float w)
        {
            bool nv = GUI.Toggle(new Rect(PAD, y, w, ROW), value, "  " + label);
            value = nv;
            return y + ROW;
        }

        private static float Slider(string label, ref float value, float min, float max, float y, float w, string fmt = "F1")
        {
            GUI.Label(new Rect(PAD, y, w, 20f), label + ": " + value.ToString(fmt));
            y += 20f;
            float nv = GUI.HorizontalSlider(new Rect(PAD, y, w - 20f, SLD), value, min, max);
            value = nv;
            return y + SLD + 4f;
        }

        private static float Button(string label, float y, float bw, float bh, Action onClick)
        {
            if (GUI.Button(new Rect(PAD, y, bw, bh), label))
                onClick?.Invoke();
            return y + bh + 4f;
        }

        private static float Gap(float y, float g = 10f) { return y + g; }

        // ── Settings Tab ──
        private static void DrawSettingsTab(float y, float w)
        {
            y = Header("═══ SETTINGS ═══", y, w);

            bool oe = ObjectLauncherController.IsLauncherEnabled;
            y = Toggle(ref oe, "Enabled", y, w);
            ObjectLauncherController.IsLauncherEnabled = oe;

            y = Label("Item: " + ObjectLauncherController.CurrentItemName, y, w);

            float of1 = ObjectLauncherController.LaunchForce;
            y = Slider("Launch Force", ref of1, 50f, 10000f, y, w, "F0");
            ObjectLauncherController.LaunchForce = of1;

            float od = ObjectLauncherController.SpawnDistance;
            y = Slider("Spawn Distance", ref od, 0.5f, 10f, y, w);
            ObjectLauncherController.SpawnDistance = od;

            bool ol = ObjectLauncherController.UseLeftHand;
            y = Toggle(ref ol, "Left Hand", y, w);
            ObjectLauncherController.UseLeftHand = ol;

            bool oa = ObjectLauncherController.IsFullAuto;
            y = Toggle(ref oa, "Full-Auto", y, w);
            ObjectLauncherController.IsFullAuto = oa;

            float fad = ObjectLauncherController.FullAutoDelay;
            y = Slider("Full-Auto Delay", ref fad, 0.01f, 1f, y, w, "F2");
            ObjectLauncherController.FullAutoDelay = fad;

            bool os = ObjectLauncherController.SafetyEnabled;
            y = Toggle(ref os, "Safety (Grip+Trigger)", y, w);
            ObjectLauncherController.SafetyEnabled = os;

            bool ot = ObjectLauncherController.ShowTrajectory;
            y = Toggle(ref ot, "Show Trajectory", y, w);
            ObjectLauncherController.ShowTrajectory = ot;

            y = Gap(y);
            y = Button("Add Item (Left Hand)", y, 200f, 28f, () => ObjectLauncherController.AddItemFromLeftHand());
            y = Button("Launch!", y, 200f, 28f, () => ObjectLauncherController.LaunchObject());
        }

        // ── Advanced Tab ──
        private static void DrawAdvancedTab(float y, float w)
        {
            y = Header("═══ ADVANCED ═══", y, w);

            float pc = ObjectLauncherController.ProjectileCount;
            y = Slider("Projectile Count", ref pc, 1f, 25f, y, w, "F0");
            ObjectLauncherController.ProjectileCount = (int)pc;

            float ps = ObjectLauncherController.ProjectileSpacing;
            y = Slider("Projectile Spacing", ref ps, 0.1f, 100f, y, w);
            ObjectLauncherController.ProjectileSpacing = ps;

            float sv = ObjectLauncherController.SpinVelocity;
            y = Slider("Spin Velocity", ref sv, 0f, 5000f, y, w, "F0");
            ObjectLauncherController.SpinVelocity = sv;

            float rx = ObjectLauncherController.RotationX;
            y = Slider("Rotation X", ref rx, -180f, 180f, y, w);
            ObjectLauncherController.RotationX = rx;

            float ry = ObjectLauncherController.RotationY;
            y = Slider("Rotation Y", ref ry, -180f, 180f, y, w);
            ObjectLauncherController.RotationY = ry;

            float rz = ObjectLauncherController.RotationZ;
            y = Slider("Rotation Z", ref rz, -180f, 180f, y, w);
            ObjectLauncherController.RotationZ = rz;

            float ox = ObjectLauncherController.SpawnOffsetX;
            y = Slider("Offset X", ref ox, -10f, 10f, y, w);
            ObjectLauncherController.SpawnOffsetX = ox;

            float oy = ObjectLauncherController.SpawnOffsetY;
            y = Slider("Offset Y", ref oy, -10f, 10f, y, w);
            ObjectLauncherController.SpawnOffsetY = oy;

            float sc = ObjectLauncherController.SpawnScale;
            y = Slider("Scale", ref sc, 0.1f, 10f, y, w);
            ObjectLauncherController.SpawnScale = sc;

            bool ar = ObjectLauncherController.AimRotationEnabled;
            y = Toggle(ref ar, "Aim Rotation (Face Launch Dir)", y, w);
            ObjectLauncherController.AimRotationEnabled = ar;

            bool pa = ObjectLauncherController.PreActivateMenuTap;
            y = Toggle(ref pa, "Pre-Activate (Menu Tap)", y, w);
            ObjectLauncherController.PreActivateMenuTap = pa;

            float fd = ObjectLauncherController.ForceDelay;
            y = Slider("Force Delay", ref fd, 0f, 2f, y, w, "F3");
            ObjectLauncherController.ForceDelay = fd;

            y = Gap(y);
            y = Header("── Auto-Cleanup ──", y, w);

            bool ace = ObjectLauncherController.AutoCleanupEnabled;
            y = Toggle(ref ace, "Auto-Cleanup Enabled", y, w);
            ObjectLauncherController.AutoCleanupEnabled = ace;

            float aci = ObjectLauncherController.AutoCleanupInterval;
            y = Slider("Cleanup Interval (s)", ref aci, 1f, 300f, y, w, "F0");
            ObjectLauncherController.AutoCleanupInterval = aci;

            bool ade = ObjectLauncherController.AutoDespawnEnabled;
            y = Toggle(ref ade, "Auto-Despawn Per Object", y, w);
            ObjectLauncherController.AutoDespawnEnabled = ade;

            float add = ObjectLauncherController.AutoDespawnDelay;
            y = Slider("Despawn Delay (s)", ref add, 1f, 300f, y, w, "F0");
            ObjectLauncherController.AutoDespawnDelay = add;

            y = Button("Despawn All Launched", y, 200f, 28f, () => ObjectLauncherController.DespawnLaunchedObjects());
        }

        // ── Search Tab ──
        private static void DrawSearchTab(float y, float w)
        {
            y = Header("═══ SEARCH ═══", y, w);

            y = Button("Refresh Item List", y, 200f, 28f, () => ObjectLauncherController.RefreshSearchList());

            GUI.Label(new Rect(PAD, y, 60f, ROW), "Search:");
            _overlaySearchQuery = GUI.TextField(new Rect(PAD + 65f, y, w - 65f, ROW), _overlaySearchQuery ?? "");
            y += ROW + 4f;

            y = Button("Apply Filter", y, 160f, 26f, () => ObjectLauncherController.SearchQuery = _overlaySearchQuery);

            y = Gap(y);

            var items = ObjectLauncherController.GetFilteredItems();
            int idx = ObjectLauncherController.SelectedSearchIndex;
            y = Label($"Results: {items.Count}  |  Selected: {(items.Count > 0 ? (idx + 1).ToString() : "-")}", y, w);

            // Show up to 12 items around current selection
            int showStart = Mathf.Max(0, idx - 5);
            int showEnd = Mathf.Min(items.Count, showStart + 12);
            for (int i = showStart; i < showEnd; i++)
            {
                var item = items[i];
                GUI.color = (i == idx) ? Color.green : Color.white;
                string prefix = (i == idx) ? ">> " : "   ";
                if (GUI.Button(new Rect(PAD, y, w, 24f), prefix + item.Title))
                {
                    ObjectLauncherController.SelectedSearchIndex = i;
                }
                y += 26f;
            }
            GUI.color = Color.white;

            y = Gap(y);
            float halfW = (w - 10f) / 3f;
            if (GUI.Button(new Rect(PAD, y, halfW, ROW), "< Prev"))
                ObjectLauncherController.PreviousSearchItem();
            if (GUI.Button(new Rect(PAD + halfW + 5f, y, halfW, ROW), "Select"))
                ObjectLauncherController.CopyBarcodeFromSearch();
            if (GUI.Button(new Rect(PAD + (halfW + 5f) * 2f, y, halfW, ROW), "Next >"))
                ObjectLauncherController.NextSearchItem();
        }

        // ── Presets Tab ──
        private static string _presetNameField = "";

        private static void DrawPresetsTab(float y, float w)
        {
            y = Header("═══ PRESETS ═══", y, w);

            GUI.Label(new Rect(PAD, y, 80f, ROW), "Name:");
            _presetNameField = GUI.TextField(new Rect(PAD + 85f, y, w - 85f, ROW), _presetNameField ?? "");
            y += ROW + 4f;

            y = Button("Save Preset", y, 200f, 28f, () =>
            {
                ObjectLauncherController.PresetName = _presetNameField;
                ObjectLauncherController.SavePreset(_presetNameField);
                ObjectLauncherMod.SavePreferences();
            });

            y = Gap(y);
            y = Label("Saved Presets:", y, w);

            var presets = ObjectLauncherController.GetPresets();
            if (presets.Count == 0)
            {
                y = Label("  (none)", y, w);
            }
            else
            {
                for (int i = 0; i < presets.Count; i++)
                {
                    var p = presets[i];
                    string display = $"{p.Name} ({p.ItemName}, F:{p.LaunchForce:F0})";
                    float btnW = (w - 10f) * 0.7f;
                    float delW = (w - 10f) * 0.25f;

                    if (GUI.Button(new Rect(PAD, y, btnW, 26f), display))
                    {
                        ObjectLauncherController.ApplyPreset(p);
                    }
                    GUI.color = Color.red;
                    if (GUI.Button(new Rect(PAD + btnW + 10f, y, delW, 26f), "Delete"))
                    {
                        ObjectLauncherController.DeletePreset(p.Name);
                        ObjectLauncherMod.SavePreferences();
                    }
                    GUI.color = Color.white;
                    y += 28f;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Notification helper (standalone replacement)
    // ════════════════════════════════════════════════════════════════
    internal static class NotificationHelper
    {
        public static void Send(NotificationType type, string message, string title = "Object Launcher", float duration = 1.5f, bool showTitle = false)
        {
            try
            {
                Notifier.Send(new Notification
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    PopupLength = duration,
                    ShowTitleOnPopup = showTitle
                });
            }
            catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Object Launcher Controller (stripped of homing & cleanup)
    // ════════════════════════════════════════════════════════════════
    public static class ObjectLauncherController
    {
        private static bool launcherEnabled = false;
        private static string currentBarcodeID = "fa534c5a83ee4ec6bd641fec424c4142.Spawnable.PropBowlingBallBig";
        private static string currentItemName = "Bowling Ball Big";
        private static float launchForce = 50f;
        private static float maxLaunchForce = 10000f;
        private static float spawnDistance = 1f;
        private static bool _prevTriggerPressed = false;
        private static float _prevTriggerValue = 0f;
        private static bool fullAutoMode = false;
        private static float lastFireTime = 0f;
        private static float fullAutoDelay = 0.15f;

        private static bool safetyEnabled = true;
        private static bool useLeftHand = false;
        private static float spinVelocity = 0f;

        // Trajectory preview
        private static bool showTrajectory = true;
        private static GameObject trajectoryObject;
        private static LineRenderer trajectoryLineRenderer;
        private static int trajectorySegments = 20;
        private static float trajectoryTimeStep = 0.1f;

        // Multi-projectile
        private static int projectileCount = 1;
        private static float projectileSpacing = 0.3f;
        private static float rotationX = 0f;
        private static float rotationY = 0f;
        private static float rotationZ = 0f;

        // Spawn offset
        private static float spawnOffsetX = 0f;
        private static float spawnOffsetY = 0f;

        // Spawn scale
        private static float spawnScale = 1f;

        // Auto-cleanup & auto-despawn
        private static bool _autoCleanupEnabled = false;
        private static float _autoCleanupInterval = 30f;
        private static float _lastCleanupTime = 0f;
        private static bool _autoDespawnEnabled = false;
        private static float _autoDespawnDelay = 10f;
        private static List<GameObject> _launchedObjects = new List<GameObject>();

        private struct DespawnTimer
        {
            public GameObject Obj;
            public float DespawnTime;
        }
        private static List<DespawnTimer> _despawnTimers = new List<DespawnTimer>();

        // Aim rotation & force delay
        private static bool _aimRotationEnabled = false;
        private static bool _preActivateMenuTap = false;
        private static float _forceDelay = 0.02f;
        private static float _spawnForceDelay = 0.02f;

        // Claimed set for multi-projectile force resolution
        private static HashSet<int> _claimedForceTargets = new HashSet<int>();

        // Search system
        private static List<SearchableItem> _allItems = new List<SearchableItem>();
        private static List<SearchableItem> _filteredItems = new List<SearchableItem>();
        private static string _searchQuery = "";
        private static int _selectedSearchIndex = 0;

        public struct SearchableItem
        {
            public string Title;
            public string BarcodeID;
        }

        // Spawn tag registry (inline) for reliable spawn→force association
        private static Dictionary<string, GameObject> _spawnTags = new Dictionary<string, GameObject>();
        private static int _spawnTagCounter = 0;

        private static string GenerateSpawnTag(string prefix) => $"{prefix}_{++_spawnTagCounter}";
        private static void RegisterSpawnTag(string tag, GameObject go) { if (!string.IsNullOrEmpty(tag) && go != null) _spawnTags[tag] = go; }
        private static GameObject ResolveSpawnTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            if (_spawnTags.TryGetValue(tag, out var go)) { if (go != null) return go; _spawnTags.Remove(tag); }
            return null;
        }
        private static void RemoveSpawnTag(string tag) { if (!string.IsNullOrEmpty(tag)) _spawnTags.Remove(tag); }

        // Pending force
        private struct PendingForce
        {
            public GameObject Obj;
            public float StartTime;
            public float Force;
            public Vector3 SpawnPos;
            public HashSet<int> PreExistingIds;
            public Vector3 ForwardDir;
            public string BarcodeId;
            public string BarcodeToken;
            public float Scale;
            public string Tag;
        }

        private struct PendingRegistration
        {
            public GameObject Obj;
            public float StartTime;

            public PendingRegistration(GameObject obj, float startTime)
            {
                Obj = obj;
                StartTime = startTime;
            }
        }

        private static List<PendingRegistration> _pendingRegistrations = new List<PendingRegistration>();
        private static List<PendingForce> _pendingForces = new List<PendingForce>();

        // ── Properties ──

        public static string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value ?? "";
                FilterSearchItems();
            }
        }

        public static bool IsFullAuto
        {
            get => fullAutoMode;
            set
            {
                fullAutoMode = value;
                MelonLogger.Msg($"Fire mode: {(value ? "Full-Auto" : "Semi-Auto")}");
            }
        }

        public static float FullAutoDelay
        {
            get => fullAutoDelay;
            set => fullAutoDelay = Mathf.Clamp(value, 0.01f, 1f);
        }

        public static bool ShowTrajectory
        {
            get => showTrajectory;
            set
            {
                showTrajectory = value;
                if (!value && trajectoryObject != null)
                    trajectoryObject.SetActive(false);
            }
        }

        public static int ProjectileCount
        {
            get => projectileCount;
            set => projectileCount = Mathf.Clamp(value, 1, 25);
        }

        public static float ProjectileSpacing
        {
            get => projectileSpacing;
            set => projectileSpacing = Mathf.Max(value, 0.1f);
        }

        public static float RotationX
        {
            get => rotationX;
            set => rotationX = value % 360f;
        }

        public static float RotationY
        {
            get => rotationY;
            set => rotationY = value % 360f;
        }

        public static float RotationZ
        {
            get => rotationZ;
            set => rotationZ = value % 360f;
        }

        public static float SpawnScale
        {
            get => spawnScale;
            set => spawnScale = Mathf.Clamp(value, 0.1f, 10f);
        }

        public static bool AimRotationEnabled
        {
            get => _aimRotationEnabled;
            set => _aimRotationEnabled = value;
        }

        public static bool PreActivateMenuTap
        {
            get => _preActivateMenuTap;
            set => _preActivateMenuTap = value;
        }

        public static float ForceDelay
        {
            get => _spawnForceDelay;
            set
            {
                _spawnForceDelay = Mathf.Clamp(value, 0f, 2f);
                _forceDelay = _spawnForceDelay;
            }
        }

        // ── Auto-Cleanup / Auto-Despawn Properties ──
        public static bool AutoCleanupEnabled
        {
            get => _autoCleanupEnabled;
            set => _autoCleanupEnabled = value;
        }

        public static float AutoCleanupInterval
        {
            get => _autoCleanupInterval;
            set => _autoCleanupInterval = Mathf.Clamp(value, 1f, 300f);
        }

        public static bool AutoDespawnEnabled
        {
            get => _autoDespawnEnabled;
            set => _autoDespawnEnabled = value;
        }

        public static float AutoDespawnDelay
        {
            get => _autoDespawnDelay;
            set => _autoDespawnDelay = Mathf.Clamp(value, 1f, 300f);
        }

        public static bool IsLauncherEnabled
        {
            get => launcherEnabled;
            set => launcherEnabled = value;
        }

        public static float LaunchForce
        {
            get => launchForce;
            set => launchForce = Mathf.Clamp(value, 1f, maxLaunchForce);
        }

        public static float SpawnDistance
        {
            get => spawnDistance;
            set => spawnDistance = Mathf.Clamp(value, 0.5f, 10f);
        }

        public static float SpawnOffsetX
        {
            get => spawnOffsetX;
            set => spawnOffsetX = Mathf.Clamp(value, -10f, 10f);
        }

        public static float SpawnOffsetY
        {
            get => spawnOffsetY;
            set => spawnOffsetY = Mathf.Clamp(value, -10f, 10f);
        }

        public static bool SafetyEnabled
        {
            get => safetyEnabled;
            set => safetyEnabled = value;
        }

        public static bool UseLeftHand
        {
            get => useLeftHand;
            set => useLeftHand = value;
        }

        public static float SpinVelocity
        {
            get => spinVelocity;
            set => spinVelocity = Mathf.Clamp(value, 0f, 5000f);
        }

        public static string CurrentBarcodeID
        {
            get => currentBarcodeID;
            set => currentBarcodeID = value ?? "";
        }

        public static string CurrentItemName
        {
            get => currentItemName;
            set => currentItemName = value ?? "";
        }

        public static int SelectedSearchIndex
        {
            get => _selectedSearchIndex;
            set
            {
                if (_filteredItems.Count > 0)
                    _selectedSearchIndex = Mathf.Clamp(value, 0, _filteredItems.Count - 1);
            }
        }

        public static List<SearchableItem> GetFilteredItems() => _filteredItems;

        // ════════════════════════════════════════════
        // PRESET SYSTEM
        // ════════════════════════════════════════════

        public struct LauncherPreset
        {
            public string Name;
            public string BarcodeID;
            public string ItemName;
            public float LaunchForce;
            public float SpawnDistance;
            public float SpawnOffsetX;
            public float SpawnOffsetY;
            public float SpinVelocity;
            public float RotationX;
            public float RotationY;
            public float RotationZ;
            public float Scale;
        }

        private static List<LauncherPreset> _presets = new List<LauncherPreset>();
        private static string _presetName = "";

        public static string PresetName
        {
            get => _presetName;
            set => _presetName = value ?? "";
        }

        public static List<LauncherPreset> GetPresets() => _presets;

        public static void SetPresets(List<LauncherPreset> presets)
        {
            _presets.Clear();
            if (presets != null) _presets.AddRange(presets);
        }

        public static LauncherPreset CaptureCurrentAsPreset(string name)
        {
            return new LauncherPreset
            {
                Name = name,
                BarcodeID = currentBarcodeID,
                ItemName = currentItemName,
                LaunchForce = launchForce,
                SpawnDistance = spawnDistance,
                SpawnOffsetX = spawnOffsetX,
                SpawnOffsetY = spawnOffsetY,
                SpinVelocity = spinVelocity,
                RotationX = rotationX,
                RotationY = rotationY,
                RotationZ = rotationZ,
                Scale = spawnScale
            };
        }

        public static void ApplyPreset(LauncherPreset preset)
        {
            currentBarcodeID = preset.BarcodeID ?? currentBarcodeID;
            currentItemName = preset.ItemName ?? currentItemName;
            launchForce = Mathf.Clamp(preset.LaunchForce, 1f, maxLaunchForce);
            spawnDistance = Mathf.Clamp(preset.SpawnDistance, 0.5f, 10f);
            spawnOffsetX = Mathf.Clamp(preset.SpawnOffsetX, -10f, 10f);
            spawnOffsetY = Mathf.Clamp(preset.SpawnOffsetY, -10f, 10f);
            spinVelocity = Mathf.Clamp(preset.SpinVelocity, 0f, 5000f);
            rotationX = preset.RotationX % 360f;
            rotationY = preset.RotationY % 360f;
            rotationZ = preset.RotationZ % 360f;
            spawnScale = Mathf.Clamp(preset.Scale > 0 ? preset.Scale : 1f, 0.1f, 10f);
            MelonLogger.Msg($"[Preset] Applied preset '{preset.Name}': {preset.ItemName} Force={preset.LaunchForce}");
            SendNotification(NotificationType.Success, $"Loaded preset: {preset.Name}");
        }

        public static void SavePreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = $"{currentItemName} #{_presets.Count + 1}";

            _presets.RemoveAll(p => p.Name == name);
            var preset = CaptureCurrentAsPreset(name);
            _presets.Add(preset);
            MelonLogger.Msg($"[Preset] Saved preset '{name}': {currentItemName} Force={launchForce}");
            SendNotification(NotificationType.Success, $"Saved preset: {name}");
        }

        public static void DeletePreset(string name)
        {
            int removed = _presets.RemoveAll(p => p.Name == name);
            if (removed > 0)
            {
                MelonLogger.Msg($"[Preset] Deleted preset '{name}'");
                SendNotification(NotificationType.Success, $"Deleted preset: {name}");
            }
        }

        public static string SerializePresets()
        {
            var parts = new List<string>();
            foreach (var p in _presets)
            {
                parts.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                    (p.Name ?? "").Replace("|", "_").Replace(";;", "__"),
                    (p.BarcodeID ?? "").Replace("|", "_"),
                    (p.ItemName ?? "").Replace("|", "_").Replace(";;", "__"),
                    p.LaunchForce, p.SpawnDistance, p.SpawnOffsetX, p.SpawnOffsetY, p.SpinVelocity,
                    p.RotationX, p.RotationY, p.RotationZ, p.Scale));
            }
            return string.Join(";;", parts);
        }

        public static void DeserializePresets(string data)
        {
            _presets.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var entry in data.Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length >= 8)
                {
                    try
                    {
                        _presets.Add(new LauncherPreset
                        {
                            Name = parts[0],
                            BarcodeID = parts[1],
                            ItemName = parts[2],
                            LaunchForce = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnDistance = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnOffsetX = float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture),
                            SpawnOffsetY = float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture),
                            SpinVelocity = float.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture),
                            RotationX = parts.Length > 8 ? float.Parse(parts[8], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            RotationY = parts.Length > 9 ? float.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            RotationZ = parts.Length > 10 ? float.Parse(parts[10], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                            Scale = parts.Length > 11 ? float.Parse(parts[11], System.Globalization.CultureInfo.InvariantCulture) : 1f
                        });
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Preset] Failed to parse preset entry: {ex.Message}");
                    }
                }
            }
            MelonLogger.Msg($"[Preset] Deserialized {_presets.Count} presets");
        }

        public static void PopulatePresetLoadPage(Page page)
        {
            if (page == null) return;
            try { page.RemoveAll(); } catch { }

            if (_presets.Count == 0)
            {
                page.CreateFunction("No presets saved", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                string display = $"{preset.Name} ({preset.ItemName}, F:{preset.LaunchForce})";
                page.CreateFunction(display, Color.green, () => ApplyPreset(preset));
            }
        }

        public static void PopulatePresetDeletePage(Page page)
        {
            if (page == null) return;
            try { page.RemoveAll(); } catch { }

            if (_presets.Count == 0)
            {
                page.CreateFunction("No presets to delete", Color.gray, () => { });
                return;
            }

            for (int i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                string name = preset.Name;
                page.CreateFunction($"Delete: {name}", Color.red, () =>
                {
                    DeletePreset(name);
                    PopulatePresetDeletePage(page);
                });
            }
        }

        // ════════════════════════════════════════════
        // INITIALIZE
        // ════════════════════════════════════════════

        public static void Initialize()
        {
            MelonLogger.Msg("Object Launcher controller initialized");
        }

        // ════════════════════════════════════════════
        // OBJECT TRACKING / DESPAWN
        // ════════════════════════════════════════════

        public static void TrackLaunchedObject(GameObject go)
        {
            if (go == null) return;
            _launchedObjects.Add(go);

            if (_autoDespawnEnabled)
            {
                _despawnTimers.Add(new DespawnTimer
                {
                    Obj = go,
                    DespawnTime = Time.time + _autoDespawnDelay
                });
            }

            // Prune nulls if list gets large
            if (_launchedObjects.Count > 200)
            {
                _launchedObjects.RemoveAll(o => o == null);
            }
        }

        public static void DespawnLaunchedObjects()
        {
            int count = 0;
            for (int i = _launchedObjects.Count - 1; i >= 0; i--)
            {
                var obj = _launchedObjects[i];
                if (obj != null)
                {
                    try { UnityEngine.Object.Destroy(obj); count++; } catch { }
                }
            }
            _launchedObjects.Clear();
            _despawnTimers.Clear();
            MelonLogger.Msg($"Despawned {count} launched objects");
        }

        // ════════════════════════════════════════════
        // SEARCH SYSTEM
        // ════════════════════════════════════════════

        private static string StripRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, "<[^>]*>", "");
        }

        public static void RefreshSearchList()
        {
            try
            {
                MelonLogger.Msg("Refreshing searchable items for launcher...");
                _allItems.Clear();

                var assetWarehouseType = FindTypeByName("AssetWarehouse");
                if (assetWarehouseType == null)
                {
                    MelonLogger.Error("AssetWarehouse type not found!");
                    SendNotification(NotificationType.Error, "AssetWarehouse not found");
                    return;
                }

                var instanceProp = assetWarehouseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    MelonLogger.Error("AssetWarehouse.Instance not found!");
                    return;
                }

                var warehouseInstance = instanceProp.GetValue(null);
                if (warehouseInstance == null)
                {
                    MelonLogger.Warning("AssetWarehouse.Instance is null");
                    SendNotification(NotificationType.Warning, "Game not fully loaded");
                    return;
                }

                var spawnableCrateType = FindTypeByName("SpawnableCrate");
                if (spawnableCrateType == null)
                {
                    MelonLogger.Error("SpawnableCrate type not found!");
                    return;
                }

                var getCratesMethod = assetWarehouseType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetCrates" && m.IsGenericMethod);

                if (getCratesMethod == null)
                {
                    MelonLogger.Error("GetCrates method not found!");
                    return;
                }

                var genericGetCrates = getCratesMethod.MakeGenericMethod(spawnableCrateType);
                var crates = genericGetCrates.Invoke(warehouseInstance, new object[] { null });

                if (crates == null)
                {
                    MelonLogger.Warning("GetCrates returned null");
                    return;
                }

                var cratesType = crates.GetType();
                var countProp = cratesType.GetProperty("Count");
                var itemProp = cratesType.GetProperty("Item");

                if (countProp != null && itemProp != null)
                {
                    int count = (int)countProp.GetValue(crates);
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var crate = itemProp.GetValue(crates, new object[] { i });
                            if (crate != null)
                                AddCrateToSearchList(crate);
                        }
                        catch { }
                    }
                }

                _allItems = _allItems.OrderBy(x => x.Title).ToList();
                FilterSearchItems();

                MelonLogger.Msg($"Loaded {_allItems.Count} items for launcher search");
                SendNotification(NotificationType.Success, $"Loaded {_allItems.Count} items");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to refresh search list: {ex.Message}");
                SendNotification(NotificationType.Error, "Failed to load items");
            }
        }

        private static void AddCrateToSearchList(object crate)
        {
            try
            {
                var crateType = crate.GetType();

                string title = null;
                var titleProp = crateType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                if (titleProp != null)
                {
                    var titleObj = titleProp.GetValue(crate);
                    title = titleObj?.ToString();
                }

                string barcodeId = null;
                var barcodeProp = crateType.GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                if (barcodeProp != null)
                {
                    var barcode = barcodeProp.GetValue(crate);
                    if (barcode != null)
                    {
                        var idProp = barcode.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                        if (idProp != null)
                            barcodeId = idProp.GetValue(barcode) as string;
                    }
                }

                if (!string.IsNullOrEmpty(barcodeId))
                {
                    if (string.IsNullOrEmpty(title))
                    {
                        int lastDot = barcodeId.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot + 1 < barcodeId.Length)
                        {
                            title = barcodeId.Substring(lastDot + 1);
                            title = Regex.Replace(title, "([a-z])([A-Z])", "$1 $2");
                        }
                        else
                        {
                            title = barcodeId;
                        }
                    }

                    _allItems.Add(new SearchableItem
                    {
                        Title = StripRichText(title),
                        BarcodeID = barcodeId
                    });
                }
            }
            catch { }
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

        private static void FilterSearchItems()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredItems = new List<SearchableItem>(_allItems);
            }
            else
            {
                var query = _searchQuery.ToLower();
                _filteredItems = _allItems
                    .Where(item => item.Title.ToLower().Contains(query) || item.BarcodeID.ToLower().Contains(query))
                    .ToList();
            }
            _selectedSearchIndex = 0;

            if (_filteredItems.Count > 0)
            {
                var firstItem = _filteredItems[0];
                SendNotification(NotificationType.Information, $"Found {_filteredItems.Count} items\n[1] {firstItem.Title}");
            }
            else if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                SendNotification(NotificationType.Warning, "No items found");
            }
        }

        public static void NextSearchItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }
            _selectedSearchIndex = (_selectedSearchIndex + 1) % _filteredItems.Count;
            ShowCurrentSearchItem();
        }

        public static void PreviousSearchItem()
        {
            if (_filteredItems.Count == 0)
            {
                SendNotification(NotificationType.Warning, "No items - Refresh first");
                return;
            }
            _selectedSearchIndex--;
            if (_selectedSearchIndex < 0)
                _selectedSearchIndex = _filteredItems.Count - 1;
            ShowCurrentSearchItem();
        }

        private static void ShowCurrentSearchItem()
        {
            if (_filteredItems.Count == 0 || _selectedSearchIndex < 0 || _selectedSearchIndex >= _filteredItems.Count)
                return;
            var item = _filteredItems[_selectedSearchIndex];
            SendNotification(NotificationType.Information, $"[{_selectedSearchIndex + 1}/{_filteredItems.Count}] {item.Title}");
        }

        public static void CopyBarcodeFromSearch()
        {
            if (_filteredItems.Count == 0 || _selectedSearchIndex < 0 || _selectedSearchIndex >= _filteredItems.Count)
            {
                SendNotification(NotificationType.Warning, "No item selected");
                return;
            }

            var item = _filteredItems[_selectedSearchIndex];
            currentBarcodeID = item.BarcodeID;
            currentItemName = item.Title;

            MelonLogger.Msg($"Copied barcode: {item.Title} ({item.BarcodeID})");
            SendNotification(NotificationType.Success, $"Copied: {item.Title}");
        }

        // ════════════════════════════════════════════
        // UPDATE
        // ════════════════════════════════════════════

        public static void Update()
        {
            // Auto-cleanup timer
            if (_autoCleanupEnabled && _launchedObjects.Count > 0)
            {
                if (Time.time - _lastCleanupTime >= _autoCleanupInterval)
                {
                    _lastCleanupTime = Time.time;
                    DespawnLaunchedObjects();
                }
            }

            // Per-object auto-despawn timers
            if (_despawnTimers.Count > 0)
            {
                float now = Time.time;
                for (int i = _despawnTimers.Count - 1; i >= 0; i--)
                {
                    var dt = _despawnTimers[i];
                    if (dt.Obj == null)
                    {
                        _despawnTimers.RemoveAt(i);
                        continue;
                    }
                    if (now >= dt.DespawnTime)
                    {
                        try { UnityEngine.Object.Destroy(dt.Obj); } catch { }
                        _launchedObjects.Remove(dt.Obj);
                        _despawnTimers.RemoveAt(i);
                    }
                }
            }

            if (!launcherEnabled)
            {
                if (trajectoryObject != null)
                    trajectoryObject.SetActive(false);
                return;
            }

            // Read input
            bool triggerPressed = false;
            bool gripHeld = false;
            float triggerValue = 0f;

            try
            {
                if (useLeftHand)
                {
                    float leftTrigger = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger");
                    triggerValue = leftTrigger;
                    if (leftTrigger > 0.5f) triggerPressed = true;
                    float leftGrip = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger");
                    if (leftGrip > 0.5f) gripHeld = true;
                }
                else
                {
                    float rightTrigger = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger");
                    triggerValue = rightTrigger;
                    if (rightTrigger > 0.5f) triggerPressed = true;
                    float rightGrip = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger");
                    if (rightGrip > 0.5f) gripHeld = true;
                }
            }
            catch { }

            // Keyboard fallback
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.L))
            {
                triggerPressed = true;
                gripHeld = true;
            }

            // Trajectory preview
            if (showTrajectory)
            {
                if (safetyEnabled && gripHeld)
                {
                    if (trajectoryObject != null)
                        trajectoryObject.SetActive(false);
                }
                else
                {
                    UpdateTrajectoryPreview();
                }
            }
            else if (trajectoryObject != null)
            {
                trajectoryObject.SetActive(false);
            }

            // Safety check
            if (safetyEnabled && gripHeld)
                triggerPressed = false;

            // Fire
            bool currentPressed = triggerPressed;
            if (fullAutoMode)
            {
                // First shot fires immediately on rising edge
                if (currentPressed && !_prevTriggerPressed)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
                // Continuous fire: trigger must be held AND not decreasing (prevents
                // stray shot from analog trigger spring-back on release)
                else if (currentPressed && _prevTriggerPressed && triggerValue >= _prevTriggerValue - 0.05f && Time.time - lastFireTime >= fullAutoDelay)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
            }
            else
            {
                if (currentPressed && !_prevTriggerPressed)
                {
                    LaunchObject();
                    lastFireTime = Time.time;
                }
            }
            _prevTriggerPressed = currentPressed;
            _prevTriggerValue = triggerValue;

            // Process pending LabFusion registrations
            if (_pendingRegistrations.Count > 0)
            {
                for (int i = _pendingRegistrations.Count - 1; i >= 0; --i)
                {
                    var pr = _pendingRegistrations[i];
                    var go = pr.Obj;
                    if (go == null)
                    {
                        _pendingRegistrations.RemoveAt(i);
                        continue;
                    }

                    object marrowEntity = null;
                    try
                    {
                        var comps = go.GetComponents<UnityEngine.Component>();
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            var t = c.GetType();
                            if (t.Name == "MarrowEntity" || (t.FullName != null && t.FullName.Contains("MarrowEntity")))
                            {
                                marrowEntity = c;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (marrowEntity != null)
                    {
                        try
                        {
                            var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                            if (rb == null) continue;

                            if (Time.time - pr.StartTime < 0.05f) continue;

                            var propSenderType = FindTypeInAssembly("PropSender", "LabFusion")
                                ?? FindTypeInAssembly("PropSender", "LabFusion.Senders")
                                ?? FindTypeInAssembly("PropSender", "");
                            if (propSenderType != null)
                            {
                                var sendMethod = propSenderType.GetMethod("SendPropCreation", BindingFlags.Public | BindingFlags.Static);
                                if (sendMethod != null)
                                    sendMethod.Invoke(null, new object[] { marrowEntity, null, false });
                            }
                        }
                        catch (Exception e)
                        {
                            MelonLogger.Warning($"Delayed PropSender.SendPropCreation failed: {e}");
                        }

                        _pendingRegistrations.RemoveAt(i);
                        continue;
                    }

                    if (Time.time - pr.StartTime > 5f)
                    {
                        MelonLogger.Warning("Timed out waiting for MarrowEntity; skipping LabFusion registration");
                        _pendingRegistrations.RemoveAt(i);
                    }
                }
            }

            // Process pending force applications
            if (_pendingForces.Count > 0)
            {
                // Add currently resolved objects to claimed set (do NOT clear — keep objects
                // that already had force applied so other PendingForces don't re-match them)
                for (int i = 0; i < _pendingForces.Count; i++)
                {
                    var existing = _pendingForces[i];
                    if (existing.Obj != null)
                        _claimedForceTargets.Add(existing.Obj.GetInstanceID());
                }

                for (int i = _pendingForces.Count - 1; i >= 0; --i)
                {
                    var pf = _pendingForces[i];
                    var go = pf.Obj;
                    if (go == null)
                    {
                        // Try tag registry first (most reliable under latency)
                        var tagged = ResolveSpawnTag(pf.Tag);
                        if (tagged != null)
                        {
                            pf.Obj = tagged;
                            _pendingForces[i] = pf;
                            go = tagged;
                            _claimedForceTargets.Add(go.GetInstanceID());
                            RemoveSpawnTag(pf.Tag);
                        }
                        // If tag exists but hasn't resolved yet, wait before falling through
                        // to proximity — prevents wrong-object matching under high latency
                        else if (!string.IsNullOrEmpty(pf.Tag) && Time.time - pf.StartTime < 2f)
                        {
                            continue;
                        }
                        // Proximity fallback (for BoneLib spawns or after tag grace period)
                        else if (TryResolveForceTarget(ref pf, out var resolvedGo, out var resolvedRb, out var resolvedDist))
                        {
                            _pendingForces[i] = pf;
                            go = resolvedGo;
                            _claimedForceTargets.Add(go.GetInstanceID());
                        }
                        else
                        {
                            if (Time.time - pf.StartTime > 5f)
                            {
                                MelonLogger.Warning("Could not resolve spawned object for force application within 5s");
                                TryDespawnNearPosition(pf.SpawnPos, pf.PreExistingIds);
                                _pendingForces.RemoveAt(i);
                            }
                            continue;
                        }
                    }

                    // Search aggressively for Rigidbody
                    var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>(includeInactive: true);
                    if (rb == null && go.transform.parent != null)
                        rb = go.transform.parent.GetComponent<Rigidbody>() ?? go.transform.parent.GetComponentInChildren<Rigidbody>(includeInactive: true);



                    if (rb == null)
                    {
                        float waitTime = Time.time - pf.StartTime;
                        if (waitTime < 5f)
                        {
                            continue;
                        }
                        else
                        {
                            MelonLogger.Warning($"Rigidbody not found after {waitTime:0.00}s; searching vicinity");

                            var allRigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                            foreach (var nearbyRb in allRigidbodies)
                            {
                                if (nearbyRb != null && !IsPlayerRigidbody(nearbyRb) && Vector3.Distance(nearbyRb.transform.position, go.transform.position) < 1.5f)
                                {
                                    rb = nearbyRb;
                                    break;
                                }
                            }

                            if (rb == null)
                            {
                                MelonLogger.Error("Still no Rigidbody found; destroying object");
                                try { UnityEngine.Object.Destroy(go); } catch { }
                                _pendingForces.RemoveAt(i);
                                continue;
                            }
                        }
                    }

                    // Freeze object during wait to prevent falling before force is applied
                    if (Time.time - pf.StartTime < _spawnForceDelay)
                    {
                        try
                        {
                            rb.useGravity = false;
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                        catch { }
                        continue;
                    }

                    // Apply force
                    try
                    {
                        var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                        Vector3 launchDirection = pf.ForwardDir.sqrMagnitude > 0.0001f
                            ? pf.ForwardDir.normalized
                            : activeHand.transform.forward.normalized;

                        if (rb.isKinematic) rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.constraints = RigidbodyConstraints.None;
                        rb.WakeUp();

                        // Apply scale
                        if (pf.Scale > 0f && Mathf.Abs(pf.Scale - 1f) > 0.001f)
                        {
                            try
                            {
                                var scaleTarget = rb.GetComponentInParent<Rigidbody>() ?? rb;
                                var scaleTransform = ((Component)scaleTarget).transform;
                                Vector3 originalScale = scaleTransform.localScale;
                                Vector3 newScale = originalScale * pf.Scale;
                                scaleTransform.localScale = newScale;
                                float origVol = originalScale.x * originalScale.y * originalScale.z;
                                float newVol = newScale.x * newScale.y * newScale.z;
                                if (origVol > 0.0001f)
                                    scaleTarget.mass = scaleTarget.mass / origVol * newVol;
                            }
                            catch (Exception scaleEx)
                            {
                                MelonLogger.Warning($"Scale application failed: {scaleEx.Message}");
                            }
                        }

                        // Aim rotation
                        if (_aimRotationEnabled && launchDirection.sqrMagnitude > 0.01f)
                        {
                            Quaternion aimRot = Quaternion.LookRotation(launchDirection);
                            go.transform.rotation = aimRot * Quaternion.Euler(rotationX, rotationY, rotationZ);
                        }

                        // Zero existing velocity and apply launch
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.velocity = launchDirection * pf.Force;
                        rb.AddForce(launchDirection * pf.Force * 0.1f, ForceMode.VelocityChange);

                        // Apply spin
                        if (spinVelocity != 0f)
                            rb.angularVelocity = launchDirection * spinVelocity;

                        rb.WakeUp();

                        // Pre-activate: simulate Y/B menu tap on spawned object
                        if (_preActivateMenuTap)
                        {
                            try { TryPreActivate(go); } catch { }
                        }

                        // Track launched object for auto-cleanup/despawn
                        TrackLaunchedObject(go);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Warning($"Failed to apply delayed force: {e.Message}");
                    }

                    _pendingForces.RemoveAt(i);
                }
            }
            else
            {
                // All pending forces resolved — safe to clear claimed set
                if (_claimedForceTargets.Count > 0)
                    _claimedForceTargets.Clear();
            }
        }

        // ════════════════════════════════════════════
        // TRAJECTORY PREVIEW
        // ════════════════════════════════════════════

        private static void UpdateTrajectoryPreview()
        {
            try
            {
                var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                if (activeHand == null)
                {
                    if (trajectoryObject != null) trajectoryObject.SetActive(false);
                    return;
                }

                Transform handTransform = activeHand.transform;
                if (handTransform == null)
                {
                    if (trajectoryObject != null) trajectoryObject.SetActive(false);
                    return;
                }

                Vector3 forward = handTransform.forward;
                Vector3 worldRight = Vector3.Cross(Vector3.up, forward).normalized;
                if (worldRight.sqrMagnitude < 0.001f)
                    worldRight = Vector3.Cross(Vector3.up, forward + Vector3.right * 0.01f).normalized;
                Vector3 startPos = handTransform.position
                    + forward * spawnDistance
                    + worldRight * spawnOffsetX
                    + Vector3.up * spawnOffsetY;
                Vector3 initialVelocity = handTransform.forward.normalized * launchForce;
                Vector3 gravity = Physics.gravity;

                if (trajectoryObject == null)
                {
                    trajectoryObject = new GameObject("TrajectoryPreview");
                    trajectoryLineRenderer = trajectoryObject.AddComponent<LineRenderer>();

                    trajectoryLineRenderer.useWorldSpace = true;
                    trajectoryLineRenderer.startWidth = 0.02f;
                    trajectoryLineRenderer.endWidth = 0.02f;
                    trajectoryLineRenderer.alignment = LineAlignment.View;
                    trajectoryLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    trajectoryLineRenderer.receiveShadows = false;
                    trajectoryLineRenderer.positionCount = trajectorySegments;

                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("SLZ/SLZ Unlit");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

                    if (shader != null)
                    {
                        trajectoryLineRenderer.material = new Material(shader);
                        trajectoryLineRenderer.material.color = Color.white;
                        if (shader.name.Contains("Universal Render Pipeline"))
                            trajectoryLineRenderer.material.SetColor("_BaseColor", Color.white);
                        else if (shader.name.Contains("SLZ") && trajectoryLineRenderer.material.HasProperty("_Color"))
                            trajectoryLineRenderer.material.SetColor("_Color", Color.white);
                        trajectoryLineRenderer.material.renderQueue = 4000;
                    }
                }

                trajectoryObject.SetActive(true);

                Vector3[] points = new Vector3[trajectorySegments];
                for (int i = 0; i < trajectorySegments; i++)
                {
                    float t = i * trajectoryTimeStep;
                    Vector3 point = startPos + initialVelocity * t + 0.5f * gravity * t * t;

                    if (i > 0)
                    {
                        Vector3 prevPoint = points[i - 1];
                        Vector3 direction = point - prevPoint;
                        float distance = direction.magnitude;

                        RaycastHit hit;
                        if (Physics.Raycast(prevPoint, direction.normalized, out hit, distance))
                        {
                            points[i] = hit.point;
                            for (int j = i + 1; j < trajectorySegments; j++)
                                points[j] = hit.point;
                            break;
                        }
                    }
                    points[i] = point;
                }

                trajectoryLineRenderer.positionCount = trajectorySegments;
                trajectoryLineRenderer.SetPositions(points);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Trajectory preview error: {ex.Message}");
                if (trajectoryObject != null) trajectoryObject.SetActive(false);
            }
        }

        // ════════════════════════════════════════════
        // ADD ITEM FROM LEFT HAND
        // ════════════════════════════════════════════

        public static void AddItemFromLeftHand()
        {
            try
            {
                var pooleeType = FindTypeInAssembly("Poolee", "Assembly-CSharp");
                if (pooleeType == null)
                    pooleeType = FindTypeInAssembly("Poolee", "Il2CppSLZ.Marrow");
                if (pooleeType == null)
                {
                    MelonLogger.Error("Cannot find Poolee type!");
                    return;
                }

                var playerType = typeof(Player);
                var getComponentInHandMethod = playerType.GetMethod("GetComponentInHand", BindingFlags.Public | BindingFlags.Static);
                if (getComponentInHandMethod == null)
                {
                    MelonLogger.Error("Cannot find GetComponentInHand method!");
                    return;
                }

                var genericMethod = getComponentInHandMethod.MakeGenericMethod(pooleeType);
                object poolee = genericMethod.Invoke(null, new object[] { Player.LeftHand });
                if (poolee == null)
                {
                    MelonLogger.Warning("No Poolee in left hand. Grab a spawnable item.");
                    return;
                }

                var spawnableCrateProp = pooleeType.GetProperty("SpawnableCrate", BindingFlags.Public | BindingFlags.Instance);
                if (spawnableCrateProp == null)
                {
                    MelonLogger.Error("Cannot find SpawnableCrate property on Poolee!");
                    return;
                }

                var spawnableCrate = spawnableCrateProp.GetValue(poolee);
                if (spawnableCrate == null)
                {
                    MelonLogger.Warning("SpawnableCrate is null.");
                    return;
                }

                var spawnableCrateType = spawnableCrate.GetType();
                var barcodeProp = spawnableCrateType.GetProperty("Barcode", BindingFlags.Public | BindingFlags.Instance);
                if (barcodeProp == null)
                {
                    MelonLogger.Error("Cannot find Barcode property on SpawnableCrate!");
                    return;
                }

                var barcode = barcodeProp.GetValue(spawnableCrate);
                if (barcode == null)
                {
                    MelonLogger.Warning("Barcode is null.");
                    return;
                }

                var barcodeType = barcode.GetType();
                var idProp = barcodeType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                if (idProp == null)
                {
                    MelonLogger.Error("Cannot find ID property on Barcode!");
                    return;
                }

                string id = idProp.GetValue(barcode) as string;
                if (string.IsNullOrEmpty(id))
                {
                    MelonLogger.Warning("Barcode ID is empty");
                    return;
                }

                // Get display name
                string itemName = id;
                try
                {
                    var titleProp = spawnableCrateType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                    if (titleProp != null)
                    {
                        var titleValue = titleProp.GetValue(spawnableCrate);
                        if (titleValue != null) itemName = titleValue.ToString();
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(itemName) || itemName == id)
                {
                    int lastDot = id.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot + 1 < id.Length)
                    {
                        itemName = id.Substring(lastDot + 1);
                        itemName = Regex.Replace(itemName, "([a-z])([A-Z])", "$1 $2");
                        itemName = Regex.Replace(itemName, "([A-Z]+)([A-Z][a-z])", "$1 $2");
                    }
                }

                CurrentBarcodeID = id;
                CurrentItemName = itemName;
                MelonLogger.Msg($"Item selected: {itemName} ({id})");
                SendNotification(NotificationType.Success, $"Selected: {itemName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to extract barcode from left hand: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════
        // LAUNCH OBJECT
        // ════════════════════════════════════════════

        public static void LaunchObject()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentBarcodeID))
                {
                    MelonLogger.Warning("No item selected. Use 'Add Item' first.");
                    return;
                }

                var activeHand = useLeftHand ? Player.LeftHand : Player.RightHand;
                if (activeHand == null)
                {
                    MelonLogger.Error($"Cannot access {(useLeftHand ? "left" : "right")} hand.");
                    return;
                }

                Transform handTransform = activeHand.transform;
                if (handTransform == null)
                {
                    MelonLogger.Error("Cannot get hand transform.");
                    return;
                }

                Vector3 fwd = handTransform.forward;
                Vector3 worldRight = Vector3.Cross(Vector3.up, fwd).normalized;
                if (worldRight.sqrMagnitude < 0.001f)
                    worldRight = Vector3.Cross(Vector3.up, fwd + Vector3.right * 0.01f).normalized;
                Vector3 baseSpawnPos = handTransform.position
                    + fwd * spawnDistance
                    + worldRight * spawnOffsetX
                    + Vector3.up * spawnOffsetY;
                Vector3 launchDir = fwd.normalized;
                Vector3 rightDir = worldRight;
                Vector3 upDir = Vector3.Cross(fwd, worldRight).normalized;

                Quaternion customRotation = Quaternion.Euler(rotationX, rotationY, rotationZ);
                Quaternion spawnRotation = handTransform.rotation * customRotation;

                var offsets = CalculateMatrixOffsets(projectileCount, projectileSpacing, rightDir, upDir);

                string barcodeToken = CurrentBarcodeID;
                if (!string.IsNullOrEmpty(barcodeToken))
                {
                    int lastDot = barcodeToken.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot + 1 < barcodeToken.Length)
                        barcodeToken = barcodeToken.Substring(lastDot + 1);
                    int lastSlash = barcodeToken.LastIndexOf('/');
                    if (lastSlash >= 0 && lastSlash + 1 < barcodeToken.Length)
                        barcodeToken = barcodeToken.Substring(lastSlash + 1);
                }

                var preExistingIds = new HashSet<int>();
                foreach (var rb in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                {
                    if (rb == null || rb.gameObject == null) continue;
                    preExistingIds.Add(rb.gameObject.GetInstanceID());
                }

                foreach (var offset in offsets)
                {
                    Vector3 spawnPos = baseSpawnPos + offset;
                    string tag = GenerateSpawnTag("OL");

                    bool spawned = TryNetworkedSpawn(CurrentBarcodeID, spawnPos, spawnRotation, launchForce, spawnScale, tag);

                    if (spawned)
                    {
                        _pendingForces.Add(new PendingForce
                        {
                            Obj = null,
                            StartTime = Time.time,
                            Force = launchForce,
                            SpawnPos = spawnPos,
                            PreExistingIds = preExistingIds,
                            ForwardDir = launchDir,
                            BarcodeId = CurrentBarcodeID,
                            BarcodeToken = barcodeToken,
                            Scale = spawnScale,
                            Tag = tag
                        });
                    }

                    if (!spawned)
                    {
                        // Fallback to BoneLib HelperMethods.SpawnCrate
                        MelonLogger.Msg("Falling back to BoneLib spawn method");
                        MethodInfo chosenSpawnMethod = null;
                        object spawnFirstArg = null;
                        var helperMethodsType = FindTypeInAssembly("HelperMethods", "BoneLib");
                        if (helperMethodsType != null)
                        {
                            var candidates = helperMethodsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(m => m.Name == "SpawnCrate").ToArray();

                            foreach (var m in candidates)
                            {
                                var ps = m.GetParameters();
                                if (ps.Length >= 2)
                                {
                                    var firstType = ps[0].ParameterType;
                                    if (firstType == typeof(string))
                                    {
                                        chosenSpawnMethod = m;
                                        spawnFirstArg = CurrentBarcodeID;
                                        break;
                                    }
                                    if (firstType.Name.Contains("SpawnableCrateReference") || firstType.Name.Contains("SpawnableCrateRef"))
                                    {
                                        var crateRefType = firstType;
                                        ConstructorInfo ctor = null;
                                        try { ctor = crateRefType.GetConstructor(new[] { typeof(string) }); } catch { }
                                        object crateRef = null;
                                        if (ctor != null)
                                        {
                                            try { crateRef = ctor.Invoke(new object[] { CurrentBarcodeID }); } catch { }
                                        }
                                        if (crateRef == null)
                                        {
                                            var barcodeType = FindTypeInAssembly("Barcode", "Assembly-CSharp") ?? FindTypeInAssembly("Barcode", "Il2CppSLZ.Marrow");
                                            if (barcodeType != null)
                                            {
                                                var bcCtor = barcodeType.GetConstructor(new[] { typeof(string) });
                                                if (bcCtor != null)
                                                {
                                                    var bc = bcCtor.Invoke(new object[] { CurrentBarcodeID });
                                                    try { ctor = crateRefType.GetConstructor(new[] { barcodeType }); } catch { }
                                                    if (ctor != null)
                                                    {
                                                        try { crateRef = ctor.Invoke(new object[] { bc }); } catch { }
                                                    }
                                                }
                                            }
                                        }
                                        if (crateRef != null)
                                        {
                                            chosenSpawnMethod = m;
                                            spawnFirstArg = crateRef;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (chosenSpawnMethod != null)
                        {
                            try
                            {
                                var ps = chosenSpawnMethod.GetParameters();
                                var args = new List<object>();
                                for (int pi = 0; pi < ps.Length; ++pi)
                                {
                                    var p = ps[pi];
                                    var pType = p.ParameterType;

                                    if (pi == 0) { args.Add(spawnFirstArg); continue; }
                                    if (pType == typeof(Vector3) && pi == 1) { args.Add(spawnPos); continue; }
                                    if (pType == typeof(Quaternion)) { args.Add(Quaternion.identity); continue; }
                                    if (pType == typeof(Vector3)) { args.Add(Vector3.one); continue; }
                                    if (pType == typeof(bool)) { args.Add(true); continue; }
                                    if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(Action<>))
                                    {
                                        var genericArg = pType.GetGenericArguments()[0];
                                        if (genericArg == typeof(GameObject))
                                        {
                                            // Capture locals for callback
                                            var capturedHand = handTransform;
                                            var capturedScale = spawnScale;
                                            var capturedForce = launchForce;
                                            Action<GameObject> callback = (go) =>
                                            {
                                                try
                                                {
                                                    var rbCb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                                                    if (rbCb != null)
                                                    {
                                                        Vector3 dir = capturedHand.forward.normalized;
                                                        if (rbCb.isKinematic) rbCb.isKinematic = false;
                                                        if (capturedScale > 0f && Mathf.Abs(capturedScale - 1f) > 0.001f)
                                                        {
                                                            try
                                                            {
                                                                var scaleRb = go.GetComponentInParent<Rigidbody>() ?? rbCb;
                                                                var st = ((Component)scaleRb).transform;
                                                                Vector3 origS = st.localScale;
                                                                Vector3 newS = origS * capturedScale;
                                                                st.localScale = newS;
                                                                float origV = origS.x * origS.y * origS.z;
                                                                float newV = newS.x * newS.y * newS.z;
                                                                if (origV > 0.0001f) scaleRb.mass = scaleRb.mass / origV * newV;
                                                            }
                                                            catch { }
                                                        }
                                                        rbCb.velocity = dir * capturedForce;
                                                    }
                                                }
                                                catch { }
                                            };
                                            args.Add(callback);
                                            continue;
                                        }
                                    }
                                    args.Add(null);
                                }
                                chosenSpawnMethod.Invoke(null, args.ToArray());
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error($"Error invoking HelperMethods.SpawnCrate: {ex.Message}");
                            }
                        }

                        _pendingForces.Add(new PendingForce
                        {
                            Obj = null,
                            StartTime = Time.time,
                            Force = launchForce,
                            SpawnPos = spawnPos,
                            PreExistingIds = preExistingIds,
                            ForwardDir = launchDir,
                            BarcodeId = CurrentBarcodeID,
                            BarcodeToken = barcodeToken,
                            Scale = spawnScale
                        });
                    }
                }

                MelonLogger.Msg($"Spawned {projectileCount} projectile(s) of {CurrentBarcodeID}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to launch object: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════
        // NETWORK SPAWN
        // ════════════════════════════════════════════

        private static bool TryNetworkedSpawn(string barcode, Vector3 position, Quaternion rotation, float forceAmount = 0f, float scaleAmount = 1f, string tag = null)
        {
            try
            {
                var networkAssetSpawnerType = FindTypeInAssembly("NetworkAssetSpawner", "LabFusion") ?? FindTypeInAssembly("NetworkAssetSpawner", "");
                var spawnableType = FindTypeInAssembly("Spawnable", "LabFusion") ?? FindTypeInAssembly("Spawnable", "");
                var spawnRequestType = FindTypeInAssembly("SpawnRequestInfo", "LabFusion") ?? FindTypeInAssembly("SpawnRequestInfo", "");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                {
                    MelonLogger.Warning("Required LabFusion types not found");
                    return false;
                }

                var crateRefType = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("SpawnableCrateReference", "Assembly-CSharp");
                if (crateRefType == null)
                {
                    MelonLogger.Warning("SpawnableCrateReference type not found");
                    return false;
                }

                ConstructorInfo crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null)
                {
                    MelonLogger.Warning("No string ctor on SpawnableCrateReference");
                    return false;
                }

                object crateRef = crateCtor.Invoke(new object[] { barcode });

                object spawnable = Activator.CreateInstance(spawnableType);
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi)
                    fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi)
                    pi.SetValue(spawnable, crateRef);

                object spawnReq = Activator.CreateInstance(spawnRequestType);
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

                // Set SpawnCallback for tag registration and scale application
                TrySetSpawnCallback(spawnReq, spawnRequestType, tag, scaleAmount);

                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null)
                {
                    MelonLogger.Warning("NetworkAssetSpawner.Spawn method not found");
                    return false;
                }

                spawnMethod.Invoke(null, new object[] { spawnReq });
                return true;
            }
            catch (Exception ex)
            {
                try { MelonLogger.Warning($"LabFusion spawn failed: {ex}"); }
                catch { MelonLogger.Warning($"LabFusion spawn failed: {ex.Message}"); }
                return false;
            }
        }

        /// <summary>
        /// Set the SpawnCallback on a SpawnRequestInfo to register tag and apply scale.
        /// Uses expression trees to handle the Il2Cpp-typed delegate.
        /// </summary>
        private static bool TrySetSpawnCallback(object spawnReq, Type spawnRequestType, string tag, float scale)
        {
            try
            {
                var callbackInfoType = FindTypeInAssembly("SpawnCallbackInfo", "LabFusion") ?? FindTypeInAssembly("SpawnCallbackInfo", "");
                var callbackMember = spawnRequestType.GetField("SpawnCallback")
                    ?? (MemberInfo)spawnRequestType.GetProperty("SpawnCallback");

                if (callbackInfoType == null || callbackMember == null)
                    return false;

                var actionType = typeof(Action<>).MakeGenericType(callbackInfoType);
                var param = Expression.Parameter(callbackInfoType, "info");

                var helperMethod = typeof(ObjectLauncherController).GetMethod(
                    nameof(OnSpawnCallback),
                    BindingFlags.Public | BindingFlags.Static);

                if (helperMethod == null) return false;

                var boxedParam = Expression.Convert(param, typeof(object));
                var tagConst = Expression.Constant(tag ?? "", typeof(string));
                var scaleConst = Expression.Constant(scale);
                var call = Expression.Call(helperMethod, boxedParam, tagConst, scaleConst);
                var lambda = Expression.Lambda(actionType, call, param);
                var typedDelegate = lambda.Compile();

                if (callbackMember is FieldInfo fi)
                    fi.SetValue(spawnReq, typedDelegate);
                else if (callbackMember is PropertyInfo pi)
                    pi.SetValue(spawnReq, typedDelegate);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"TrySetSpawnCallback error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called from the NetworkAssetSpawner SpawnCallback via expression-tree delegate.
        /// Registers the spawned object by tag and optionally applies scale.
        /// </summary>
        public static void OnSpawnCallback(object callbackInfo, string tag, float scale)
        {
            try
            {
                if (callbackInfo == null) return;
                var spawnedField = callbackInfo.GetType().GetField("Spawned");
                if (spawnedField == null) return;
                var go = spawnedField.GetValue(callbackInfo) as GameObject;
                if (go == null) return;

                if (!string.IsNullOrEmpty(tag))
                {
                    RegisterSpawnTag(tag, go);
                    MelonLogger.Msg($"[SpawnTag] Registered '{tag}' → '{go.name}'");
                }

                if (scale > 0f && Mathf.Abs(scale - 1f) > 0.001f)
                {
                    ApplyScaleToSpawnCallback(go, scale);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SpawnTag] Callback error: {ex.Message}");
            }
        }

        private static void ApplyScaleToSpawnCallback(GameObject go, float scale)
        {
            try
            {
                if (go == null) return;

                var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                Transform st;
                Rigidbody scaleRb;
                if (rb != null)
                {
                    scaleRb = go.GetComponentInParent<Rigidbody>() ?? rb;
                    st = ((Component)scaleRb).transform;
                }
                else
                {
                    st = go.transform;
                    scaleRb = null;
                }

                Vector3 origS = st.localScale;
                Vector3 newS = origS * scale;
                st.localScale = newS;

                if (scaleRb != null)
                {
                    float origV = origS.x * origS.y * origS.z;
                    float newV = newS.x * newS.y * newS.z;
                    if (origV > 0.0001f)
                        scaleRb.mass = scaleRb.mass / origV * newV;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"SpawnCallback scale failed: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════

        private static bool IsPlayerRigidbody(Rigidbody rb)
        {
            try
            {
                var localRig = Player.RigManager;
                if (localRig == null) return false;
                var rigTransform = ((Component)localRig).transform;
                return rb.transform.IsChildOf(rigTransform);
            }
            catch { return false; }
        }

        private static bool TryResolveForceTarget(ref PendingForce pf, out GameObject resolvedGo, out Rigidbody resolvedRb, out float resolvedDist)
        {
            resolvedGo = null;
            resolvedRb = null;
            resolvedDist = float.MaxValue;

            var allRbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            foreach (var rb in allRbs)
            {
                if (rb == null || rb.gameObject == null) continue;
                if (IsPlayerRigidbody(rb)) continue;
                int id = rb.gameObject.GetInstanceID();
                if (pf.PreExistingIds != null && pf.PreExistingIds.Contains(id)) continue;
                if (_claimedForceTargets.Contains(id)) continue;

                float dist = Vector3.Distance(rb.transform.position, pf.SpawnPos);
                if (dist > 8f) continue;

                if (dist < resolvedDist)
                {
                    resolvedGo = rb.gameObject;
                    resolvedRb = rb;
                    resolvedDist = dist;
                }
            }

            if (resolvedGo != null)
            {
                pf.Obj = resolvedGo;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Simulates a Y/B menu tap on the spawned object by finding components with
        /// OnTriggerGripUpdate and invoking them via IL2CPP runtime with _menuTap = true.
        /// </summary>
        private static unsafe void TryPreActivate(GameObject go)
        {
            var hand = useLeftHand ? Player.LeftHand : Player.RightHand;
            if (hand == null) return;

            var controller = hand.Controller;
            if (controller == null) return;

            // Set menu tap flag via IL2CPP property
            controller._menuTap = true;

            try
            {
                // Find and invoke OnTriggerGripUpdate on all MonoBehaviours via IL2CPP runtime
                var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
                if (behaviours != null)
                {
                    foreach (var mb in behaviours)
                    {
                        if (mb == null) continue;
                        try
                        {
                            IntPtr il2cppClass = IL2CPP.il2cpp_object_get_class(mb.Pointer);
                            IntPtr methodPtr = IL2CPP.il2cpp_class_get_method_from_name(il2cppClass, "OnTriggerGripUpdate", 1);
                            if (methodPtr == IntPtr.Zero) continue;

                            void** args = stackalloc void*[1];
                            args[0] = (void*)hand.Pointer;
                            IntPtr exc = IntPtr.Zero;
                            IL2CPP.il2cpp_runtime_invoke(methodPtr, mb.Pointer, args, ref exc);
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                controller._menuTap = false;
            }
        }

        private static void TryDespawnNearPosition(Vector3 spawnPos, HashSet<int> preExistingIds)
        {
            try
            {
                var allRbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                foreach (var rb in allRbs)
                {
                    if (rb == null || rb.gameObject == null) continue;
                    if (preExistingIds != null && preExistingIds.Contains(rb.gameObject.GetInstanceID())) continue;

                    float dist = Vector3.Distance(rb.transform.position, spawnPos);
                    if (dist < 3f)
                    {
                        UnityEngine.Object.Destroy(rb.gameObject);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Failed to despawn near position: {e.Message}");
            }
        }

        private static List<Vector3> CalculateMatrixOffsets(int count, float spacing, Vector3 right, Vector3 up)
        {
            var offsets = new List<Vector3>();

            if (count <= 1)
            {
                offsets.Add(Vector3.zero);
                return offsets;
            }

            if (count == 2)
            {
                offsets.Add(-right * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f);
            }
            else if (count == 3)
            {
                offsets.Add(up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else if (count == 4)
            {
                offsets.Add(-right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else
            {
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
                float halfGrid = (gridSize - 1) * spacing * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    int row = i / gridSize;
                    int col = i % gridSize;
                    float x = col * spacing - halfGrid;
                    float y = row * spacing - halfGrid;
                    offsets.Add(right * x + up * y);
                }
            }

            return offsets;
        }

        private static Type FindTypeInAssembly(string typeName, string assemblyName)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly != null)
                {
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;

                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null) return type;
                }

                var typeFromAnyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == typeName);

                if (typeFromAnyAssembly != null)
                    return typeFromAnyAssembly;
            }
            catch { }
            return null;
        }

        private static void SendNotification(NotificationType type, string message)
        {
            NotificationHelper.Send(type, message);
        }
    }
}
