using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using LabFusion.Player;
using LabFusion.Network;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Quick Menu â€” a lightweight persistent overlay for immediate needs:
    /// simplified launcher/drop-on-player, player list with kick/ban/steam profile,
    /// and level change.
    /// Separate keybind from the main overlay.
    /// </summary>
    public static class QuickMenuController
    {
        private static bool _visible = false;
        public static bool IsVisible => _visible;

        // Window layout
        private const int WIN_ID = 99910;
        private const float WIN_W = 480f;
        private const float WIN_H = 640f;
        private const float PAD = 10f;
        private const float ROW = 26f;
        private const float BTN_H = 26f;

        // Tabs
        private static int _tab = 0; // 0=Launcher, 1=Players, 2=Levels, 3=Spawn
        private static readonly string[] TabNames = { "Launcher", "Players", "Levels", "Spawn" };

        // Launcher tab state
        private static string _launcherSearch = "";
        private static List<SpawnableEntry> _launcherResults = new List<SpawnableEntry>();
        private static bool _launcherSearchDirty = true;
        private static bool _showHomingSettings = false;
        private static bool _showLaunchSettings = false;
        private static bool _showCleanupSettings = false;
        private static readonly string[] TargetFilterNames = { "NEAREST", "HEAVIEST", "LIGHTEST", "SELECTED" };

        // Player tab state - cached
        private static float _playerRefreshTime = 0f;

        // Level tab state
        private static string _levelSearch = "";
        private static List<MapChangeController.LevelEntry> _levelResults = new List<MapChangeController.LevelEntry>();
        private static bool _levelSearchDirty = true;

        // Spawn tab state
        private static string _spawnSearch = "";
        private static List<SpawnableEntry> _spawnResults = new List<SpawnableEntry>();
        private static bool _spawnSearchDirty = true;
        private static bool _spawnListLoaded = false;

        // Kick/Ban reflection cache
        private static MethodInfo _kickMethod;
        private static MethodInfo _banMethod;
        private static bool _kickBanResolved = false;

        public struct SpawnableEntry
        {
            public string Title;
            public string BarcodeID;
        }

        public static void Initialize()
        {
            ResolveKickBan();
        }

        public static void CheckInput()
        {
            var key = KeybindManager.GetKey("QuickMenu");
            if (key != KeyCode.None && Input.GetKeyDown(key))
                _visible = !_visible;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  MAIN DRAW
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public static void Draw()
        {
            if (!_visible) return;

            // Manual scroll (same pattern as main overlay)
            if (Event.current != null && Event.current.type == EventType.ScrollWheel)
            {
                _scrollY += Event.current.delta.y * 30f;
                if (_scrollY < 0f) _scrollY = 0f;
            }

            float x = Screen.width - WIN_W - 20f;
            float y = (Screen.height - WIN_H) / 2f;

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, OverlayMenu.MenuOpacity);
            var style = OverlayMenu.WindowStyle;
            if (style != null)
                GUI.Window(WIN_ID, new Rect(x, y, WIN_W, WIN_H), (Action<int>)DrawWindow, "Quick Menu  |  " + TabNames[_tab], style);
            else
                GUI.Window(WIN_ID, new Rect(x, y, WIN_W, WIN_H), (Action<int>)DrawWindow, "Quick Menu  |  " + TabNames[_tab]);
            GUI.BringWindowToFront(WIN_ID);
            GUI.color = prevColor;
        }

        private static float _scrollY = 0f;

        private static void DrawWindow(int id)
        {
            float y = 22f;
            float w = WIN_W - PAD * 2f;

            // Tab bar
            float tabW = w / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                GUI.color = (i == _tab) ? OverlayMenu.AccentColor : Color.white;
                if (GUI.Button(new Rect(PAD + i * tabW, y, tabW - 2f, BTN_H), TabNames[i]))
                {
                    _tab = i;
                    _scrollY = 0f;
                }
            }
            GUI.color = Color.white;
            y += BTN_H + 6f;

            // Content area â€” clipped group with manual scroll offset
            float contentTop = y;
            float contentH = WIN_H - contentTop - PAD;
            GUI.BeginGroup(new Rect(0, contentTop, WIN_W, contentH));

            float cy = -_scrollY;
            float cw = w;

            switch (_tab)
            {
                case 0: cy = DrawLauncherTab(cy, cw); break;
                case 1: cy = DrawPlayersTab(cy, cw); break;
                case 2: cy = DrawLevelsTab(cy, cw); break;
                case 3: cy = DrawSpawnTab(cy, cw); break;
            }

            GUI.EndGroup();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TAB: LAUNCHER
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static float DrawLauncherTab(float y, float w)
        {
            // Current item
            GUI.color = OverlayMenu.SectionColor;
            GUI.Label(new Rect(0, y, w, ROW), $"Selected: {ObjectLauncherController.CurrentItemName}");
            GUI.color = Color.white;
            y += ROW;

            // Quick toggles
            bool launcherOn = ObjectLauncherController.IsLauncherEnabled;
            bool newLauncher = GUI.Toggle(new Rect(0, y, w * 0.5f, ROW), launcherOn, " Launcher Enabled");
            if (newLauncher != launcherOn) { ObjectLauncherController.IsLauncherEnabled = newLauncher; SettingsManager.MarkDirty(); }

            bool homingOn = ObjectLauncherController.HomingEnabled;
            bool newHoming = GUI.Toggle(new Rect(w * 0.5f, y, w * 0.5f, ROW), homingOn, " Homing");
            if (newHoming != homingOn) { ObjectLauncherController.HomingEnabled = newHoming; SettingsManager.MarkDirty(); }
            y += ROW + 2f;

            bool fullAutoOn = ObjectLauncherController.IsFullAuto;
            bool newFullAuto = GUI.Toggle(new Rect(0, y, w * 0.5f, ROW), fullAutoOn, " Full Auto");
            if (newFullAuto != fullAutoOn) { ObjectLauncherController.IsFullAuto = newFullAuto; SettingsManager.MarkDirty(); }

            bool safetyOn = ObjectLauncherController.SafetyEnabled;
            bool newSafety = GUI.Toggle(new Rect(w * 0.5f, y, w * 0.5f, ROW), safetyOn, " Safety (Grip+Trig)");
            if (newSafety != safetyOn) { ObjectLauncherController.SafetyEnabled = newSafety; SettingsManager.MarkDirty(); }
            y += ROW + 2f;

            bool leftHand = ObjectLauncherController.UseLeftHand;
            bool newLeftHand = GUI.Toggle(new Rect(0, y, w * 0.5f, ROW), leftHand, " Left Hand");
            if (newLeftHand != leftHand) { ObjectLauncherController.UseLeftHand = newLeftHand; SettingsManager.MarkDirty(); }

            bool showTraj = ObjectLauncherController.ShowTrajectory;
            bool newShowTraj = GUI.Toggle(new Rect(w * 0.5f, y, w * 0.5f, ROW), showTraj, " Show Trajectory");
            if (newShowTraj != showTraj) { ObjectLauncherController.ShowTrajectory = newShowTraj; SettingsManager.MarkDirty(); }
            y += ROW + 2f;

            // ── Collapsible Launch Settings ──
            GUI.color = OverlayMenu.AccentColor;
            if (GUI.Button(new Rect(0, y, w, BTN_H), _showLaunchSettings ? "â–¼ Launch Settings" : "â–º Launch Settings"))
                _showLaunchSettings = !_showLaunchSettings;
            GUI.color = Color.white;
            y += BTN_H + 2f;

            if (_showLaunchSettings)
            {
                float lx = 10f;
                float lw = w - lx;
                float labelW = 110f;
                float sliderW = lw - labelW - 50f;
                float valW = 46f;

                // Full-Auto Delay
                GUI.Label(new Rect(lx, y, labelW, ROW), "FA Delay:");
                float faDelay = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.FullAutoDelay, 0.01f, 1f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), faDelay.ToString("F2"));
                if (Mathf.Abs(faDelay - ObjectLauncherController.FullAutoDelay) > 0.001f) { ObjectLauncherController.FullAutoDelay = faDelay; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Launch Force
                GUI.Label(new Rect(lx, y, labelW, ROW), "Force:");
                float force = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.LaunchForce, 50f, 10000f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), force.ToString("F0"));
                if (Mathf.Abs(force - ObjectLauncherController.LaunchForce) > 1f) { ObjectLauncherController.LaunchForce = force; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spawn Distance
                GUI.Label(new Rect(lx, y, labelW, ROW), "Distance:");
                float dist = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpawnDistance, 0.5f, 10f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), dist.ToString("F1"));
                if (Mathf.Abs(dist - ObjectLauncherController.SpawnDistance) > 0.01f) { ObjectLauncherController.SpawnDistance = dist; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spawn Offset X
                GUI.Label(new Rect(lx, y, labelW, ROW), "Offset X:");
                float offX = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpawnOffsetX, -5f, 5f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), offX.ToString("F2"));
                if (Mathf.Abs(offX - ObjectLauncherController.SpawnOffsetX) > 0.01f) { ObjectLauncherController.SpawnOffsetX = offX; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spawn Offset Y
                GUI.Label(new Rect(lx, y, labelW, ROW), "Offset Y:");
                float offY = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpawnOffsetY, -5f, 5f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), offY.ToString("F2"));
                if (Mathf.Abs(offY - ObjectLauncherController.SpawnOffsetY) > 0.01f) { ObjectLauncherController.SpawnOffsetY = offY; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Projectile Count
                GUI.Label(new Rect(lx, y, labelW, ROW), "Count:");
                float cnt = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.ProjectileCount, 1f, 25f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), ((int)cnt).ToString());
                if (Mathf.Abs(cnt - ObjectLauncherController.ProjectileCount) > 0.5f) { ObjectLauncherController.ProjectileCount = (int)cnt; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Projectile Spacing
                GUI.Label(new Rect(lx, y, labelW, ROW), "Spacing:");
                float spacing = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.ProjectileSpacing, 0.1f, 100f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), spacing.ToString("F1"));
                if (Mathf.Abs(spacing - ObjectLauncherController.ProjectileSpacing) > 0.05f) { ObjectLauncherController.ProjectileSpacing = spacing; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spin Velocity
                GUI.Label(new Rect(lx, y, labelW, ROW), "Spin:");
                float spin = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpinVelocity, 0f, 10000f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), spin.ToString("F0"));
                if (Mathf.Abs(spin - ObjectLauncherController.SpinVelocity) > 1f) { ObjectLauncherController.SpinVelocity = spin; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spawn Scale
                GUI.Label(new Rect(lx, y, labelW, ROW), "Scale:");
                float scale = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpawnScale, 0.1f, 10f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), scale.ToString("F1"));
                if (Mathf.Abs(scale - ObjectLauncherController.SpawnScale) > 0.01f) { ObjectLauncherController.SpawnScale = scale; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Aim Rotation
                bool aimRot = ObjectLauncherController.AimRotationEnabled;
                bool newAimRot = GUI.Toggle(new Rect(lx, y, lw, ROW), aimRot, " Aim Rotation (Face Launch Dir)");
                if (newAimRot != aimRot) { ObjectLauncherController.AimRotationEnabled = newAimRot; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Rotation X/Y/Z
                GUI.Label(new Rect(lx, y, labelW, ROW), "Rot X:");
                float rotX = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.RotationX, -180f, 180f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), rotX.ToString("F0"));
                if (Mathf.Abs(rotX - ObjectLauncherController.RotationX) > 0.5f) { ObjectLauncherController.RotationX = rotX; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                GUI.Label(new Rect(lx, y, labelW, ROW), "Rot Y:");
                float rotY = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.RotationY, -180f, 180f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), rotY.ToString("F0"));
                if (Mathf.Abs(rotY - ObjectLauncherController.RotationY) > 0.5f) { ObjectLauncherController.RotationY = rotY; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                GUI.Label(new Rect(lx, y, labelW, ROW), "Rot Z:");
                float rotZ = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.RotationZ, -180f, 180f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), rotZ.ToString("F0"));
                if (Mathf.Abs(rotZ - ObjectLauncherController.RotationZ) > 0.5f) { ObjectLauncherController.RotationZ = rotZ; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Pre-Activate on Menu Tap
                bool preAct = ObjectLauncherController.PreActivateMenuTap;
                bool newPreAct = GUI.Toggle(new Rect(lx, y, lw, ROW), preAct, " Pre-Activate on Menu Tap");
                if (newPreAct != preAct) { ObjectLauncherController.PreActivateMenuTap = newPreAct; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Force Delay
                GUI.Label(new Rect(lx, y, labelW, ROW), "Force Delay:");
                float fDelay = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.ForceDelay, 0f, 0.5f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), fDelay.ToString("F3"));
                if (Mathf.Abs(fDelay - ObjectLauncherController.ForceDelay) > 0.001f) { ObjectLauncherController.ForceDelay = fDelay; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                y += 4f;
            }

            // ── Collapsible Homing Settings ──
            GUI.color = OverlayMenu.AccentColor;
            if (GUI.Button(new Rect(0, y, w, BTN_H), _showHomingSettings ? "â–¼ Homing Settings" : "â–º Homing Settings"))
                _showHomingSettings = !_showHomingSettings;
            GUI.color = Color.white;
            y += BTN_H + 2f;

            if (_showHomingSettings)
            {
                float lx = 10f; // indent
                float lw = w - lx;
                float labelW = 110f;
                float sliderW = lw - labelW - 50f;
                float valW = 46f;

                // Filter (cycle button)
                int fi = (int)ObjectLauncherController.HomingFilter;
                GUI.Label(new Rect(lx, y, labelW, ROW), "Filter:");
                if (GUI.Button(new Rect(lx + labelW, y, lw - labelW, BTN_H), TargetFilterNames[fi]))
                {
                    fi = (fi + 1) % TargetFilterNames.Length;
                    ObjectLauncherController.HomingFilter = (TargetFilter)fi;
                    SettingsManager.MarkDirty();
                }
                y += BTN_H + 2f;

                // â”€â”€ Target Player display + switcher â”€â”€
                string tgtName = PlayerTargeting.SelectedPlayerName;
                GUI.color = OverlayMenu.SectionColor;
                GUI.Label(new Rect(lx, y, lw, ROW), $"Aiming at: {tgtName}");
                GUI.color = Color.white;
                y += ROW;

                // Prev / Next buttons
                float halfBtn = (lw - 4f) * 0.5f;
                if (GUI.Button(new Rect(lx, y, halfBtn, BTN_H), "\u25C0 Prev"))
                {
                    CyclePrev();
                }
                if (GUI.Button(new Rect(lx + halfBtn + 4f, y, halfBtn, BTN_H), "Next \u25B6"))
                {
                    PlayerTargeting.CycleTarget(ObjectLauncherController.HomingFilter);
                }
                y += BTN_H + 2f;

                // Quick-select player list
                var tgtPlayers = PlayerInfoController.Players;
                if (tgtPlayers.Count > 0)
                {
                    foreach (var tp in tgtPlayers)
                    {
                        if (tp.IsLocal) continue;
                        bool isCurrent = tgtName == tp.Username;
                        GUI.color = isCurrent ? Color.green : Color.white;
                        if (GUI.Button(new Rect(lx, y, lw, BTN_H), isCurrent ? $"\u2192 [{tp.SmallID}] {tp.Username}" : $"  [{tp.SmallID}] {tp.Username}"))
                        {
                            SelectPlayerBySmallId(tp.SmallID);
                        }
                        y += BTN_H + 1f;
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(lx, y, lw, ROW), "No players available");
                    y += ROW;
                }
                y += 4f;

                // Strength slider (1-50)
                GUI.Label(new Rect(lx, y, labelW, ROW), "Strength:");
                float str = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.HomingStrength, 1f, 50f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), str.ToString("0.0"));
                if (Mathf.Abs(str - ObjectLauncherController.HomingStrength) > 0.01f) { ObjectLauncherController.HomingStrength = str; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Duration slider (0-30, 0=unlimited)
                GUI.Label(new Rect(lx, y, labelW, ROW), "Duration:");
                float dur = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.HomingDuration, 0f, 30f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), dur <= 0f ? "INF" : dur.ToString("0.0"));
                if (Mathf.Abs(dur - ObjectLauncherController.HomingDuration) > 0.01f) { ObjectLauncherController.HomingDuration = dur; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Speed slider (0-500, 0=auto)
                GUI.Label(new Rect(lx, y, labelW, ROW), "Speed:");
                float spd = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.HomingSpeed, 0f, 500f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), spd <= 0f ? "Auto" : spd.ToString("0"));
                if (Mathf.Abs(spd - ObjectLauncherController.HomingSpeed) > 0.01f) { ObjectLauncherController.HomingSpeed = spd; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Stay Duration slider (0-30)
                GUI.Label(new Rect(lx, y, labelW, ROW), "Stay Dur:");
                float stay = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.HomingStayDuration, 0f, 30f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), stay.ToString("0.0"));
                if (Mathf.Abs(stay - ObjectLauncherController.HomingStayDuration) > 0.01f) { ObjectLauncherController.HomingStayDuration = stay; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Toggles row 1
                bool rotLock = ObjectLauncherController.HomingRotationLock;
                bool newRotLock = GUI.Toggle(new Rect(lx, y, lw * 0.5f, ROW), rotLock, " Rotation Lock");
                if (newRotLock != rotLock) { ObjectLauncherController.HomingRotationLock = newRotLock; SettingsManager.MarkDirty(); }

                bool targetHead = ObjectLauncherController.HomingTargetHead;
                bool newTargetHead = GUI.Toggle(new Rect(lx + lw * 0.5f, y, lw * 0.5f, ROW), targetHead, " Target Head");
                if (newTargetHead != targetHead) { ObjectLauncherController.HomingTargetHead = newTargetHead; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Toggles row 2
                bool momentum = ObjectLauncherController.HomingMomentum;
                bool newMomentum = GUI.Toggle(new Rect(lx, y, lw * 0.5f, ROW), momentum, " Momentum");
                if (newMomentum != momentum) { ObjectLauncherController.HomingMomentum = newMomentum; SettingsManager.MarkDirty(); }

                bool accelOn = ObjectLauncherController.HomingAccelEnabled;
                bool newAccel = GUI.Toggle(new Rect(lx + lw * 0.5f, y, lw * 0.5f, ROW), accelOn, " Acceleration");
                if (newAccel != accelOn) { ObjectLauncherController.HomingAccelEnabled = newAccel; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Accel rate (only when enabled)
                if (ObjectLauncherController.HomingAccelEnabled)
                {
                    GUI.Label(new Rect(lx, y, labelW, ROW), "Accel Rate:");
                    float ar = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.HomingAccelRate, 0.1f, 10f);
                    GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), ar.ToString("0.0"));
                    if (Mathf.Abs(ar - ObjectLauncherController.HomingAccelRate) > 0.01f) { ObjectLauncherController.HomingAccelRate = ar; SettingsManager.MarkDirty(); }
                    y += ROW + 2f;
                }

                y += 4f;
            }

            // ── Collapsible Cleanup Settings ──
            GUI.color = OverlayMenu.AccentColor;
            if (GUI.Button(new Rect(0, y, w, BTN_H), _showCleanupSettings ? "â–¼ Cleanup Settings" : "â–º Cleanup Settings"))
                _showCleanupSettings = !_showCleanupSettings;
            GUI.color = Color.white;
            y += BTN_H + 2f;

            if (_showCleanupSettings)
            {
                float lx = 10f;
                float lw = w - lx;
                float labelW = 110f;
                float sliderW = lw - labelW - 50f;
                float valW = 46f;

                // Auto Cleanup
                bool autoClean = ObjectLauncherController.AutoCleanupEnabled;
                bool newAutoClean = GUI.Toggle(new Rect(lx, y, lw, ROW), autoClean, " Auto Cleanup");
                if (newAutoClean != autoClean) { ObjectLauncherController.AutoCleanupEnabled = newAutoClean; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Cleanup Interval
                GUI.Label(new Rect(lx, y, labelW, ROW), "Cleanup Int:");
                float cleanInt = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.AutoCleanupInterval, 1f, 120f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), cleanInt.ToString("F0") + "s");
                if (Mathf.Abs(cleanInt - ObjectLauncherController.AutoCleanupInterval) > 0.5f) { ObjectLauncherController.AutoCleanupInterval = cleanInt; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Spawn-Force Delay
                GUI.Label(new Rect(lx, y, labelW, ROW), "Spn-F Delay:");
                float sfd = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.SpawnForceDelay, 0f, 0.5f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), sfd.ToString("F3"));
                if (Mathf.Abs(sfd - ObjectLauncherController.SpawnForceDelay) > 0.001f) { ObjectLauncherController.SpawnForceDelay = sfd; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Auto Despawn
                bool autoDespawn = ObjectLauncherController.AutoDespawnEnabled;
                bool newAutoDespawn = GUI.Toggle(new Rect(lx, y, lw, ROW), autoDespawn, " Auto Despawn");
                if (newAutoDespawn != autoDespawn) { ObjectLauncherController.AutoDespawnEnabled = newAutoDespawn; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                // Despawn Delay
                GUI.Label(new Rect(lx, y, labelW, ROW), "Despawn Del:");
                float dsDel = GUI.HorizontalSlider(new Rect(lx + labelW, y + 4f, sliderW, ROW), ObjectLauncherController.AutoDespawnDelay, 1f, 120f);
                GUI.Label(new Rect(lx + labelW + sliderW + 4f, y, valW, ROW), dsDel.ToString("F0") + "s");
                if (Mathf.Abs(dsDel - ObjectLauncherController.AutoDespawnDelay) > 0.5f) { ObjectLauncherController.AutoDespawnDelay = dsDel; SettingsManager.MarkDirty(); }
                y += ROW + 2f;

                y += 4f;
            }

            // Fire button
            GUI.color = Color.green;
            if (GUI.Button(new Rect(0, y, w * 0.5f - 2f, 30f), "FIRE"))
            {
                ObjectLauncherController.IsLauncherEnabled = true;
                ObjectLauncherController.LaunchObject();
            }
            GUI.color = Color.red;
            if (GUI.Button(new Rect(w * 0.5f, y, w * 0.5f, 30f), "Despawn Launched"))
            {
                ObjectLauncherController.DespawnLaunchedObjects();
            }
            GUI.color = Color.white;
            y += 34f;

            // Drop on player section
            y += 4f;
            GUI.color = OverlayMenu.AccentColor;
            GUI.Label(new Rect(0, y, w, ROW), "â”€â”€ Drop on Player â”€â”€");
            GUI.color = Color.white;
            y += ROW;

            // Show connected players as drop targets
            var players = PlayerInfoController.Players;
            if (players.Count == 0)
            {
                GUI.Label(new Rect(0, y, w, ROW), "No players (not in a server?)");
                y += ROW;
            }
            else
            {
                foreach (var p in players)
                {
                    if (p.IsLocal) continue;
                    string label = $"[{p.SmallID}] {p.Username}";
                    if (GUI.Button(new Rect(0, y, w, BTN_H), $"Drop on {label}"))
                    {
                        PlayerSpawnController.SpawnOnPlayerBySmallId(p.SmallID, p.Username);
                    }
                    y += BTN_H + 2f;
                }
            }

            // Search / set spawnable
            y += 6f;
            GUI.color = OverlayMenu.AccentColor;
            GUI.Label(new Rect(0, y, w, ROW), "â”€â”€ Search Spawnable â”€â”€");
            GUI.color = Color.white;
            y += ROW;

            GUI.Label(new Rect(0, y, 55f, ROW), "Search:");
            string newSearch = GUI.TextField(new Rect(58f, y, w - 58f, ROW), _launcherSearch);
            if (newSearch != _launcherSearch) { _launcherSearch = newSearch; _launcherSearchDirty = true; }
            y += ROW + 4f;

            if (_launcherSearchDirty)
            {
                RefreshLauncherSearch();
                _launcherSearchDirty = false;
            }

            int shown = 0;
            foreach (var entry in _launcherResults)
            {
                if (shown >= 20) break;
                bool isSelected = entry.BarcodeID == ObjectLauncherController.CurrentBarcodeID;
                GUI.color = isSelected ? Color.green : Color.white;
                if (GUI.Button(new Rect(0, y, w, BTN_H), entry.Title))
                {
                    ObjectLauncherController.CurrentBarcodeID = entry.BarcodeID;
                    ObjectLauncherController.CurrentItemName = entry.Title;
                    // Also set for drop on player
                    PlayerSpawnController.CurrentBarcode = entry.BarcodeID;
                    PlayerSpawnController.CurrentItemName = entry.Title;
                    SettingsManager.MarkDirty();
                }
                y += BTN_H + 1f;
                shown++;
            }
            GUI.color = Color.white;

            return y + 20f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TAB: PLAYERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static float DrawPlayersTab(float y, float w)
        {
            // Auto-refresh
            if (Time.time - _playerRefreshTime > 2f)
            {
                PlayerInfoController.ForceRefresh();
                _playerRefreshTime = Time.time;
            }

            GUI.color = Color.green;
            if (GUI.Button(new Rect(0, y, 150f, BTN_H), "Refresh Players"))
            {
                PlayerInfoController.ForceRefresh();
                _playerRefreshTime = Time.time;
            }
            GUI.color = Color.white;
            y += BTN_H + 6f;

            var players = PlayerInfoController.Players;
            if (players.Count == 0)
            {
                GUI.Label(new Rect(0, y, w, ROW), "No players (not in a server?)");
                return y + ROW;
            }

            foreach (var p in players)
            {
                // Player name header
                string localTag = p.IsLocal ? " (YOU)" : "";
                string spoofTag = p.IsSuspectedSpoof ? " [SPOOF?]" : "";
                GUI.color = p.IsSuspectedSpoof ? Color.red : (p.IsLocal ? OverlayMenu.AccentColor : Color.white);
                GUI.Label(new Rect(0, y, w, ROW), $"[{p.SmallID}] {p.Username}{localTag}{spoofTag}");
                GUI.color = Color.white;
                y += ROW;

                // SteamID
                if (p.SteamID != 0)
                {
                    GUI.Label(new Rect(10f, y, w - 10f, ROW), $"SteamID: {p.SteamID}");
                    y += ROW;
                }

                // Action buttons (not for local player)
                if (!p.IsLocal)
                {
                    float bx = 10f;
                    float bw = 90f;

                    // Steam Profile
                    GUI.color = OverlayMenu.AccentColor;
                    if (GUI.Button(new Rect(bx, y, bw, BTN_H), "Profile"))
                    {
                        if (p.SteamID != 0)
                            Application.OpenURL($"steam://url/SteamIDPage/{p.SteamID}");
                    }
                    bx += bw + 4f;

                    // Kick
                    GUI.color = OverlayMenu.SectionColor;
                    if (GUI.Button(new Rect(bx, y, bw, BTN_H), "Kick"))
                    {
                        KickPlayer(p.SmallID, p.Username);
                    }
                    bx += bw + 4f;

                    // Ban
                    GUI.color = Color.red;
                    if (GUI.Button(new Rect(bx, y, bw, BTN_H), "Ban"))
                    {
                        BanPlayer(p.SmallID, p.Username);
                    }

                    GUI.color = Color.white;
                    y += BTN_H + 2f;

                    // TP buttons row
                    bx = 10f;
                    GUI.color = Color.green;
                    if (GUI.Button(new Rect(bx, y, bw, BTN_H), "TP to"))
                    {
                        TeleportController.TeleportToPlayerBySmallID(p.SmallID, p.Username);
                    }
                    bx += bw + 4f;

                    GUI.color = Color.magenta;
                    if (GUI.Button(new Rect(bx, y, bw, BTN_H), "TP here"))
                    {
                        TeleportController.TeleportPlayerToMeBySmallID(p.SmallID, p.Username);
                    }

                    GUI.color = Color.white;
                    y += BTN_H + 2f;
                }

                y += 4f; // gap between players
            }

            return y + 20f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TAB: LEVELS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static float DrawLevelsTab(float y, float w)
        {
            GUI.Label(new Rect(0, y, 55f, ROW), "Search:");
            string newSearch = GUI.TextField(new Rect(58f, y, w - 58f, ROW), _levelSearch);
            if (newSearch != _levelSearch) { _levelSearch = newSearch; _levelSearchDirty = true; }
            y += ROW + 4f;

            GUI.color = OverlayMenu.SectionColor;
            if (GUI.Button(new Rect(0, y, 150f, BTN_H), "Reload Current"))
            {
                MapChangeController.ReloadLevel();
            }
            GUI.color = Color.white;
            y += BTN_H + 6f;

            if (_levelSearchDirty)
            {
                RefreshLevelSearch();
                _levelSearchDirty = false;
            }

            if (_levelResults.Count == 0)
            {
                GUI.Label(new Rect(0, y, w, ROW), "No levels found. Try a different search.");
                return y + ROW;
            }

            int shown = 0;
            foreach (var level in _levelResults)
            {
                if (shown >= 30) break;
                if (GUI.Button(new Rect(0, y, w, BTN_H), level.Title))
                {
                    MapChangeController.LoadLevel(level.BarcodeID, level.BarcodeRef);
                }
                y += BTN_H + 1f;
                shown++;
            }

            return y + 20f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TAB: SPAWN
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static float DrawSpawnTab(float y, float w)
        {
            // Refresh / load button
            if (!_spawnListLoaded || SpawnMenuController.TotalItemCount == 0)
            {
                GUI.color = OverlayMenu.SectionColor;
                if (GUI.Button(new Rect(0, y, w, BTN_H), "Load Spawnable Items"))
                {
                    SpawnMenuController.RefreshItemList();
                    _spawnListLoaded = true;
                }
                GUI.color = Color.white;
                y += BTN_H + 4f;
            }
            else
            {
                // Item count + refresh
                GUI.Label(new Rect(0, y, w * 0.6f, ROW), $"Items: {SpawnMenuController.FilteredItemCount}/{SpawnMenuController.TotalItemCount}");
                if (GUI.Button(new Rect(w * 0.6f, y, w * 0.4f, BTN_H), "Refresh"))
                {
                    SpawnMenuController.RefreshItemList();
                }
                y += BTN_H + 4f;
            }

            // Selected item display
            GUI.color = OverlayMenu.SectionColor;
            GUI.Label(new Rect(0, y, w, ROW), $"Selected: {SpawnMenuController.GetSelectedItemName()}");
            GUI.color = Color.white;
            y += ROW + 2f;

            // Search field
            GUI.Label(new Rect(0, y, 55f, ROW), "Search:");
            string newSearch = GUI.TextField(new Rect(58f, y, w - 58f, ROW), _spawnSearch);
            if (newSearch != _spawnSearch)
            {
                _spawnSearch = newSearch;
                SpawnMenuController.SearchQuery = _spawnSearch;
            }
            y += ROW + 4f;

            // Spawn distance slider
            float labelW = 100f;
            float sliderW = w - labelW - 50f;
            GUI.Label(new Rect(0, y, labelW, ROW), "Spawn Dist:");
            float dist = GUI.HorizontalSlider(new Rect(labelW, y + 4f, sliderW, ROW), SpawnMenuController.SpawnDistance, 0.5f, 20f);
            GUI.Label(new Rect(labelW + sliderW + 4f, y, 46f, ROW), dist.ToString("0.0"));
            if (Mathf.Abs(dist - SpawnMenuController.SpawnDistance) > 0.01f)
                SpawnMenuController.SpawnDistance = dist;
            y += ROW + 4f;

            // Spawn Selected button
            GUI.color = Color.green;
            if (GUI.Button(new Rect(0, y, w, BTN_H + 4f), $"SPAWN: {SpawnMenuController.GetSelectedItemName()}"))
            {
                SpawnMenuController.SpawnSelectedItem();
            }
            GUI.color = Color.white;
            y += BTN_H + 8f;

            // Page navigation
            string pageInfo = SpawnMenuController.GetPageInfo();
            float thirdW = (w - 8f) / 3f;
            if (GUI.Button(new Rect(0, y, thirdW, BTN_H), "â—€ Prev"))
                SpawnMenuController.PreviousPage();
            GUI.Label(new Rect(thirdW + 4f, y, thirdW, BTN_H), pageInfo);
            if (GUI.Button(new Rect(thirdW * 2f + 8f, y, thirdW, BTN_H), "Next â–¶"))
                SpawnMenuController.NextPage();
            y += BTN_H + 4f;

            // Item list
            var items = SpawnMenuController.GetCurrentPageItems();
            if (items.Count == 0)
            {
                GUI.Label(new Rect(0, y, w, ROW), _spawnListLoaded ? "No items match search." : "Press Load to populate list.");
                y += ROW;
            }
            else
            {
                var selected = SpawnMenuController.SelectedItem;
                foreach (var item in items)
                {
                    bool isCurrent = selected.HasValue && selected.Value.BarcodeID == item.BarcodeID;
                    GUI.color = isCurrent ? Color.green : Color.white;
                    if (GUI.Button(new Rect(0, y, w - 60f, BTN_H), isCurrent ? $"â–º {item.Title}" : $"  {item.Title}"))
                    {
                        // Select this item
                        SpawnMenuController.SearchQuery = ""; // clear search to show all
                        _spawnSearch = "";
                        // Find and select this item by spawning it
                    }
                    // Inline spawn button
                    GUI.color = Color.green;
                    if (GUI.Button(new Rect(w - 56f, y, 56f, BTN_H), "Spawn"))
                    {
                        SpawnMenuController.SpawnItem(item.BarcodeID, item.Title);
                    }
                    GUI.color = Color.white;
                    y += BTN_H + 1f;
                }
            }

            return y + 20f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static void RefreshLauncherSearch()
        {
            _launcherResults.Clear();
            try
            {
                // Use the ObjectLauncher's search infrastructure
                var allSpawnables = ObjectLauncherController.GetAllSpawnables();
                if (allSpawnables == null) return;

                string query = (_launcherSearch ?? "").Trim().ToLower();
                foreach (var kvp in allSpawnables)
                {
                    if (string.IsNullOrEmpty(query) || kvp.Value.ToLower().Contains(query))
                    {
                        _launcherResults.Add(new SpawnableEntry { BarcodeID = kvp.Key, Title = kvp.Value });
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[QuickMenu] Spawnable search error: {ex.Message}");
            }
        }

        private static void RefreshLevelSearch()
        {
            _levelResults.Clear();
            try
            {
                MapChangeController.SearchQuery = _levelSearch;
                MapChangeController.LoadAllLevels();
                MapChangeController.Search();

                // Access filtered results via public method
                _levelResults = MapChangeController.GetFilteredLevels();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[QuickMenu] Level search error: {ex.Message}");
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  KICK / BAN (via reflection on LabFusion)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static void ResolveKickBan()
        {
            if (_kickBanResolved) return;
            _kickBanResolved = true;

            try
            {
                var nhType = typeof(NetworkHelper);
                _kickMethod = nhType.GetMethod("KickUser", BindingFlags.Public | BindingFlags.Static);
                _banMethod = nhType.GetMethod("BanUser", BindingFlags.Public | BindingFlags.Static);

                if (_kickMethod != null)
                    Main.MelonLog.Msg("[QuickMenu] KickUser resolved");
                if (_banMethod != null)
                    Main.MelonLog.Msg("[QuickMenu] BanUser resolved");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[QuickMenu] Kick/Ban resolve error: {ex.Message}");
            }
        }

        private static void KickPlayer(byte smallId, string name)
        {
            try
            {
                if (_kickMethod == null) { Main.MelonLog.Warning("[QuickMenu] KickUser not available"); return; }

                var pid = FindPlayerID(smallId);
                if (pid == null) { Main.MelonLog.Warning($"[QuickMenu] PlayerID not found for {name}"); return; }

                _kickMethod.Invoke(null, new object[] { pid });
                Main.MelonLog.Msg($"[QuickMenu] Kicked {name}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, $"Kicked {name}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[QuickMenu] Kick error: {ex.Message}");
            }
        }

        private static void BanPlayer(byte smallId, string name)
        {
            try
            {
                if (_banMethod == null) { Main.MelonLog.Warning("[QuickMenu] BanUser not available"); return; }

                var pid = FindPlayerID(smallId);
                if (pid == null) { Main.MelonLog.Warning($"[QuickMenu] PlayerID not found for {name}"); return; }

                _banMethod.Invoke(null, new object[] { pid });
                Main.MelonLog.Msg($"[QuickMenu] Banned {name}");
                NotificationHelper.Send(BoneLib.Notifications.NotificationType.Success, $"Banned {name}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[QuickMenu] Ban error: {ex.Message}");
            }
        }

        private static PlayerID FindPlayerID(byte smallId)
        {
            try
            {
                foreach (var pid in PlayerIDManager.PlayerIDs)
                {
                    if (pid != null && pid.SmallID == smallId)
                        return pid;
                }
            }
            catch { }
            return null;
        }

        // â”€â”€ Homing target helpers â”€â”€

        /// <summary>Cycle to previous player (reverse of CycleTarget).</summary>
        private static void CyclePrev()
        {
            var players = PlayerTargeting.GetCachedPlayers();
            if (players == null || players.Count == 0) return;

            // Find current index
            var current = PlayerTargeting.SelectedPlayer;
            int currentIdx = -1;
            if (current != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Rig == current) { currentIdx = i; break; }
                }
            }

            int prevIdx = (currentIdx <= 0) ? players.Count - 1 : currentIdx - 1;
            PlayerTargeting.SelectedPlayer = players[prevIdx].Rig;
        }

        /// <summary>Select a homing target by Fusion SmallID.</summary>
        private static void SelectPlayerBySmallId(byte smallId)
        {
            var players = PlayerTargeting.GetCachedPlayers();
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].SmallID == smallId)
                {
                    PlayerTargeting.SelectedPlayer = players[i].Rig;
                    // Auto-switch filter to SELECTED so it takes effect immediately
                    if (ObjectLauncherController.HomingFilter != TargetFilter.SELECTED)
                    {
                        ObjectLauncherController.HomingFilter = TargetFilter.SELECTED;
                        SettingsManager.MarkDirty();
                    }
                    return;
                }
            }
        }
    }
}
