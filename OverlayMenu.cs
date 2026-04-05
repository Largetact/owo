using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BonelabUtilityMod
{
    public static class OverlayMenu
    {
        private static bool _visible = false;
        private static int _currentPage = 0;
        private static float _scroll = 0f;

        // Avatar search state
        private static string _avatarSearchQuery = "";
        private static string _prevAvatarSearchQuery = "";
        private static int _avatarSearchPage = 0;
        private const int AVATAR_ITEMS_PER_PAGE = 25;

        // Spawnable search states for various pages
        private static string _launcherSearchQuery = "";
        private static string _spawnOnPlayerSearchQuery = "";
        private static string _waypointSearchQuery = "";
        private static string _explosivePunchSearchQuery = "";
        private static string _groundSlamSearchQuery = "";
        private static string _dashSearchQuery = "";
        private static string _flightSearchQuery = "";
        private static string _expImpactSearchQuery = "";
        private static List<(string barcode, string title)> _launcherSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _spawnOnPlayerSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _waypointSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _explosivePunchSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _groundSlamSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _dashSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _flightSearchResults = new List<(string, string)>();
        private static List<(string barcode, string title)> _expImpactSearchResults = new List<(string, string)>();
        private static string _randExplodeSearchQuery = "";
        private static List<(string barcode, string title)> _randExplodeSearchResults = new List<(string, string)>();
        private static string _bhopEffectSearchQuery = "";
        private static List<(string barcode, string title)> _bhopEffectSearchResults = new List<(string, string)>();

        // Cosmetic barcode search states for combat features
        private static string _epCosmeticSearchQuery = "";
        private static List<(string barcode, string title)> _epCosmeticSearchResults = new List<(string, string)>();
        private static string _epLeftCustomSearchQuery = "";
        private static List<(string barcode, string title)> _epLeftCustomSearchResults = new List<(string, string)>();
        private static string _epLeftCosSearchQuery = "";
        private static List<(string barcode, string title)> _epLeftCosSearchResults = new List<(string, string)>();
        private static string _epRightCustomSearchQuery = "";
        private static List<(string barcode, string title)> _epRightCustomSearchResults = new List<(string, string)>();
        private static string _epRightCosSearchQuery = "";
        private static List<(string barcode, string title)> _epRightCosSearchResults = new List<(string, string)>();
        private static string _gsCosmeticSearchQuery = "";
        private static List<(string barcode, string title)> _gsCosmeticSearchResults = new List<(string, string)>();
        private static string _eiCosmeticSearchQuery = "";
        private static List<(string barcode, string title)> _eiCosmeticSearchResults = new List<(string, string)>();
        private static string _dashCosmeticSearchQuery = "";
        private static List<(string barcode, string title)> _dashCosmeticSearchResults = new List<(string, string)>();
        private static string _flightCosmeticSearchQuery = "";
        private static List<(string barcode, string title)> _flightCosmeticSearchResults = new List<(string, string)>();

        // Spoofing text input buffers
        private static string _spoofUsernameInput = "Player";
        private static string _spoofNicknameInput = "";
        private static string _spoofDescriptionInput = "";

        // Block system search states
        private static string _playerBlockSearchQuery = "";
        private static List<TeleportController.PlayerInfo> _playerBlockSearchResults = new List<TeleportController.PlayerInfo>();
        private static string _itemBlockSearchQuery = "";
        private static List<(string barcode, string title)> _itemBlockSearchResults = new List<(string, string)>();
        private static string _localBlockSearchQuery = "";
        private static List<(string barcode, string title)> _localBlockSearchResults = new List<(string, string)>();

        // Utility page collapsible sections (all collapsed by default to avoid lag)
        private static bool _utilDespawn = false;
        private static bool _utilAntiDespawn = false;
        private static bool _utilAntiGrab = false;
        private static bool _utilSpawnLimiter = false;
        private static bool _utilForceSpawner = false;
        private static bool _utilWindSfx = false;
        private static bool _utilMapChange = false;
        private static bool _utilNotifications = false;
        private static bool _utilXyzScale = false;
        private static bool _utilSpawnMenu = false;

        // Player page collapsible sections
        private static bool _playerGodMode = false;
        private static bool _playerRagdoll = false;
        private static bool _playerAntiConstraint = false;
        private static bool _playerAntiKnockout = false;
        private static bool _playerUnbreakGrip = false;
        private static bool _playerAntiGravChange = false;
        private static bool _playerForceGrab = false;
        private static bool _playerDefaultWorld = false;
        private static bool _playerGhostMode = false;
        private static bool _playerAntiRagdoll = false;
        private static bool _playerAntiSlowmo = false;
        private static bool _playerAntiTeleport = false;

        // Movement page collapsible sections
        private static bool _movDash = false;
        private static bool _movFlight = false;
        private static bool _movBunnyHop = false;
        private static bool _movAutoRun = false;
        private static bool _movSpinbot = false;
        private static bool _movTeleport = false;
        private static bool _movWaypoints = false;

        // Weapons page collapsible sections
        private static bool _weapChaosGun = false;
        private static bool _weapFullAuto = false;
        private static bool _weapInfAmmo = false;
        private static bool _weapDamageMult = false;

        // Gun Visuals page collapsible sections
        private static bool _gvCustomColor = false;
        private static bool _gvShaderLib = false;
        private static bool _gvTexEditor = false;

        // Combat page collapsible sections
        private static bool _combatExpPunch = false;
        private static bool _combatGroundSlam = false;
        private static bool _combatExpImpact = false;
        private static bool _combatRandExplode = false;
        private static bool _combatSpawnOnPlayer = false;
        private static bool _combatWaypointProj = false;
        private static bool _combatObjLauncher = false;
        private static bool _combatRecoilRagdoll = false;
        private static bool _combatHomingThrow = false;
        private static bool _combatESP = false;
        private static bool _combatItemESP = false;
        private static bool _combatAimAssist = false;

        // Utility page collapsible sections (new)
        private static bool _utilAINpc = false;
        private static bool _utilAvatarLogger = false;
        private static bool _utilPlayerActionLog = false;
        private static bool _utilSpawnLogger = false;
        private static bool _utilLobbyBrowser = false;
        private static bool _utilSpoofing = false;
        private static bool _utilAutoUpdater = false;

        // Spoofing mod reflection cache
        private static Type _spoofingModType;
        private static bool _spoofingModChecked;
        private static System.Reflection.PropertyInfo _spoofUserEnabled;
        private static System.Reflection.PropertyInfo _spoofNickEnabled;
        private static System.Reflection.PropertyInfo _spoofDescEnabled;
        private static System.Reflection.PropertyInfo _spoofFakeUsername;
        private static System.Reflection.PropertyInfo _spoofFakeNickname;
        private static System.Reflection.PropertyInfo _spoofFakeDescription;

        // Cosmetics page collapsible sections
        private static bool _cosWeepingAngel = false;
        private static bool _cosAvatarCopier = false;
        private static int _acPlayerPage = 0;
        private static bool _cosBodyLogColor = false;
        private static bool _cosAvatarFx = false;
        private static bool _cosHolsterHider = false;

        // Network page collapsible sections
        private static bool _netAutoHost = false;
        private static bool _netServerQueue = false;
        private static bool _netServerSettings = false;
        private static bool _netFreezePlayer = false;
        private static int _fpPlayerPage = 0;
        private static bool _netBlockSystem = false;
        private static bool _netScreenShare = false;
        private static bool _netPlayerInfo = false;

        // Global search
        private static string _globalSearchQuery = "";
        private static string _expandAfterSearch = null;

        private static void SearchSpawnables(string query, List<(string barcode, string title)> results)
        {
            results.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;
            string q = query.ToLowerInvariant();
            var all = ObjectLauncherController.GetAllSpawnables();
            int count = 0;
            foreach (var kv in all)
            {
                if (kv.Value.ToLowerInvariant().Contains(q) || kv.Key.ToLowerInvariant().Contains(q))
                {
                    results.Add((kv.Key, kv.Value));
                    if (++count >= 20) break;
                }
            }
        }

        private static float DrawSpawnableSearch(string label, ref string query, List<(string barcode, string title)> results,
            Action<string, string> onSelect, float y, float w)
        {
            y = Section($"── {label} ──", y, w);
            GUI.Label(new Rect(PAD, y, 70f, ROW), "Search:");
            string nq = GUI.TextField(new Rect(PAD + 70f, y, w - 140f, ROW), query ?? "");
            if (GUI.Button(new Rect(PAD + w - 60f, y, 55f, ROW), "Go", _cachedButtonStyle))
                SearchSpawnables(nq, results);
            if (nq != query) { query = nq; SearchSpawnables(query, results); }
            y += ROW + 2f;

            for (int i = 0; i < results.Count; i++)
            {
                var (barcode, title) = results[i];
                if (GUI.Button(new Rect(PAD, y, w, ROW), title, _cachedButtonStyle))
                {
                    onSelect(barcode, title);
                    SettingsManager.MarkDirty();
                }
                y += ROW + 1f;
            }
            return y + 4f;
        }

        // ── Overlay customization ──
        private static float _menuOpacity = 1f;
        private static int _fontSize = 14;
        // Base colors (what the user has configured / what presets set)
        private static Color _baseAccentColor = Color.cyan;
        private static Color _baseSectionColor = Color.yellow;
        private static Color _baseBgColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);
        private static Color _baseGradientEndColor = Color.magenta;
        // Active colors (may be rainbow-shifted each frame)
        private static Color _accentColor = Color.cyan;
        private static Color _sectionColor = Color.yellow;
        private static Color _bgColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);
        private static Color _gradientEndColor = Color.magenta;
        private static bool _rainbowTitle = false;
        private static float _rainbowHue = 0f;
        private static bool _gradientEnabled = false;

        public static float MenuOpacity { get => _menuOpacity; set => _menuOpacity = Mathf.Clamp01(value); }
        public static int FontSize { get => _fontSize; set => _fontSize = Mathf.Clamp(value, 10, 24); }
        public static Color AccentColor { get => _baseAccentColor; set { _baseAccentColor = value; if (!_rainbowTitle) _accentColor = value; } }
        public static Color SectionColor { get => _baseSectionColor; set { _baseSectionColor = value; if (!_rainbowTitle) _sectionColor = value; } }
        public static Color BgColor { get => _baseBgColor; set { _baseBgColor = value; if (!_rainbowTitle) { _bgColor = value; _bgTexDirty = true; } } }
        public static bool RainbowTitle { get => _rainbowTitle; set => _rainbowTitle = value; }
        public static bool GradientEnabled { get => _gradientEnabled; set => _gradientEnabled = value; }
        public static Color GradientEndColor { get => _baseGradientEndColor; set { _baseGradientEndColor = value; if (!_rainbowTitle) _gradientEndColor = value; } }
        // Expose the cached window style so Quick Menu can share it
        public static GUIStyle WindowStyle => _cachedWindowStyle;

        // ── Cached GUIStyles (allocated once, not per frame) ──
        private static GUIStyle _cachedHeaderStyle;
        private static GUIStyle _cachedSectionStyle;
        private static GUIStyle _cachedButtonStyle;
        private static GUIStyle _cachedToggleStyle;
        private static GUIStyle _cachedLabelStyle;
        private static GUIStyle _cachedWindowStyle;
        private static Texture2D _bgTex;
        private static bool _bgTexDirty = true;
        private static Color[] _bgPixels = null;
        private static bool _stylesCached = false;

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var t = new Texture2D(w, h) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels(pix);
            t.Apply();
            return t;
        }

        /// <summary>Call on scene/level change to force full style + texture rebuild next frame.</summary>
        public static void InvalidateStyles()
        {
            _stylesCached = false;
            _bgTexDirty = true;
        }

        private static void EnsureStyles()
        {
            if (_stylesCached && _cachedHeaderStyle != null && !_bgTexDirty) return;

            if (!_stylesCached || _cachedHeaderStyle == null)
            {
                _cachedHeaderStyle = new GUIStyle(GUI.skin.label);
                _cachedSectionStyle = new GUIStyle(GUI.skin.label);
                _cachedButtonStyle = new GUIStyle(GUI.skin.button);
                _cachedToggleStyle = new GUIStyle(GUI.skin.toggle);
                _cachedLabelStyle = new GUIStyle(GUI.skin.label);
                _cachedWindowStyle = new GUIStyle(GUI.skin.window);
            }

            if (_bgTexDirty || _bgTex == null)
            {
                if (_bgTex != null)
                {
                    // Reuse cached pixel array — zero GC alloc per frame in rainbow mode
                    if (_bgPixels == null || _bgPixels.Length != 16)
                        _bgPixels = new Color[16];
                    for (int i = 0; i < 16; i++) _bgPixels[i] = _bgColor;
                    _bgTex.SetPixels(_bgPixels);
                    _bgTex.Apply();
                }
                else
                {
                    _bgTex = MakeTex(4, 4, _bgColor);
                    _bgPixels = null; // will be allocated on next dirty
                }
                _cachedWindowStyle.normal.background = _bgTex;
                _cachedWindowStyle.onNormal.background = _bgTex;
                _bgTexDirty = false;
            }

            _stylesCached = true;
        }

        private static readonly string[] PageNames = new string[]
        {
            "Movement",
            "Player",
            "Weapons",
            "Gun Visuals",
            "Combat",
            "Cosmetics",
            "Server",
            "Utility",
            "Avatar",
            "Keybinds",
            "Settings"
        };

        // Window dimensions
        private const int MAIN_WIDTH = 720;
        private const int MAIN_HEIGHT = 720;
        private const int SIDEBAR_WIDTH = 185;
        private const int SIDEBAR_GAP = 8;
        private const float ROW = 28f;
        private const float SLD = 20f;
        private const float PAD = 12f;

        public static bool IsVisible => _visible;

        public static void CheckInput()
        {
            var key = KeybindManager.GetKey("OverlayToggle");
            if (key != KeyCode.None && Input.GetKeyDown(key))
                _visible = !_visible;
        }

        public static void Draw()
        {
            if (!_visible) return;

            try
            {
                EnsureStyles();
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("OverlayMenu.EnsureStyles: " + ex.Message);
                return;
            }

            // Apply opacity
            Color prevGuiColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);

            // Rainbow animation — shift ALL active colors each frame
            if (_rainbowTitle)
            {
                _rainbowHue = (_rainbowHue + Time.deltaTime * 0.3f) % 1f;
                _accentColor = Color.HSVToRGB(_rainbowHue, 0.8f, 1f);
                _sectionColor = Color.HSVToRGB((_rainbowHue + 0.33f) % 1f, 0.7f, 1f);
                _gradientEndColor = Color.HSVToRGB((_rainbowHue + 0.5f) % 1f, 0.8f, 1f);
                Color newBg = Color.HSVToRGB(_rainbowHue, 0.3f, 0.12f);
                newBg.a = _baseBgColor.a;
                _bgColor = newBg;
                _bgTexDirty = true;
            }

            if (Event.current != null)
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    _scroll += Event.current.delta.y * 30f;
                    if (_scroll < 0f) _scroll = 0f;
                }
            }

            int mainX = (Screen.width - MAIN_WIDTH) / 2;
            int mainY = (Screen.height - MAIN_HEIGHT) / 2;
            int sidebarX = mainX - SIDEBAR_WIDTH - SIDEBAR_GAP;

            GUI.WindowFunction sidebarFunc = (Action<int>)DrawSidebar;
            GUI.WindowFunction mainFunc = (Action<int>)DrawMainWindow;

            try
            {
                GUI.Window(99901, new Rect(sidebarX, mainY, SIDEBAR_WIDTH, MAIN_HEIGHT), sidebarFunc, "Pages", _cachedWindowStyle);
                GUI.BringWindowToFront(99901);

                string title = Main.GetCyclingName() + " V3  |  " + PageNames[_currentPage] + "  |  ` Close  |  Scroll";
                GUI.Window(99900, new Rect(mainX, mainY, MAIN_WIDTH, MAIN_HEIGHT), mainFunc, title, _cachedWindowStyle);
                GUI.BringWindowToFront(99900);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("OverlayMenu.Draw window: " + ex.Message);
            }

            GUI.color = prevGuiColor;
        }

        // ═══════════════════════════════════════════
        // Sidebar — page buttons
        // ═══════════════════════════════════════════
        private static void DrawSidebar(int id)
        {
            try
            {
                float y = 25f;
                float w = SIDEBAR_WIDTH - 20f;
                Color dimAccent = new Color(
                    Mathf.Lerp(1f, _accentColor.r, 0.18f),
                    Mathf.Lerp(1f, _accentColor.g, 0.18f),
                    Mathf.Lerp(1f, _accentColor.b, 0.18f),
                    _menuOpacity);
                for (int i = 0; i < PageNames.Length; i++)
                {
                    GUI.color = (i == _currentPage) ? _accentColor * new Color(1, 1, 1, _menuOpacity) : dimAccent;
                    if (GUI.Button(new Rect(10f, y, w, 34f), PageNames[i], _cachedButtonStyle))
                    {
                        _currentPage = i;
                        _scroll = 0f;
                    }
                    y += 38f;
                }
                GUI.color = new Color(1, 1, 1, _menuOpacity);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("OverlayMenu.DrawSidebar: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════
        // Main window — dispatches to page
        // ═══════════════════════════════════════════
        private static void DrawMainWindow(int id)
        {
            try
            {
                float w = MAIN_WIDTH - PAD * 2f;
                float y = 25f;

                // Global search bar (always visible, not affected by scroll)
                GUI.Label(new Rect(PAD, y, 55f, ROW), "Search:");
                string newSearch = GUI.TextField(new Rect(PAD + 55f, y, w - 120f, ROW), _globalSearchQuery ?? "");
                if (newSearch != _globalSearchQuery) { _globalSearchQuery = newSearch; _scroll = 0f; }
                if (GUI.Button(new Rect(PAD + w - 55f, y, 55f, ROW), "Clear", _cachedButtonStyle))
                { _globalSearchQuery = ""; _scroll = 0f; }
                y += ROW + 6f;

                if (!string.IsNullOrEmpty(_globalSearchQuery))
                {
                    y -= _scroll;
                    DrawSearchResults(y, w);
                    return;
                }

                y -= _scroll;

                switch (_currentPage)
                {
                    case 0: DrawMovementPage(y, w); break;
                    case 1: DrawPlayerPage(y, w); break;
                    case 2: DrawWeaponsPage(y, w); break;
                    case 3: DrawGunVisualsPage(y, w); break;
                    case 4: DrawCombatPage(y, w); break;
                    case 5: DrawCosmeticsPage(y, w); break;
                    case 6: DrawServerPage(y, w); break;
                    case 7: DrawUtilityPage(y, w); break;
                    case 8: DrawAvatarPage(y, w); break;
                    case 9: DrawKeybindsPage(y, w); break;
                    case 10: DrawSettingsPage(y, w); break;
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("OverlayMenu.DrawMainWindow (page " + _currentPage + "): " + ex);
            }
        }

        // ═══════════════════════════════════════════
        //  Helpers — Rect-based, styles created inline
        // ═══════════════════════════════════════════

        /// <summary>
        /// Converts a Color to a 6-digit hex string (RRGGBB) without relying on ColorUtility
        /// which may not be available in IL2CPP at runtime.
        /// </summary>
        private static string ColorToHex(Color c)
        {
            int r = Mathf.Clamp((int)(c.r * 255f), 0, 255);
            int g = Mathf.Clamp((int)(c.g * 255f), 0, 255);
            int b = Mathf.Clamp((int)(c.b * 255f), 0, 255);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        /// <summary>
        /// Builds a Unity rich-text string with per-character color gradient,
        /// same approach as the Unity Gradient Maker web tool.
        /// </summary>
        private static string GradientText(string text, Color start, Color end)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int len = text.Length;
            if (len == 1) return $"<color=#{ColorToHex(start)}>{text}</color>";

            var sb = new System.Text.StringBuilder(len * 28);
            for (int i = 0; i < len; i++)
            {
                if (text[i] == ' ') { sb.Append(' '); continue; }
                float t = (float)i / (len - 1);
                Color c = Color.Lerp(start, end, t);
                sb.Append("<color=#");
                sb.Append(ColorToHex(c));
                sb.Append('>');
                sb.Append(text[i]);
                sb.Append("</color>");
            }
            return sb.ToString();
        }

        private static float Header(string text, float y, float w)
        {
            _cachedHeaderStyle.fontSize = _fontSize + 3;
            _cachedHeaderStyle.richText = _gradientEnabled;
            if (_gradientEnabled)
            {
                _cachedHeaderStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(PAD, y, w, 30f), GradientText(text, _accentColor, _gradientEndColor), _cachedHeaderStyle);
            }
            else
            {
                _cachedHeaderStyle.normal.textColor = _accentColor;
                GUI.Label(new Rect(PAD, y, w, 30f), text, _cachedHeaderStyle);
            }
            return y + 30f;
        }

        private static float CollapsibleHeader(string text, ref bool expanded, float y, float w)
        {
            if (_expandAfterSearch != null && text == _expandAfterSearch)
            {
                expanded = true;
                _expandAfterSearch = null;
            }
            string prefix = expanded ? "▼ " : "► ";
            _cachedHeaderStyle.fontSize = _fontSize + 3;
            _cachedHeaderStyle.richText = _gradientEnabled;
            if (_gradientEnabled)
            {
                _cachedHeaderStyle.normal.textColor = Color.white;
                if (GUI.Button(new Rect(PAD, y, w, 30f), GradientText(prefix + text, _accentColor, _gradientEndColor), _cachedHeaderStyle))
                    expanded = !expanded;
            }
            else
            {
                _cachedHeaderStyle.normal.textColor = _accentColor;
                if (GUI.Button(new Rect(PAD, y, w, 30f), prefix + text, _cachedHeaderStyle))
                    expanded = !expanded;
            }
            return y + 30f;
        }

        private static float Section(string text, float y, float w)
        {
            _cachedSectionStyle.fontSize = _fontSize + 1;
            _cachedSectionStyle.richText = _gradientEnabled;
            if (_gradientEnabled)
            {
                _cachedSectionStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(PAD, y, w, 26f), GradientText(text, _sectionColor, _gradientEndColor), _cachedSectionStyle);
            }
            else
            {
                _cachedSectionStyle.normal.textColor = _sectionColor;
                GUI.Label(new Rect(PAD, y, w, 26f), text, _cachedSectionStyle);
            }
            return y + 26f;
        }

        private static float Label(string text, float y, float w)
        {
            _cachedLabelStyle.fontSize = _fontSize;
            _cachedLabelStyle.normal.textColor = _sectionColor;
            GUI.Label(new Rect(PAD, y, w, ROW), text, _cachedLabelStyle);
            return y + ROW;
        }

        private static float Toggle(ref bool value, string label, float y, float w)
        {
            _cachedToggleStyle.fontSize = _fontSize;
            _cachedToggleStyle.normal.textColor = Color.white;
            _cachedToggleStyle.onNormal.textColor = _accentColor;
            bool nv = GUI.Toggle(new Rect(PAD, y, w, ROW), value, "  " + label, _cachedToggleStyle);
            if (nv != value) { value = nv; SettingsManager.MarkDirty(); }
            return y + ROW;
        }

        private static float Slider(string label, ref float value, float min, float max, float y, float w, string fmt = "F1")
        {
            GUI.Label(new Rect(PAD, y, w, 20f), label + ": " + value.ToString(fmt));
            y += 20f;
            float nv = GUI.HorizontalSlider(new Rect(PAD, y, w - 20f, SLD), value, min, max);
            if (Math.Abs(nv - value) > 0.001f) { value = nv; SettingsManager.MarkDirty(); }
            return y + SLD + 4f;
        }

        private static float Button(string label, float y, float bw, float bh, Action onClick)
        {
            Color prev = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD, y, bw, bh), label, _cachedButtonStyle))
                onClick?.Invoke();
            GUI.color = prev;
            return y + bh + 4f;
        }

        private static float Gap(float y, float g = 14f) { return y + g; }

        private static float EnumCycle(string label, ref MatrixMode mode, float y, float w)
        {
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (MatrixMode)(((int)mode + 1) % 3);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref ExplosionType mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(ExplosionType));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (ExplosionType)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref TargetFilter mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(TargetFilter));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (TargetFilter)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref RagdollMode mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(RagdollMode));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (RagdollMode)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref RagdollBinding mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(RagdollBinding));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (RagdollBinding)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref RagdollHand mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(RagdollHand));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (RagdollHand)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref DespawnFilter mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(DespawnFilter));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (DespawnFilter)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref SpinDirection mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(SpinDirection));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (SpinDirection)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref ESPMode mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(ESPMode));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (ESPMode)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref ESPColorMode mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(ESPColorMode));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (ESPColorMode)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref ItemESPFilter mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(ItemESPFilter));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (ItemESPFilter)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref AimTarget mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(AimTarget));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (AimTarget)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        private static float EnumCycle(string label, ref CompensationSmoothing mode, float y, float w)
        {
            var names = System.Enum.GetNames(typeof(CompensationSmoothing));
            float labelW = w * 0.45f;
            float btnW = w * 0.5f;
            GUI.Label(new Rect(PAD, y, labelW, ROW), label + ": " + mode);
            Color ec = GUI.color;
            GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
            if (GUI.Button(new Rect(PAD + labelW + 5f, y, btnW, ROW), mode.ToString(), _cachedButtonStyle))
            {
                mode = (CompensationSmoothing)(((int)mode + 1) % names.Length);
                SettingsManager.MarkDirty();
            }
            GUI.color = ec;
            return y + ROW + 2f;
        }

        // ═══════════════════════════════════════════
        //  Search functionality
        // ═══════════════════════════════════════════
        private static readonly (string name, string keywords, int page, string section)[] SearchEntries = new[]
        {
            ("God Mode", "god invincible health immortal", 0, "GOD MODE"),
            ("Dash", "dash speed teleport movement lock-on", 0, "DASH"),
            ("Flight", "fly flight hover soar", 0, "FLIGHT"),
            ("Ragdoll", "ragdoll fall flop physics tantrum", 0, "RAGDOLL"),
            ("Anti-Constraint", "constraint stuck joints break", 0, "ANTI-CONSTRAINT"),
            ("Anti-Knockout", "knockout unconscious stun wake", 0, "ANTI-KNOCKOUT"),
            ("Unbreakable Grip", "grip hold grab strength unbreakable", 0, "UNBREAKABLE GRIP"),
            ("Anti-Gravity Change", "gravity lock earth loop", 0, "ANTI-GRAVITY CHANGE"),
            ("Force Grab", "force grab telekinesis pull push", 0, "FORCE GRAB"),
            ("Auto Run", "auto run walk movement", 0, "AUTO RUN"),
            ("Default World", "default world level map startup", 0, "DEFAULT WORLD"),
            ("Explosive Punch", "explosive punch fist hit black flash", 1, "EXPLOSIVE PUNCH"),
            ("Ground Slam", "ground slam pound stomp", 1, "GROUND SLAM"),
            ("Explosive Impact", "explosive impact collision crash", 1, "EXPLOSIVE IMPACT"),
            ("Random Explode", "random explode chance bomb surprise", 1, "RANDOM EXPLODE"),
            ("Full Auto", "full auto fire rate rapid shoot", 1, "FULL AUTO"),
            ("Infinite Ammo", "infinite ammo unlimited bullets magazine", 1, "INFINITE AMMO"),
            ("Gun Modifier", "gun modifier damage recoil weight bounce reload", 1, "GUN MODIFIER"),
            ("Custom Gun Color", "custom gun color rainbow emission reflection transparency rgb gradient", 1, "CUSTOM GUN COLOR"),
            ("Spawn on Player", "spawn player drop item overhead homing", 1, "SPAWN ON PLAYER"),
            ("Waypoint Projectile", "waypoint projectile path homing guide", 1, "WAYPOINT PROJECTILE"),
            ("Object Launcher", "launcher shoot spawn object projectile homing", 2, "OBJECT LAUNCHER"),
            ("Weeping Angel", "weeping angel freeze statue look", 3, "WEEPING ANGEL"),
            ("Avatar Copier", "avatar copier clone copy player nickname", 3, "AVATAR COPIER"),
            ("BodyLog Color", "bodylog color body log hologram ball line radial", 3, "BODYLOG COLOR"),
            ("Despawn All", "despawn clear remove cleanup all", 4, "DESPAWN ALL"),
            ("Anti-Despawn", "anti despawn keep persist prevent", 4, "ANTI-DESPAWN"),
            ("Anti-Grab", "anti grab prevent steal protect", 4, "ANTI-GRAB"),
            ("Spawn Limiter", "spawn limiter anti crash rate limit delay protect", 4, "SPAWN LIMITER"),
            ("Force Spawner", "force spawner unredact spawn distance", 4, "FORCE SPAWNER"),
            ("Remove Wind SFX", "wind sound audio sfx remove quiet", 4, "REMOVE WIND SFX"),
            ("Teleport", "teleport warp position save player", 4, "TELEPORT"),
            ("Waypoints", "waypoint marker save spawn", 4, "WAYPOINTS"),
            ("Map Change", "map change level reload", 4, "MAP CHANGE"),
            ("Notifications", "notification popup alert message", 4, "NOTIFICATIONS"),
            ("Fix Wobbly Avatar", "fix wobbly avatar shake", 4, ""),
            ("XYZ Scale", "scale xyz size resize stretch", 4, "XYZ SCALE"),
            ("Avatar Switch FX", "avatar switch effects disable animation", 4, "AVATAR SWITCH FX"),
            ("Holster Hider", "holster hider hide bodylog ammo pouch belt", 4, "HOLSTER HIDER"),
            ("Spawn Menu", "spawn menu item catalog browse search", 4, "SPAWN MENU"),
            ("Auto Host", "auto host server friends lobby", 5, "AUTO HOST"),
            ("Server Queue", "server queue join code connect", 5, "SERVER QUEUE"),
            ("Server Settings", "server settings nametags voicechat mortality friendly", 5, "SERVER SETTINGS"),
            ("Freeze Player", "freeze player stop movement lock", 5, "FREEZE PLAYER"),
            ("Block System", "block player item despawn prevent", 5, "BLOCK SYSTEM"),
            ("Screen Share", "screen share stream display monitor", 5, "SCREEN SHARE"),
            ("Player Info", "player info steam id list users", 5, "PLAYER INFO"),
            ("Avatar Searcher", "avatar search swap find change", 6, "AVATAR SEARCHER"),
            ("Keybinds", "keybind key bind shortcut hotkey remap", 7, "KEYBINDS"),
            ("Overlay Settings", "overlay settings color theme font opacity rainbow gradient preset", 8, "OVERLAY SETTINGS"),
        };

        private static void DrawSearchResults(float y, float w)
        {
            string q = (_globalSearchQuery ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return;

            y = Section("Search Results", y, w);
            int count = 0;
            for (int i = 0; i < SearchEntries.Length; i++)
            {
                var entry = SearchEntries[i];
                if (entry.name.ToLowerInvariant().Contains(q) || entry.keywords.Contains(q))
                {
                    string pageLabel = PageNames[entry.page];
                    string btnText = $"{entry.name}  [{pageLabel}]";
                    Color prev = GUI.color;
                    GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
                    if (GUI.Button(new Rect(PAD, y, w, ROW), btnText, _cachedButtonStyle))
                    {
                        _currentPage = entry.page;
                        _globalSearchQuery = "";
                        _scroll = 0f;
                        if (!string.IsNullOrEmpty(entry.section))
                            _expandAfterSearch = entry.section;
                    }
                    GUI.color = prev;
                    y += ROW + 2f;
                    count++;
                }
            }

            if (count == 0)
                y = Label("No results found.", y, w);
        }

        // ═══════════════════════════════════════════
        //  PAGE: Movement (Dash, Flight, Bunny Hop, etc.)
        // ═══════════════════════════════════════════
        private static void DrawMovementPage(float y, float w)
        {
            y = CollapsibleHeader("DASH", ref _movDash, y, w);
            if (_movDash)
            {

                bool v1 = DashController.IsDashEnabled;
                y = Toggle(ref v1, "Enabled", y, w);
                DashController.IsDashEnabled = v1;

                float f1 = DashController.DashForce;
                y = Slider("Dash Force", ref f1, 0f, 500f, y, w);
                DashController.DashForce = f1;

                bool v2 = DashController.IsDashInstantaneous;
                y = Toggle(ref v2, "Instantaneous", y, w);
                DashController.IsDashInstantaneous = v2;

                bool v3 = DashController.IsDashContinuous;
                y = Toggle(ref v3, "Continuous", y, w);
                DashController.IsDashContinuous = v3;

                bool v4 = DashController.IsHandOriented;
                y = Toggle(ref v4, "Hand Oriented", y, w);
                DashController.IsHandOriented = v4;

                bool v5 = DashController.UseLeftHand;
                y = Toggle(ref v5, "Use Left Hand", y, w);
                DashController.UseLeftHand = v5;

                bool v6 = DashController.LockOnEnabled;
                y = Toggle(ref v6, "Lock-On", y, w);
                DashController.LockOnEnabled = v6;

                TargetFilter dLockFilter = DashController.LockOnFilter;
                y = EnumCycle("Lock-On Filter", ref dLockFilter, y, w);
                DashController.LockOnFilter = dLockFilter;

                bool v6b = DashController.LookAtTarget;
                y = Toggle(ref v6b, "Look At Target", y, w);
                DashController.LookAtTarget = v6b;

                bool v6c = DashController.LookAtHead;
                y = Toggle(ref v6c, "Look At Head (Yaw+Pitch)", y, w);
                DashController.LookAtHead = v6c;

                bool dKvl = DashController.KillVelocityOnLand;
                y = Toggle(ref dKvl, "Kill Velocity On Land", y, w);
                DashController.KillVelocityOnLand = dKvl;

                y = Gap(y);
                y = Section("Dash Effects", y, w);

                bool v7 = DashController.EffectEnabled;
                y = Toggle(ref v7, "Custom Effect", y, w);
                DashController.EffectEnabled = v7;

                bool v8 = DashController.SmashBoneEnabled;
                y = Toggle(ref v8, "SmashBone Effect", y, w);
                DashController.SmashBoneEnabled = v8;

                float dSbCount = DashController.SmashBoneCount;
                y = Slider("SmashBone Count", ref dSbCount, 1f, 25f, y, w, "F0");
                DashController.SmashBoneCount = (int)dSbCount;

                bool dSbFlip = DashController.SmashBoneFlip;
                y = Toggle(ref dSbFlip, "SmashBone Flip", y, w);
                DashController.SmashBoneFlip = dSbFlip;

                bool v9 = DashController.CosmeticEnabled;
                y = Toggle(ref v9, "Cosmetic Effect", y, w);
                DashController.CosmeticEnabled = v9;

                float dCosCount = DashController.CosmeticCount;
                y = Slider("Cosmetic Count", ref dCosCount, 1f, 25f, y, w, "F0");
                DashController.CosmeticCount = (int)dCosCount;

                bool dCosFlip = DashController.CosmeticFlip;
                y = Toggle(ref dCosFlip, "Cosmetic Flip", y, w);
                DashController.CosmeticFlip = dCosFlip;

                y = Label("Effect Barcode: " + (string.IsNullOrEmpty(DashController.EffectBarcode) ? "(none)" : DashController.EffectBarcode), y, w);
                y = Label("Cosmetic Barcode: " + (string.IsNullOrEmpty(DashController.CosmeticBarcode) ? "(none)" : DashController.CosmeticBarcode), y, w);

                y = DrawSpawnableSearch("Search Dash Effect", ref _dashSearchQuery, _dashSearchResults,
                    (barcode, title) => { DashController.EffectBarcode = barcode; }, y, w);
                y = DrawSpawnableSearch("Search Dash Cosmetic", ref _dashCosmeticSearchQuery, _dashCosmeticSearchResults,
                    (barcode, title) => { DashController.CosmeticBarcode = barcode; }, y, w);

                float dSpawnDelay = DashController.EffectSpawnDelay;
                y = Slider("Effect Spawn Delay", ref dSpawnDelay, 0f, 2f, y, w, "F2");
                DashController.EffectSpawnDelay = dSpawnDelay;

                float dSpawnInt = DashController.EffectSpawnInterval;
                y = Slider("Effect Spawn Interval", ref dSpawnInt, 0f, 2f, y, w, "F2");
                DashController.EffectSpawnInterval = dSpawnInt;

                float dOffX = DashController.EffectOffsetX;
                y = Slider("Effect Offset X", ref dOffX, -5f, 5f, y, w, "F2");
                DashController.EffectOffsetX = dOffX;

                float dOffY = DashController.EffectOffsetY;
                y = Slider("Effect Offset Y", ref dOffY, -5f, 5f, y, w, "F2");
                DashController.EffectOffsetY = dOffY;

                float dOffZ = DashController.EffectOffsetZ;
                y = Slider("Effect Offset Z", ref dOffZ, -5f, 5f, y, w, "F2");
                DashController.EffectOffsetZ = dOffZ;

                float mc1 = DashController.EffectMatrixCount;
                y = Slider("Matrix Count", ref mc1, 1f, 25f, y, w, "F0");
                DashController.EffectMatrixCount = (int)mc1;

                float ms1 = DashController.EffectMatrixSpacing;
                y = Slider("Matrix Spacing", ref ms1, 0.1f, 10f, y, w);
                DashController.EffectMatrixSpacing = ms1;

                MatrixMode dmm = DashController.EffectMatrixMode;
                y = EnumCycle("Matrix Mode", ref dmm, y, w);
                DashController.EffectMatrixMode = dmm;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("FLIGHT", ref _movFlight, y, w);
            if (_movFlight)
            {

                bool fe = FlightController.Enabled;
                y = Toggle(ref fe, "Enabled", y, w);
                FlightController.Enabled = fe;

                float fs = FlightController.SpeedMultiplier;
                y = Slider("Speed Multiplier", ref fs, 0.5f, 20f, y, w);
                FlightController.SpeedMultiplier = fs;

                bool fa = FlightController.AccelerationEnabled;
                y = Toggle(ref fa, "Acceleration", y, w);
                FlightController.AccelerationEnabled = fa;

                float far = FlightController.AccelerationRate;
                y = Slider("Acceleration Rate", ref far, 0.1f, 10f, y, w);
                FlightController.AccelerationRate = far;

                bool fm = FlightController.MomentumEnabled;
                y = Toggle(ref fm, "Momentum", y, w);
                FlightController.MomentumEnabled = fm;

                bool fl = FlightController.LockOnEnabled;
                y = Toggle(ref fl, "Lock-On", y, w);
                FlightController.LockOnEnabled = fl;

                TargetFilter fLockFilter = FlightController.LockOnFilter;
                y = EnumCycle("Lock-On Filter", ref fLockFilter, y, w);
                FlightController.LockOnFilter = fLockFilter;

                bool flb = FlightController.LookAtTarget;
                y = Toggle(ref flb, "Look At Target", y, w);
                FlightController.LookAtTarget = flb;

                bool flc = FlightController.LookAtHead;
                y = Toggle(ref flc, "Look At Head (Yaw+Pitch)", y, w);
                FlightController.LookAtHead = flc;

                y = Gap(y);
                y = Section("Flight Effects", y, w);

                bool fxe = FlightController.EffectEnabled;
                y = Toggle(ref fxe, "Custom Effect", y, w);
                FlightController.EffectEnabled = fxe;

                bool fxs = FlightController.SmashBoneEnabled;
                y = Toggle(ref fxs, "SmashBone Effect", y, w);
                FlightController.SmashBoneEnabled = fxs;

                float fSbCount = FlightController.SmashBoneCount;
                y = Slider("SmashBone Count", ref fSbCount, 1f, 25f, y, w, "F0");
                FlightController.SmashBoneCount = (int)fSbCount;

                bool fSbFlip = FlightController.SmashBoneFlip;
                y = Toggle(ref fSbFlip, "SmashBone Flip", y, w);
                FlightController.SmashBoneFlip = fSbFlip;

                bool fxc = FlightController.CosmeticEnabled;
                y = Toggle(ref fxc, "Cosmetic Effect", y, w);
                FlightController.CosmeticEnabled = fxc;

                float fCosCount = FlightController.CosmeticCount;
                y = Slider("Cosmetic Count", ref fCosCount, 1f, 25f, y, w, "F0");
                FlightController.CosmeticCount = (int)fCosCount;

                bool fCosFlip = FlightController.CosmeticFlip;
                y = Toggle(ref fCosFlip, "Cosmetic Flip", y, w);
                FlightController.CosmeticFlip = fCosFlip;

                y = Label("Effect Barcode: " + (string.IsNullOrEmpty(FlightController.EffectBarcode) ? "(none)" : FlightController.EffectBarcode), y, w);
                y = Label("Cosmetic Barcode: " + (string.IsNullOrEmpty(FlightController.CosmeticBarcode) ? "(none)" : FlightController.CosmeticBarcode), y, w);

                y = DrawSpawnableSearch("Search Flight Effect", ref _flightSearchQuery, _flightSearchResults,
                    (barcode, title) => { FlightController.EffectBarcode = barcode; }, y, w);
                y = DrawSpawnableSearch("Search Flight Cosmetic", ref _flightCosmeticSearchQuery, _flightCosmeticSearchResults,
                    (barcode, title) => { FlightController.CosmeticBarcode = barcode; }, y, w);

                bool fHandOr = FlightController.EffectHandOriented;
                y = Toggle(ref fHandOr, "Effect Hand Oriented", y, w);
                FlightController.EffectHandOriented = fHandOr;

                bool fLeftH = FlightController.EffectUseLeftHand;
                y = Toggle(ref fLeftH, "Effect Use Left Hand", y, w);
                FlightController.EffectUseLeftHand = fLeftH;

                float fSpawnDelay = FlightController.EffectSpawnDelay;
                y = Slider("Effect Spawn Delay", ref fSpawnDelay, 0f, 2f, y, w, "F2");
                FlightController.EffectSpawnDelay = fSpawnDelay;

                float fSpawnInt = FlightController.EffectSpawnInterval;
                y = Slider("Effect Spawn Interval", ref fSpawnInt, 0f, 2f, y, w, "F2");
                FlightController.EffectSpawnInterval = fSpawnInt;

                float fOffX = FlightController.EffectOffsetX;
                y = Slider("Effect Offset X", ref fOffX, -5f, 5f, y, w, "F2");
                FlightController.EffectOffsetX = fOffX;

                float fOffY = FlightController.EffectOffsetY;
                y = Slider("Effect Offset Y", ref fOffY, -5f, 5f, y, w, "F2");
                FlightController.EffectOffsetY = fOffY;

                float fOffZ = FlightController.EffectOffsetZ;
                y = Slider("Effect Offset Z", ref fOffZ, -5f, 5f, y, w, "F2");
                FlightController.EffectOffsetZ = fOffZ;

                float fmc = FlightController.EffectMatrixCount;
                y = Slider("Matrix Count", ref fmc, 1f, 25f, y, w, "F0");
                FlightController.EffectMatrixCount = (int)fmc;

                float fms = FlightController.EffectMatrixSpacing;
                y = Slider("Matrix Spacing", ref fms, 0.1f, 10f, y, w);
                FlightController.EffectMatrixSpacing = fms;

                MatrixMode fmm = FlightController.EffectMatrixMode;
                y = EnumCycle("Matrix Mode", ref fmm, y, w);
                FlightController.EffectMatrixMode = fmm;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("BUNNY HOP", ref _movBunnyHop, y, w);
            if (_movBunnyHop)
            {
                bool bhEn = BunnyHopController.Enabled;
                y = Toggle(ref bhEn, "Enabled", y, w);
                BunnyHopController.Enabled = bhEn;

                float bhBoost = BunnyHopController.HopBoost;
                y = Slider("Hop Boost", ref bhBoost, 0f, 20f, y, w, "F1");
                BunnyHopController.HopBoost = bhBoost;

                float bhMax = BunnyHopController.MaxSpeed;
                y = Slider("Max Speed", ref bhMax, 5f, 200f, y, w, "F0");
                BunnyHopController.MaxSpeed = bhMax;

                float bhStrafe = BunnyHopController.AirStrafeForce;
                y = Slider("Air Strafe Force", ref bhStrafe, 0f, 50f, y, w, "F1");
                BunnyHopController.AirStrafeForce = bhStrafe;

                float bhJump = BunnyHopController.JumpForce;
                y = Slider("Jump Force", ref bhJump, 1f, 20f, y, w, "F1");
                BunnyHopController.JumpForce = bhJump;

                float bhStandable = BunnyHopController.StandableNormal;
                y = Slider("Standable Normal", ref bhStandable, 0f, 1f, y, w, "F2");
                BunnyHopController.StandableNormal = bhStandable;

                bool bhAuto = BunnyHopController.AutoHop;
                y = Toggle(ref bhAuto, "Auto Hop", y, w);
                BunnyHopController.AutoHop = bhAuto;

                bool bhAutoToggle = BunnyHopController.AutoJumpToggle;
                y = Toggle(ref bhAutoToggle, "Auto Jump Toggle", y, w);
                BunnyHopController.AutoJumpToggle = bhAutoToggle;

                bool bhTrimp = BunnyHopController.TrimpEnabled;
                y = Toggle(ref bhTrimp, "Trimping", y, w);
                BunnyHopController.TrimpEnabled = bhTrimp;

                float bhTrimpMul = BunnyHopController.TrimpMultiplier;
                y = Slider("Trimp Multiplier", ref bhTrimpMul, 0f, 3f, y, w, "F2");
                BunnyHopController.TrimpMultiplier = bhTrimpMul;

                bool bhFx = BunnyHopController.JumpEffectEnabled;
                y = Toggle(ref bhFx, "Jump Effect", y, w);
                BunnyHopController.JumpEffectEnabled = bhFx;

                y = Label("Effect Barcode: " + (string.IsNullOrEmpty(BunnyHopController.JumpEffectBarcode) ? "(none)" : BunnyHopController.JumpEffectBarcode), y, w);
                y = DrawSpawnableSearch("Search Jump Effect", ref _bhopEffectSearchQuery, _bhopEffectSearchResults,
                    (barcode, title) => { BunnyHopController.JumpEffectBarcode = barcode; }, y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AUTO RUN", ref _movAutoRun, y, w);
            if (_movAutoRun)
            {

                bool ar = AutoRunController.Enabled;
                y = Toggle(ref ar, "Enabled", y, w);
                AutoRunController.Enabled = ar;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SPINBOT", ref _movSpinbot, y, w);
            if (_movSpinbot)
            {
                bool sb = SpinbotController.Enabled;
                y = Toggle(ref sb, "Enabled", y, w);
                SpinbotController.Enabled = sb;

                float sbSpeed = SpinbotController.Speed;
                y = Slider("Speed (°/s)", ref sbSpeed, 10f, 7200f, y, w, "F0");
                SpinbotController.Speed = sbSpeed;

                SpinDirection sbDir = SpinbotController.Direction;
                y = EnumCycle("Direction", ref sbDir, y, w);
                SpinbotController.Direction = sbDir;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("TELEPORT", ref _movTeleport, y, w);
            if (_movTeleport)
            {
                y = Label("Saved: " + (TeleportController.HasSavedPosition ? TeleportController.SavedPositionText : "(none)"), y, w);

                float tpBtnW = (w - 10f) / 3f;
                if (GUI.Button(new Rect(PAD, y, tpBtnW, ROW), "Save Pos", _cachedButtonStyle))
                    TeleportController.SaveCurrentPosition();
                if (GUI.Button(new Rect(PAD + tpBtnW + 5f, y, tpBtnW, ROW), "Teleport", _cachedButtonStyle))
                    TeleportController.TeleportToSavedPosition();
                if (GUI.Button(new Rect(PAD + (tpBtnW + 5f) * 2f, y, tpBtnW, ROW), "Clear", _cachedButtonStyle))
                    TeleportController.ClearSavedPosition();
                y += ROW + 4f;

                y = Section("Player Teleport", y, w);

                y = Button("Refresh Players", y, 200f, 28f, () => TeleportController.RefreshPlayerList());

                var tpPlayers = TeleportController.GetCachedPlayers();
                if (tpPlayers.Count == 0)
                {
                    y = Label("No players found. Hit Refresh.", y, w);
                }
                else
                {
                    float tpNameW = w - 80f;
                    for (int pi = 0; pi < tpPlayers.Count; pi++)
                    {
                        var p = tpPlayers[pi];
                        GUI.Label(new Rect(PAD, y, tpNameW, ROW), p.DisplayName);
                        Color tpPrev = GUI.color;
                        GUI.color = _accentColor * new Color(1f, 1f, 1f, _menuOpacity);
                        if (GUI.Button(new Rect(PAD + tpNameW + 5f, y, 70f, ROW), "TP", _cachedButtonStyle))
                            TeleportController.TeleportToPlayerBySmallID(p.SmallID, p.DisplayName);
                        GUI.color = tpPrev;
                        y += ROW + 1f;
                    }
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("WAYPOINTS", ref _movWaypoints, y, w);
            if (_movWaypoints)
            {
                y = Label("Waypoints: " + WaypointController.WaypointCount, y, w);

                float tpHold = WaypointController.TeleportHoldTime;
                y = Slider("Teleport Hold Time", ref tpHold, 0.5f, 5f, y, w);
                WaypointController.TeleportHoldTime = tpHold;

                y = Button("Create Waypoint", y, w * 0.48f, 28f, () => WaypointController.CreateWaypoint());
                y = Button("Clear All Waypoints", y, w * 0.48f, 28f, () => WaypointController.ClearAllWaypoints());

                var wps = WaypointController.Waypoints;
                if (wps.Count > 0)
                {
                    y = Section("Saved Waypoints", y, w);
                    for (int i = 0; i < wps.Count; i++)
                    {
                        int idx = i;
                        var wp = wps[i];
                        string wpLabel = $"{wp.Name} ({wp.Position.x:0.0}, {wp.Position.y:0.0}, {wp.Position.z:0.0})";
                        GUI.Label(new Rect(PAD, y, w - 90f, ROW), wpLabel, _cachedLabelStyle);
                        if (GUI.Button(new Rect(PAD + w - 80f, y, 70f, ROW - 2f), "Teleport", _cachedButtonStyle))
                            WaypointController.TeleportToWaypoint(idx);
                        y += ROW;
                    }
                }

                y = Label("Default Spawn: " + (WaypointController.HasDefaultSpawn ? "Set" : "None"), y, w);
                y = Button("Set Default Spawn", y, w * 0.31f, 28f, () => WaypointController.SetDefaultSpawn());
                y = Button("Go To Spawn", y, w * 0.31f, 28f, () => WaypointController.TeleportToDefaultSpawn());
                y = Button("Clear Spawn", y, w * 0.31f, 28f, () => WaypointController.ClearDefaultSpawn());
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Player (God Mode, Ragdoll, etc.)
        // ═══════════════════════════════════════════
        private static void DrawPlayerPage(float y, float w)
        {
            y = CollapsibleHeader("GOD MODE", ref _playerGodMode, y, w);
            if (_playerGodMode)
            {
                bool gm = GodModeController.IsGodModeEnabled;
                y = Toggle(ref gm, "God Mode", y, w);
                GodModeController.IsGodModeEnabled = gm;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("RAGDOLL", ref _playerRagdoll, y, w);
            if (_playerRagdoll)
            {

                bool re = RagdollController.Enabled;
                y = Toggle(ref re, "Enabled", y, w);
                RagdollController.Enabled = re;

                RagdollMode rMode = RagdollController.Mode;
                y = EnumCycle("Mode", ref rMode, y, w);
                RagdollController.Mode = rMode;

                bool rg = RagdollController.GrabEnabled;
                y = Toggle(ref rg, "Grab Ragdoll", y, w);
                RagdollController.GrabEnabled = rg;

                bool rNeck = RagdollController.NeckGrabDisablesArms;
                y = Toggle(ref rNeck, "Neck Grab Disables Arms", y, w);
                RagdollController.NeckGrabDisablesArms = rNeck;

                bool rArm = RagdollController.ArmGrabEnabled;
                y = Toggle(ref rArm, "Arm Grab (2.5x Mass)", y, w);
                RagdollController.ArmGrabEnabled = rArm;

                bool rt = RagdollController.TantrumMode;
                y = Toggle(ref rt, "Tantrum Mode", y, w);
                RagdollController.TantrumMode = rt;

                RagdollBinding rBind = RagdollController.Binding;
                y = EnumCycle("Keybind", ref rBind, y, w);
                RagdollController.Binding = rBind;

                RagdollHand rHand = RagdollController.KeybindHand;
                y = EnumCycle("Keybind Hand", ref rHand, y, w);
                RagdollController.KeybindHand = rHand;

                y = Gap(y);
                y = Section("Ragdoll Triggers", y, w);

                bool rFall = RagdollController.FallEnabled;
                y = Toggle(ref rFall, "Fall Ragdoll", y, w);
                RagdollController.FallEnabled = rFall;

                float rFallV = RagdollController.FallVelocityThreshold;
                y = Slider("Fall Velocity", ref rFallV, 1f, 50f, y, w);
                RagdollController.FallVelocityThreshold = rFallV;

                bool rImpact = RagdollController.ImpactEnabled;
                y = Toggle(ref rImpact, "Impact Ragdoll", y, w);
                RagdollController.ImpactEnabled = rImpact;

                float rImpactT = RagdollController.ImpactThreshold;
                y = Slider("Impact Threshold", ref rImpactT, 1f, 50f, y, w);
                RagdollController.ImpactThreshold = rImpactT;

                bool rLaunch = RagdollController.LaunchEnabled;
                y = Toggle(ref rLaunch, "Launch Ragdoll", y, w);
                RagdollController.LaunchEnabled = rLaunch;

                float rLaunchT = RagdollController.LaunchThreshold;
                y = Slider("Launch Threshold", ref rLaunchT, 1f, 50f, y, w);
                RagdollController.LaunchThreshold = rLaunchT;

                bool rSlip = RagdollController.SlipEnabled;
                y = Toggle(ref rSlip, "Slip Ragdoll", y, w);
                RagdollController.SlipEnabled = rSlip;

                float rSlipFric = RagdollController.SlipFrictionThreshold;
                y = Slider("Slip Friction", ref rSlipFric, 0.01f, 1f, y, w, "F2");
                RagdollController.SlipFrictionThreshold = rSlipFric;

                float rSlipVel = RagdollController.SlipVelocityThreshold;
                y = Slider("Slip Velocity", ref rSlipVel, 0.5f, 20f, y, w);
                RagdollController.SlipVelocityThreshold = rSlipVel;

                bool rWall = RagdollController.WallPushEnabled;
                y = Toggle(ref rWall, "Wall Push Ragdoll", y, w);
                RagdollController.WallPushEnabled = rWall;

                float rWallV = RagdollController.WallPushVelocityThreshold;
                y = Slider("Wall Push Velocity", ref rWallV, 0.5f, 20f, y, w);
                RagdollController.WallPushVelocityThreshold = rWallV;

                y = Button("Unragdoll Now", y, 200f, 28f, () =>
                {
                    try
                    {
                        var physRig = BoneLib.Player.RigManager?.physicsRig;
                        if (physRig != null) { RagdollController.UnragdollPlayer(physRig); }
                    }
                    catch { }
                });
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-CONSTRAINT", ref _playerAntiConstraint, y, w);
            if (_playerAntiConstraint)
            {

                bool ac = AntiConstraintController.IsEnabled;
                y = Toggle(ref ac, "Enabled", y, w);
                AntiConstraintController.IsEnabled = ac;

                y = Button("Clear Constraints", y, 200f, 28f, () => AntiConstraintController.ClearConstraints());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-KNOCKOUT", ref _playerAntiKnockout, y, w);
            if (_playerAntiKnockout)
            {

                bool ak = AntiKnockoutController.IsEnabled;
                y = Toggle(ref ak, "Enabled", y, w);
                AntiKnockoutController.IsEnabled = ak;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("UNBREAKABLE GRIP", ref _playerUnbreakGrip, y, w);
            if (_playerUnbreakGrip)
            {

                bool ug = UnbreakableGripController.IsEnabled;
                y = Toggle(ref ug, "Enabled", y, w);
                UnbreakableGripController.IsEnabled = ug;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-GRAVITY CHANGE", ref _playerAntiGravChange, y, w);
            if (_playerAntiGravChange)
            {

                bool agc = AntiGravityChangeController.Enabled;
                y = Toggle(ref agc, "Earth Loop (Lock Gravity)", y, w);
                AntiGravityChangeController.Enabled = agc;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("FORCE GRAB", ref _playerForceGrab, y, w);
            if (_playerForceGrab)
            {

                bool fge = ForceGrabController.IsEnabled;
                y = Toggle(ref fge, "Enabled", y, w);
                ForceGrabController.IsEnabled = fge;

                bool fgi = ForceGrabController.InstantMode;
                y = Toggle(ref fgi, "Instant Mode", y, w);
                ForceGrabController.InstantMode = fgi;

                bool fgg = ForceGrabController.GlobalMode;
                y = Toggle(ref fgg, "Global Mode", y, w);
                ForceGrabController.GlobalMode = fgg;

                float fgfs = ForceGrabController.FlySpeed;
                y = Slider("Fly Speed", ref fgfs, 5f, 200f, y, w);
                ForceGrabController.FlySpeed = fgfs;

                bool fggo = ForceGrabController.GripOnly;
                y = Toggle(ref fggo, "Grip Only", y, w);
                ForceGrabController.GripOnly = fggo;

                bool fgip = ForceGrabController.IgnorePlayerRig;
                y = Toggle(ref fgip, "Ignore Player Rig", y, w);
                ForceGrabController.IgnorePlayerRig = fgip;

                bool fgfp = ForceGrabController.ForcePush;
                y = Toggle(ref fgfp, "Force Push", y, w);
                ForceGrabController.ForcePush = fgfp;

                float fgpf = ForceGrabController.PushForce;
                y = Slider("Push Force", ref fgpf, 5f, 500f, y, w);
                ForceGrabController.PushForce = fgpf;

                bool fgap = ForceGrabController.AffectPlayers;
                y = Toggle(ref fgap, "Affect Players", y, w);
                ForceGrabController.AffectPlayers = fgap;

                y = Button("Clear Selected", y, 200f, 28f, () => ForceGrabController.ClearSelected());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("DEFAULT WORLD", ref _playerDefaultWorld, y, w);
            if (_playerDefaultWorld)
            {

                bool dw = DefaultWorldController.Enabled;
                y = Toggle(ref dw, "Enabled", y, w);
                DefaultWorldController.Enabled = dw;

                y = Label($"Level: {(string.IsNullOrEmpty(DefaultWorldController.LevelName) ? "(none)" : DefaultWorldController.LevelName)}", y, w);
                y = Button("Set Current Level", y, 200f, 28f, () => DefaultWorldController.SetCurrentLevelAsDefault());
                y = Button("Clear Default", y, 200f, 28f, () => DefaultWorldController.ClearDefault());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("GHOST MODE", ref _playerGhostMode, y, w);
            if (_playerGhostMode)
            {
                bool gm2 = GhostModeController.Enabled;
                y = Toggle(ref gm2, "Enabled", y, w);
                GhostModeController.Enabled = gm2;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-RAGDOLL", ref _playerAntiRagdoll, y, w);
            if (_playerAntiRagdoll)
            {
                bool ar2 = AntiRagdollController.Enabled;
                y = Toggle(ref ar2, "Enabled", y, w);
                AntiRagdollController.Enabled = ar2;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-SLOWMO", ref _playerAntiSlowmo, y, w);
            if (_playerAntiSlowmo)
            {
                bool as2 = AntiSlowmoController.Enabled;
                y = Toggle(ref as2, "Enabled", y, w);
                AntiSlowmoController.Enabled = as2;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-TELEPORT", ref _playerAntiTeleport, y, w);
            if (_playerAntiTeleport)
            {
                bool at2 = AntiTeleportController.Enabled;
                y = Toggle(ref at2, "Enabled", y, w);
                AntiTeleportController.Enabled = at2;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("XYZ SCALE", ref _utilXyzScale, y, w);
            if (_utilXyzScale)
            {
                bool xyzEn = XYZScaleController.Enabled;
                y = Toggle(ref xyzEn, "Enabled", y, w);
                XYZScaleController.Enabled = xyzEn;

                float sx = XYZScaleController.ScaleX;
                y = Slider("Scale X", ref sx, 0.1f, 10f, y, w);
                XYZScaleController.ScaleX = sx;

                float sy = XYZScaleController.ScaleY;
                y = Slider("Scale Y", ref sy, 0.1f, 10f, y, w);
                XYZScaleController.ScaleY = sy;

                float sz = XYZScaleController.ScaleZ;
                y = Slider("Scale Z", ref sz, 0.1f, 10f, y, w);
                XYZScaleController.ScaleZ = sz;

                y = Button("Apply Scale", y, 200f, 28f, () => XYZScaleController.ApplyScale());
                y = Button("Reset (1, 1, 1)", y, 200f, 28f, () =>
                {
                    XYZScaleController.ScaleX = 1f;
                    XYZScaleController.ScaleY = 1f;
                    XYZScaleController.ScaleZ = 1f;
                    XYZScaleController.ApplyScale();
                    SettingsManager.MarkDirty();
                });
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Weapons
        // ═══════════════════════════════════════════
        private static void DrawWeaponsPage(float y, float w)
        {
            y = CollapsibleHeader("GUN MODIFIER", ref _weapChaosGun, y, w);
            if (_weapChaosGun)
            {

                bool cgDamage = ChaosGunController.InsaneDamage;
                y = Toggle(ref cgDamage, "Insane Damage!", y, w);
                ChaosGunController.InsaneDamage = cgDamage;

                bool cgRecoil = ChaosGunController.NoRecoil;
                y = Toggle(ref cgRecoil, "No Recoil!", y, w);
                ChaosGunController.NoRecoil = cgRecoil;

                bool cgFirerate = ChaosGunController.InsaneFirerate;
                y = Toggle(ref cgFirerate, "Insane Firerate", y, w);
                ChaosGunController.InsaneFirerate = cgFirerate;

                bool cgWeight = ChaosGunController.NoWeight;
                y = Toggle(ref cgWeight, "No Weight!", y, w);
                ChaosGunController.NoWeight = cgWeight;

                bool cgBounce = ChaosGunController.GunsBounce;
                y = Toggle(ref cgBounce, "Guns Bounce!", y, w);
                ChaosGunController.GunsBounce = cgBounce;

                bool cgReload = ChaosGunController.NoReload;
                y = Toggle(ref cgReload, "No Reload", y, w);
                ChaosGunController.NoReload = cgReload;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("FULL AUTO", ref _weapFullAuto, y, w);
            if (_weapFullAuto)
            {

                bool fae = FullAutoController.IsFullAutoEnabled;
                y = Toggle(ref fae, "Enabled", y, w);
                FullAutoController.IsFullAutoEnabled = fae;

                float faMult = FullAutoController.FireRateMultiplier;
                y = Slider("Fire Rate Multiplier", ref faMult, 1f, 1000f, y, w, "F1");
                FullAutoController.FireRateMultiplier = faMult;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("INFINITE AMMO", ref _weapInfAmmo, y, w);
            if (_weapInfAmmo)
            {

                bool infAmmo = InfiniteAmmoController.IsEnabled;
                y = Toggle(ref infAmmo, "Infinite Ammo", y, w);
                InfiniteAmmoController.IsEnabled = infAmmo;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("DAMAGE MULTIPLIER", ref _weapDamageMult, y, w);
            if (_weapDamageMult)
            {
                float dmGun = DamageMultiplierController.GunMultiplier;
                y = Slider("Gun Multiplier", ref dmGun, 0.1f, 100f, y, w);
                DamageMultiplierController.GunMultiplier = dmGun;

                float dmMelee = DamageMultiplierController.MeleeMultiplier;
                y = Slider("Melee Multiplier", ref dmMelee, 0.1f, 100f, y, w);
                DamageMultiplierController.MeleeMultiplier = dmMelee;

                y = Button("Apply Now", y, 200f, 28f, () => DamageMultiplierController.ApplyMultipliersNow());
                y = Button("Reset to 1x", y, 200f, 28f, () => { DamageMultiplierController.GunMultiplier = 1f; DamageMultiplierController.MeleeMultiplier = 1f; });
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Gun Visuals
        // ═══════════════════════════════════════════
        private static void DrawGunVisualsPage(float y, float w)
        {
            y = CollapsibleHeader("CUSTOM GUN COLOR", ref _gvCustomColor, y, w);
            if (_gvCustomColor)
            {
                bool ccEnabled = ChaosGunController.CustomGunColorEnabled;
                y = Toggle(ref ccEnabled, "Enabled", y, w);
                ChaosGunController.CustomGunColorEnabled = ccEnabled;

                bool ccRainbow = ChaosGunController.RainbowEnabled;
                y = Toggle(ref ccRainbow, "Rainbow", y, w);
                ChaosGunController.RainbowEnabled = ccRainbow;

                bool ccEmission = ChaosGunController.EmissionEnabled;
                y = Toggle(ref ccEmission, "Emission", y, w);
                ChaosGunController.EmissionEnabled = ccEmission;

                bool ccReflection = ChaosGunController.ReflectionEnabled;
                y = Toggle(ref ccReflection, "Reflection", y, w);
                ChaosGunController.ReflectionEnabled = ccReflection;

                bool ccTransparency = ChaosGunController.TransparencyEnabled;
                y = Toggle(ref ccTransparency, "Transparency", y, w);
                ChaosGunController.TransparencyEnabled = ccTransparency;

                float ccTransAmount = ChaosGunController.TransparencyAmount;
                y = Slider("Transparency Amount", ref ccTransAmount, 0f, 1f, y, w, "F2");
                ChaosGunController.TransparencyAmount = ccTransAmount;

                float ccEmIntensity = ChaosGunController.EmissionIntensity;
                y = Slider("Emission Intensity", ref ccEmIntensity, 0f, 20f, y, w, "F1");
                ChaosGunController.EmissionIntensity = ccEmIntensity;

                float ccRainbowSpeed = ChaosGunController.RainbowSpeed;
                y = Slider("Rainbow Speed", ref ccRainbowSpeed, 0.01f, 2f, y, w, "F2");
                ChaosGunController.RainbowSpeed = ccRainbowSpeed;

                float ccR = ChaosGunController.ColorR;
                y = Slider("Color R", ref ccR, 0f, 1f, y, w, "F2");
                ChaosGunController.ColorR = ccR;

                float ccG = ChaosGunController.ColorG;
                y = Slider("Color G", ref ccG, 0f, 1f, y, w, "F2");
                ChaosGunController.ColorG = ccG;

                float ccB = ChaosGunController.ColorB;
                y = Slider("Color B", ref ccB, 0f, 1f, y, w, "F2");
                ChaosGunController.ColorB = ccB;

                bool ccGradient = ChaosGunController.GradientEnabled;
                y = Toggle(ref ccGradient, "Gradient", y, w);
                ChaosGunController.GradientEnabled = ccGradient;

                float ccGradSpeed = ChaosGunController.GradientSpeed;
                y = Slider("Gradient Speed", ref ccGradSpeed, 0f, 5f, y, w, "F1");
                ChaosGunController.GradientSpeed = ccGradSpeed;

                float ccGradSpread = ChaosGunController.GradientSpread;
                y = Slider("Gradient Spread", ref ccGradSpread, 0.1f, 5f, y, w, "F1");
                ChaosGunController.GradientSpread = ccGradSpread;

                float cc2R = ChaosGunController.Color2R;
                y = Slider("Color 2 R", ref cc2R, 0f, 1f, y, w, "F2");
                ChaosGunController.Color2R = cc2R;

                float cc2G = ChaosGunController.Color2G;
                y = Slider("Color 2 G", ref cc2G, 0f, 1f, y, w, "F2");
                ChaosGunController.Color2G = cc2G;

                float cc2B = ChaosGunController.Color2B;
                y = Slider("Color 2 B", ref cc2B, 0f, 1f, y, w, "F2");
                ChaosGunController.Color2B = cc2B;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SHADER LIBRARY", ref _gvShaderLib, y, w);
            if (_gvShaderLib)
            {
                bool shEn = ChaosGunController.ShaderLibraryEnabled;
                y = Toggle(ref shEn, "Enabled", y, w);
                ChaosGunController.ShaderLibraryEnabled = shEn;

                bool aaShader = ChaosGunController.AutoApplyShader;
                y = Toggle(ref aaShader, "Auto-Apply on Grab", y, w);
                ChaosGunController.AutoApplyShader = aaShader;

                bool favOnly = ChaosGunController.ShowFavoritesOnly;
                y = Toggle(ref favOnly, "Favorites Only", y, w);
                if (favOnly != ChaosGunController.ShowFavoritesOnly) { ChaosGunController.ShowFavoritesOnly = favOnly; SettingsManager.MarkDirty(); }

                GUI.Label(new Rect(PAD, y, 60f, ROW), "Search:");
                string newSQ = GUI.TextField(new Rect(PAD + 60f, y, w - 70f, ROW), ChaosGunController.ShaderSearchQuery ?? "");
                if (newSQ != ChaosGunController.ShaderSearchQuery) ChaosGunController.ShaderSearchQuery = newSQ;
                y += ROW + 4f;

                y = Button("Refresh Shaders (" + ChaosGunController.ShaderCount + ")", y, 250f, 28f, () => ChaosGunController.RefreshShaderList());

                if (!ChaosGunController.IsScanningPallets)
                    y = Button("Scan All Mod Shaders", y, 250f, 28f, () => ChaosGunController.ScanPalletShaders());
                else
                    y = Label(ChaosGunController.ScanProgress, y, w);

                if (ChaosGunController.ShaderCount > 0)
                {
                    ChaosGunController.RebuildFilterIfNeeded();
                    string favStar = ChaosGunController.IsCurrentShaderFavorited() ? "\u2605" : "\u2606";
                    y = Label("Shader: " + ChaosGunController.FilteredShaderName, y, w);
                    string palletInfo = ChaosGunController.FilteredShaderPalletInfo;
                    if (!string.IsNullOrEmpty(palletInfo)) y = Label("  Source: " + palletInfo, y, w);
                    y = Label("  (" + (ChaosGunController.FilteredCursor + 1) + " / " + ChaosGunController.FilteredCount + ")", y, w);

                    float navW = (w - 10f) / 2f;
                    if (GUI.Button(new Rect(PAD, y, navW, ROW), "<< Prev", _cachedButtonStyle))
                        ChaosGunController.PrevShader();
                    if (GUI.Button(new Rect(PAD + navW + 10f, y, navW, ROW), "Next >>", _cachedButtonStyle))
                        ChaosGunController.NextShader();
                    y += ROW + 4f;

                    y = Button(favStar + " Toggle Favorite", y, 200f, 28f, () => { ChaosGunController.ToggleFavoriteCurrent(); SettingsManager.MarkDirty(); });
                    y = Button("Apply Shader", y, 200f, 28f, () => ChaosGunController.ApplyShaderToGun());
                    y = Button("Revert Shaders", y, 200f, 28f, () => ChaosGunController.RevertShaders());
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("TEXTURE EDITOR", ref _gvTexEditor, y, w);
            if (_gvTexEditor)
            {
                int texMode = ChaosGunController.TextureMode;
                string[] texModes = ChaosGunController.TextureModeNames;
                y = Label("Mode: " + texModes[texMode], y, w);
                float modeF = texMode;
                y = Slider("Mode", ref modeF, 0f, texModes.Length - 1, y, w, "F0");
                ChaosGunController.TextureMode = (int)modeF;

                if (ChaosGunController.TextureMode >= 2)
                {
                    float tr2 = ChaosGunController.TexGradR2;
                    y = Slider("Tex Color2 R", ref tr2, 0f, 1f, y, w, "F2");
                    ChaosGunController.TexGradR2 = tr2;

                    float tg2 = ChaosGunController.TexGradG2;
                    y = Slider("Tex Color2 G", ref tg2, 0f, 1f, y, w, "F2");
                    ChaosGunController.TexGradG2 = tg2;

                    float tb2 = ChaosGunController.TexGradB2;
                    y = Slider("Tex Color2 B", ref tb2, 0f, 1f, y, w, "F2");
                    ChaosGunController.TexGradB2 = tb2;
                }

                if (ChaosGunController.TextureMode == 3)
                {
                    float ns = ChaosGunController.TexNoiseScale;
                    y = Slider("Noise Scale", ref ns, 1f, 50f, y, w, "F0");
                    ChaosGunController.TexNoiseScale = ns;
                }

                float scr = ChaosGunController.TexScrollSpeed;
                y = Slider("Scroll Speed", ref scr, 0f, 5f, y, w, "F1");
                ChaosGunController.TexScrollSpeed = scr;

                y = Button("Apply Texture", y, 200f, 28f, () => ChaosGunController.ApplyTextureToGun());
                y = Button("Restore Textures", y, 200f, 28f, () => ChaosGunController.RestoreTextures());
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Combat (Explosive Punch)
        // ═══════════════════════════════════════════
        private static void DrawCombatPage(float y, float w)
        {
            y = CollapsibleHeader("EXPLOSIVE PUNCH", ref _combatExpPunch, y, w);
            if (_combatExpPunch)
            {

                y = Label("Hand Mode: " + ExplosivePunchController.PunchMode, y, w);
                float halfW = (w - 10f) / 2f;
                if (GUI.Button(new Rect(PAD, y, halfW, ROW), "BOTH", _cachedButtonStyle))
                { ExplosivePunchController.PunchMode = PunchHandMode.BOTH; SettingsManager.MarkDirty(); }
                if (GUI.Button(new Rect(PAD + halfW + 10f, y, halfW, ROW), "SEPARATE", _cachedButtonStyle))
                { ExplosivePunchController.PunchMode = PunchHandMode.SEPARATE; SettingsManager.MarkDirty(); }
                y += ROW + 4f;

                float ps = ExplosivePunchController.PunchVelocityThreshold;
                y = Slider("Punch Speed", ref ps, 1f, 15f, y, w);
                ExplosivePunchController.PunchVelocityThreshold = ps;

                float sd = ExplosivePunchController.SpawnDelay;
                y = Slider("Spawn Delay", ref sd, 0f, 0.5f, y, w, "F2");
                ExplosivePunchController.SpawnDelay = sd;

                float cd = ExplosivePunchController.PunchCooldown;
                y = Slider("Cooldown", ref cd, 0.05f, 1f, y, w, "F2");
                ExplosivePunchController.PunchCooldown = cd;

                float pc = ExplosivePunchController.PunchSpawnCount;
                y = Slider("Spawn Count", ref pc, 1f, 25f, y, w, "F0");
                ExplosivePunchController.PunchSpawnCount = (int)pc;

                float psp = ExplosivePunchController.PunchSpacing;
                y = Slider("Spawn Spacing", ref psp, 0.1f, 10f, y, w);
                ExplosivePunchController.PunchSpacing = psp;

                MatrixMode epmm = ExplosivePunchController.PunchMatrixMode;
                y = EnumCycle("Matrix Mode", ref epmm, y, w);
                ExplosivePunchController.PunchMatrixMode = epmm;

                bool rc = ExplosivePunchController.RigCheckOnly;
                y = Toggle(ref rc, "Rig Check Only", y, w);
                ExplosivePunchController.RigCheckOnly = rc;

                bool ft = ExplosivePunchController.FaceTarget;
                y = Toggle(ref ft, "Face Target", y, w);
                ExplosivePunchController.FaceTarget = ft;

                bool lg = ExplosivePunchController.IsLegacyPunchEnabled;
                y = Toggle(ref lg, "Legacy Punch", y, w);
                ExplosivePunchController.IsLegacyPunchEnabled = lg;

                y = Gap(y);
                y = Section("Both Hands Toggles", y, w);

                bool t1 = ExplosivePunchController.IsExplosivePunchEnabled;
                y = Toggle(ref t1, "Explosive Punch", y, w);
                ExplosivePunchController.IsExplosivePunchEnabled = t1;

                bool t2 = ExplosivePunchController.IsSuperExplosivePunchEnabled;
                y = Toggle(ref t2, "Super Explosive", y, w);
                ExplosivePunchController.IsSuperExplosivePunchEnabled = t2;

                bool t3 = ExplosivePunchController.IsBlackFlashEnabled;
                y = Toggle(ref t3, "BLACKFLASH", y, w);
                ExplosivePunchController.IsBlackFlashEnabled = t3;

                bool t4 = ExplosivePunchController.IsTinyExplosiveEnabled;
                y = Toggle(ref t4, "Tiny Explosive", y, w);
                ExplosivePunchController.IsTinyExplosiveEnabled = t4;

                bool t5 = ExplosivePunchController.IsBoomEnabled;
                y = Toggle(ref t5, "BOOM", y, w);
                ExplosivePunchController.IsBoomEnabled = t5;

                bool t6 = ExplosivePunchController.IsSmashBoneEnabled;
                y = Toggle(ref t6, "SmashBone", y, w);
                ExplosivePunchController.IsSmashBoneEnabled = t6;

                float epSbCount = ExplosivePunchController.SmashBoneCount;
                y = Slider("SmashBone Count", ref epSbCount, 1f, 25f, y, w, "F0");
                ExplosivePunchController.SmashBoneCount = (int)epSbCount;

                bool epSbFlip = ExplosivePunchController.SmashBoneFlip;
                y = Toggle(ref epSbFlip, "SmashBone Flip", y, w);
                ExplosivePunchController.SmashBoneFlip = epSbFlip;

                bool t7 = ExplosivePunchController.IsCustomPunchEnabled;
                y = Toggle(ref t7, "Custom Punch", y, w);
                ExplosivePunchController.IsCustomPunchEnabled = t7;

                bool t8 = ExplosivePunchController.IsCosmeticEnabled;
                y = Toggle(ref t8, "Cosmetic Effect", y, w);
                ExplosivePunchController.IsCosmeticEnabled = t8;

                float epCosCount = ExplosivePunchController.CosmeticCount;
                y = Slider("Cosmetic Count", ref epCosCount, 1f, 25f, y, w, "F0");
                ExplosivePunchController.CosmeticCount = (int)epCosCount;

                bool epCosFlip = ExplosivePunchController.CosmeticFlip;
                y = Toggle(ref epCosFlip, "Cosmetic Flip", y, w);
                ExplosivePunchController.CosmeticFlip = epCosFlip;

                y = Label("Custom Punch: " + (string.IsNullOrEmpty(ExplosivePunchController.CustomPunchBarcode) ? "(none)" : ExplosivePunchController.CustomPunchBarcode), y, w);
                y = Label("Cosmetic Barcode: " + (string.IsNullOrEmpty(ExplosivePunchController.CosmeticBarcode) ? "(none)" : ExplosivePunchController.CosmeticBarcode), y, w);
                y = DrawSpawnableSearch("Search Custom Punch", ref _explosivePunchSearchQuery, _explosivePunchSearchResults,
                    (barcode, title) => { ExplosivePunchController.CustomPunchBarcode = barcode; }, y, w);
                y = DrawSpawnableSearch("Search Punch Cosmetic", ref _epCosmeticSearchQuery, _epCosmeticSearchResults,
                    (barcode, title) => { ExplosivePunchController.CosmeticBarcode = barcode; }, y, w);

                // ── Per-Hand (SEPARATE mode) ──
                if (ExplosivePunchController.PunchMode == PunchHandMode.SEPARATE)
                {
                    y = Gap(y);
                    y = Section("Left Hand", y, w);

                    ExplosionType epLeftType = ExplosivePunchController.LeftExplosionType;
                    y = EnumCycle("Explosion Type", ref epLeftType, y, w);
                    ExplosivePunchController.LeftExplosionType = epLeftType;

                    y = Label("Left Custom: " + (string.IsNullOrEmpty(ExplosivePunchController.LeftCustomBarcode) ? "(none)" : ExplosivePunchController.LeftCustomBarcode), y, w);
                    y = DrawSpawnableSearch("Search Left Custom", ref _epLeftCustomSearchQuery, _epLeftCustomSearchResults,
                        (barcode, title) => { ExplosivePunchController.LeftCustomBarcode = barcode; }, y, w);

                    bool epLSb = ExplosivePunchController.LeftSmashBoneEnabled;
                    y = Toggle(ref epLSb, "SmashBone", y, w);
                    ExplosivePunchController.LeftSmashBoneEnabled = epLSb;

                    float epLSbC = ExplosivePunchController.LeftSmashBoneCount;
                    y = Slider("SmashBone Count", ref epLSbC, 1f, 25f, y, w, "F0");
                    ExplosivePunchController.LeftSmashBoneCount = (int)epLSbC;

                    bool epLSbF = ExplosivePunchController.LeftSmashBoneFlip;
                    y = Toggle(ref epLSbF, "SmashBone Flip", y, w);
                    ExplosivePunchController.LeftSmashBoneFlip = epLSbF;

                    bool epLCos = ExplosivePunchController.LeftCosmeticEnabled;
                    y = Toggle(ref epLCos, "Cosmetic", y, w);
                    ExplosivePunchController.LeftCosmeticEnabled = epLCos;

                    y = Label("Left Cosmetic: " + (string.IsNullOrEmpty(ExplosivePunchController.LeftCosmeticBarcode) ? "(none)" : ExplosivePunchController.LeftCosmeticBarcode), y, w);
                    y = DrawSpawnableSearch("Search Left Cosmetic", ref _epLeftCosSearchQuery, _epLeftCosSearchResults,
                        (barcode, title) => { ExplosivePunchController.LeftCosmeticBarcode = barcode; }, y, w);

                    float epLCosC = ExplosivePunchController.LeftCosmeticCount;
                    y = Slider("Cosmetic Count", ref epLCosC, 1f, 25f, y, w, "F0");
                    ExplosivePunchController.LeftCosmeticCount = (int)epLCosC;

                    bool epLCosF = ExplosivePunchController.LeftCosmeticFlip;
                    y = Toggle(ref epLCosF, "Cosmetic Flip", y, w);
                    ExplosivePunchController.LeftCosmeticFlip = epLCosF;

                    y = Gap(y);
                    y = Section("Right Hand", y, w);

                    ExplosionType epRightType = ExplosivePunchController.RightExplosionType;
                    y = EnumCycle("Explosion Type", ref epRightType, y, w);
                    ExplosivePunchController.RightExplosionType = epRightType;

                    y = Label("Right Custom: " + (string.IsNullOrEmpty(ExplosivePunchController.RightCustomBarcode) ? "(none)" : ExplosivePunchController.RightCustomBarcode), y, w);
                    y = DrawSpawnableSearch("Search Right Custom", ref _epRightCustomSearchQuery, _epRightCustomSearchResults,
                        (barcode, title) => { ExplosivePunchController.RightCustomBarcode = barcode; }, y, w);

                    bool epRSb = ExplosivePunchController.RightSmashBoneEnabled;
                    y = Toggle(ref epRSb, "SmashBone", y, w);
                    ExplosivePunchController.RightSmashBoneEnabled = epRSb;

                    float epRSbC = ExplosivePunchController.RightSmashBoneCount;
                    y = Slider("SmashBone Count", ref epRSbC, 1f, 25f, y, w, "F0");
                    ExplosivePunchController.RightSmashBoneCount = (int)epRSbC;

                    bool epRSbF = ExplosivePunchController.RightSmashBoneFlip;
                    y = Toggle(ref epRSbF, "SmashBone Flip", y, w);
                    ExplosivePunchController.RightSmashBoneFlip = epRSbF;

                    bool epRCos = ExplosivePunchController.RightCosmeticEnabled;
                    y = Toggle(ref epRCos, "Cosmetic", y, w);
                    ExplosivePunchController.RightCosmeticEnabled = epRCos;

                    y = Label("Right Cosmetic: " + (string.IsNullOrEmpty(ExplosivePunchController.RightCosmeticBarcode) ? "(none)" : ExplosivePunchController.RightCosmeticBarcode), y, w);
                    y = DrawSpawnableSearch("Search Right Cosmetic", ref _epRightCosSearchQuery, _epRightCosSearchResults,
                        (barcode, title) => { ExplosivePunchController.RightCosmeticBarcode = barcode; }, y, w);

                    float epRCosC = ExplosivePunchController.RightCosmeticCount;
                    y = Slider("Cosmetic Count", ref epRCosC, 1f, 25f, y, w, "F0");
                    ExplosivePunchController.RightCosmeticCount = (int)epRCosC;

                    bool epRCosF = ExplosivePunchController.RightCosmeticFlip;
                    y = Toggle(ref epRCosF, "Cosmetic Flip", y, w);
                    ExplosivePunchController.RightCosmeticFlip = epRCosF;
                }

            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("GROUND SLAM", ref _combatGroundSlam, y, w);
            if (_combatGroundSlam)
            {

                bool gs1 = GroundPoundController.Enabled;
                y = Toggle(ref gs1, "Enabled", y, w);
                GroundPoundController.Enabled = gs1;

                float gsVel = GroundPoundController.VelocityThreshold;
                y = Slider("Velocity Threshold", ref gsVel, 1f, 50f, y, w);
                GroundPoundController.VelocityThreshold = gsVel;

                float gsCd = GroundPoundController.Cooldown;
                y = Slider("Cooldown", ref gsCd, 0.05f, 5f, y, w, "F2");
                GroundPoundController.Cooldown = gsCd;

                float gsSd = GroundPoundController.SpawnDelay;
                y = Slider("Spawn Delay", ref gsSd, 0f, 2f, y, w, "F2");
                GroundPoundController.SpawnDelay = gsSd;

                ExplosionType gsExp = GroundPoundController.SelectedExplosion;
                y = EnumCycle("Explosion Type", ref gsExp, y, w);
                GroundPoundController.SelectedExplosion = gsExp;

                float gsMc = GroundPoundController.MatrixCount;
                y = Slider("Matrix Count", ref gsMc, 1f, 25f, y, w, "F0");
                GroundPoundController.MatrixCount = (int)gsMc;

                float gsMs = GroundPoundController.MatrixSpacing;
                y = Slider("Matrix Spacing", ref gsMs, 0.1f, 10f, y, w);
                GroundPoundController.MatrixSpacing = gsMs;

                MatrixMode gsMm = GroundPoundController.SelectedMatrixMode;
                y = EnumCycle("Matrix Mode", ref gsMm, y, w);
                GroundPoundController.SelectedMatrixMode = gsMm;

                bool gsSb = GroundPoundController.SmashBoneEnabled;
                y = Toggle(ref gsSb, "SmashBone", y, w);
                GroundPoundController.SmashBoneEnabled = gsSb;

                float gsSbCount = GroundPoundController.SmashBoneCount;
                y = Slider("SmashBone Count", ref gsSbCount, 1f, 25f, y, w, "F0");
                GroundPoundController.SmashBoneCount = (int)gsSbCount;

                bool gsSbFlip = GroundPoundController.SmashBoneFlip;
                y = Toggle(ref gsSbFlip, "SmashBone Flip", y, w);
                GroundPoundController.SmashBoneFlip = gsSbFlip;

                bool gsCos = GroundPoundController.CosmeticEnabled;
                y = Toggle(ref gsCos, "Cosmetic Effect", y, w);
                GroundPoundController.CosmeticEnabled = gsCos;

                float gsCosCount = GroundPoundController.CosmeticCount;
                y = Slider("Cosmetic Count", ref gsCosCount, 1f, 25f, y, w, "F0");
                GroundPoundController.CosmeticCount = (int)gsCosCount;

                bool gsCosFlip = GroundPoundController.CosmeticFlip;
                y = Toggle(ref gsCosFlip, "Cosmetic Flip", y, w);
                GroundPoundController.CosmeticFlip = gsCosFlip;

                y = Label("Custom Barcode: " + (string.IsNullOrEmpty(GroundPoundController.CustomBarcode) ? "(none)" : GroundPoundController.CustomBarcode), y, w);
                y = Label("Cosmetic Barcode: " + (string.IsNullOrEmpty(GroundPoundController.CosmeticBarcode) ? "(none)" : GroundPoundController.CosmeticBarcode), y, w);

                y = DrawSpawnableSearch("Search Custom Slam", ref _groundSlamSearchQuery, _groundSlamSearchResults,
                    (barcode, title) => { GroundPoundController.CustomBarcode = barcode; }, y, w);
                y = DrawSpawnableSearch("Search Slam Cosmetic", ref _gsCosmeticSearchQuery, _gsCosmeticSearchResults,
                    (barcode, title) => { GroundPoundController.CosmeticBarcode = barcode; }, y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("EXPLOSIVE IMPACT", ref _combatExpImpact, y, w);
            if (_combatExpImpact)
            {

                bool ei1 = ExplosiveImpactController.Enabled;
                y = Toggle(ref ei1, "Enabled", y, w);
                ExplosiveImpactController.Enabled = ei1;

                float eiVel = ExplosiveImpactController.VelocityThreshold;
                y = Slider("Velocity Threshold", ref eiVel, 1f, 50f, y, w);
                ExplosiveImpactController.VelocityThreshold = eiVel;

                float eiCd = ExplosiveImpactController.Cooldown;
                y = Slider("Cooldown", ref eiCd, 0.05f, 5f, y, w, "F2");
                ExplosiveImpactController.Cooldown = eiCd;

                float eiSd = ExplosiveImpactController.SpawnDelay;
                y = Slider("Spawn Delay", ref eiSd, 0f, 2f, y, w, "F2");
                ExplosiveImpactController.SpawnDelay = eiSd;

                ExplosionType eiExp = ExplosiveImpactController.SelectedExplosion;
                y = EnumCycle("Explosion Type", ref eiExp, y, w);
                ExplosiveImpactController.SelectedExplosion = eiExp;

                float eiMc = ExplosiveImpactController.MatrixCount;
                y = Slider("Matrix Count", ref eiMc, 1f, 25f, y, w, "F0");
                ExplosiveImpactController.MatrixCount = (int)eiMc;

                float eiMs = ExplosiveImpactController.MatrixSpacing;
                y = Slider("Matrix Spacing", ref eiMs, 0.1f, 10f, y, w);
                ExplosiveImpactController.MatrixSpacing = eiMs;

                MatrixMode eiMm = ExplosiveImpactController.SelectedMatrixMode;
                y = EnumCycle("Matrix Mode", ref eiMm, y, w);
                ExplosiveImpactController.SelectedMatrixMode = eiMm;

                bool eiSb = ExplosiveImpactController.SmashBoneEnabled;
                y = Toggle(ref eiSb, "SmashBone", y, w);
                ExplosiveImpactController.SmashBoneEnabled = eiSb;

                float eiSbCount = ExplosiveImpactController.SmashBoneCount;
                y = Slider("SmashBone Count", ref eiSbCount, 1f, 25f, y, w, "F0");
                ExplosiveImpactController.SmashBoneCount = (int)eiSbCount;

                bool eiSbFlip = ExplosiveImpactController.SmashBoneFlip;
                y = Toggle(ref eiSbFlip, "SmashBone Flip", y, w);
                ExplosiveImpactController.SmashBoneFlip = eiSbFlip;

                bool eiCos = ExplosiveImpactController.CosmeticEnabled;
                y = Toggle(ref eiCos, "Cosmetic Effect", y, w);
                ExplosiveImpactController.CosmeticEnabled = eiCos;

                float eiCosCount = ExplosiveImpactController.CosmeticCount;
                y = Slider("Cosmetic Count", ref eiCosCount, 1f, 25f, y, w, "F0");
                ExplosiveImpactController.CosmeticCount = (int)eiCosCount;

                bool eiCosFlip = ExplosiveImpactController.CosmeticFlip;
                y = Toggle(ref eiCosFlip, "Cosmetic Flip", y, w);
                ExplosiveImpactController.CosmeticFlip = eiCosFlip;

                y = Label("Custom Barcode: " + (string.IsNullOrEmpty(ExplosiveImpactController.CustomBarcode) ? "(none)" : ExplosiveImpactController.CustomBarcode), y, w);
                y = Label("Cosmetic Barcode: " + (string.IsNullOrEmpty(ExplosiveImpactController.CosmeticBarcode) ? "(none)" : ExplosiveImpactController.CosmeticBarcode), y, w);

                y = DrawSpawnableSearch("Search Custom Impact", ref _expImpactSearchQuery, _expImpactSearchResults,
                    (barcode, title) => { ExplosiveImpactController.CustomBarcode = barcode; }, y, w);
                y = DrawSpawnableSearch("Search Impact Cosmetic", ref _eiCosmeticSearchQuery, _eiCosmeticSearchResults,
                    (barcode, title) => { ExplosiveImpactController.CosmeticBarcode = barcode; }, y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("RANDOM EXPLODE", ref _combatRandExplode, y, w);
            if (_combatRandExplode)
            {

                bool re1 = RandomExplodeController.Enabled;
                y = Toggle(ref re1, "Enabled", y, w);
                RandomExplodeController.Enabled = re1;

                ExplosionType reExpType = RandomExplodeController.SelectedExplosion;
                y = EnumCycle("Explosion Type", ref reExpType, y, w);
                RandomExplodeController.SelectedExplosion = reExpType;

                y = Label("Custom Barcode: " + (string.IsNullOrEmpty(RandomExplodeController.CustomBarcode) ? "(none)" : RandomExplodeController.CustomBarcode), y, w);
                y = DrawSpawnableSearch("Search Custom Explode", ref _randExplodeSearchQuery, _randExplodeSearchResults,
                    (barcode, title) => { RandomExplodeController.CustomBarcode = barcode; }, y, w);

                float reInterval = RandomExplodeController.Interval;
                y = Slider("Interval (sec)", ref reInterval, 0.1f, 60f, y, w);
                RandomExplodeController.Interval = reInterval;

                float reChance = RandomExplodeController.ChanceDenominator;
                y = Slider("Chance (1 in N)", ref reChance, 1f, 1000000f, y, w, "F0");
                RandomExplodeController.ChanceDenominator = (int)reChance;

                float reLaunch = RandomExplodeController.LaunchForce;
                y = Slider("Launch Force", ref reLaunch, 0f, 1000f, y, w);
                RandomExplodeController.LaunchForce = reLaunch;

                bool reRag = RandomExplodeController.RagdollOnExplode;
                y = Toggle(ref reRag, "Ragdoll on Explode", y, w);
                RandomExplodeController.RagdollOnExplode = reRag;

                // Launch Direction
                string[] launchDirNames = { "Random", "Facing", "Opposite", "Up" };
                int launchIdx = (int)RandomExplodeController.LaunchDirection;
                y = Label($"Launch Dir: {launchDirNames[launchIdx]}", y, w);
                y = Button("< Prev Dir", y, w * 0.48f, 28f, () =>
                {
                    int d = ((int)RandomExplodeController.LaunchDirection - 1 + launchDirNames.Length) % launchDirNames.Length;
                    RandomExplodeController.LaunchDirection = (ExplodeLaunchDir)d;
                    SettingsManager.MarkDirty();
                });
                y = Button("Next Dir >", y, w * 0.48f, 28f, () =>
                {
                    int d = ((int)RandomExplodeController.LaunchDirection + 1) % launchDirNames.Length;
                    RandomExplodeController.LaunchDirection = (ExplodeLaunchDir)d;
                    SettingsManager.MarkDirty();
                });

                // Target selection
                string[] targetNames = { "Self", "Others", "All" };
                int targetIdx = (int)RandomExplodeController.Target;
                y = Label($"Target: {targetNames[targetIdx]}", y, w);
                y = Button("< Prev Target", y, w * 0.48f, 28f, () =>
                {
                    int t = ((int)RandomExplodeController.Target - 1 + targetNames.Length) % targetNames.Length;
                    RandomExplodeController.Target = (ExplodeTarget)t;
                    SettingsManager.MarkDirty();
                });
                y = Button("Next Target >", y, w * 0.48f, 28f, () =>
                {
                    int t = ((int)RandomExplodeController.Target + 1) % targetNames.Length;
                    RandomExplodeController.Target = (ExplodeTarget)t;
                    SettingsManager.MarkDirty();
                });

                bool reSc = RandomExplodeController.ControllerShortcut;
                y = Toggle(ref reSc, "Controller Shortcut (B+Y)", y, w);
                RandomExplodeController.ControllerShortcut = reSc;

                float reHold = RandomExplodeController.HoldDuration;
                y = Slider("Hold Duration (sec)", ref reHold, 0.1f, 5f, y, w);
                RandomExplodeController.HoldDuration = reHold;

                y = Button("Test Explosion Now", y, 200f, 28f, () => RandomExplodeController.TriggerExplosion());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SPAWN ON PLAYER", ref _combatSpawnOnPlayer, y, w);
            if (_combatSpawnOnPlayer)
            {

                y = Label("Item: " + PlayerSpawnController.CurrentItemName, y, w);

                float spHeight = PlayerSpawnController.HeightAbovePlayer;
                y = Slider("Height Above Player", ref spHeight, -50f, 50f, y, w);
                PlayerSpawnController.HeightAbovePlayer = spHeight;

                float spForce = PlayerSpawnController.LaunchForce;
                y = Slider("Launch Force", ref spForce, 0f, 1000f, y, w);
                PlayerSpawnController.LaunchForce = spForce;

                float spCount = PlayerSpawnController.ProjectileCount;
                y = Slider("Projectile Count", ref spCount, 1f, 25f, y, w, "F0");
                PlayerSpawnController.ProjectileCount = (int)spCount;

                float spSpacing = PlayerSpawnController.ProjectileSpacing;
                y = Slider("Projectile Spacing", ref spSpacing, 0.1f, 5f, y, w);
                PlayerSpawnController.ProjectileSpacing = spSpacing;

                float spScale = PlayerSpawnController.SpawnScale;
                y = Slider("Spawn Scale", ref spScale, 0.1f, 10f, y, w);
                PlayerSpawnController.SpawnScale = spScale;

                float spSpin = PlayerSpawnController.SpinVelocity;
                y = Slider("Spin Velocity", ref spSpin, 0f, 10000f, y, w, "F0");
                PlayerSpawnController.SpinVelocity = spSpin;

                bool spAim = PlayerSpawnController.AimRotationEnabled;
                y = Toggle(ref spAim, "Aim Rotation", y, w);
                PlayerSpawnController.AimRotationEnabled = spAim;

                bool spPre = PlayerSpawnController.PreActivateMenuTap;
                y = Toggle(ref spPre, "Pre-Activate on Menu Tap", y, w);
                PlayerSpawnController.PreActivateMenuTap = spPre;

                float spFDelay = PlayerSpawnController.SpawnForceDelay;
                y = Slider("Spawn Force Delay", ref spFDelay, 0f, 0.5f, y, w, "F3");
                PlayerSpawnController.SpawnForceDelay = spFDelay;

                float spFd = PlayerSpawnController.ForceDelay;
                y = Slider("Force Delay", ref spFd, 0f, 0.5f, y, w, "F3");
                PlayerSpawnController.ForceDelay = spFd;

                y = Gap(y);
                y = Section("Spawn on Player Homing", y, w);

                bool spH = PlayerSpawnController.HomingEnabled;
                y = Toggle(ref spH, "Homing Enabled", y, w);
                PlayerSpawnController.HomingEnabled = spH;

                TargetFilter spHf = PlayerSpawnController.HomingFilter;
                y = EnumCycle("Homing Filter", ref spHf, y, w);
                PlayerSpawnController.HomingFilter = spHf;

                float spHs = PlayerSpawnController.HomingStrength;
                y = Slider("Homing Strength", ref spHs, 1f, 50f, y, w);
                PlayerSpawnController.HomingStrength = spHs;

                float spHd = PlayerSpawnController.HomingSpeed;
                y = Slider("Homing Speed", ref spHd, 0f, 100f, y, w);
                PlayerSpawnController.HomingSpeed = spHd;

                float spHDur = PlayerSpawnController.HomingDuration;
                y = Slider("Homing Duration", ref spHDur, 0f, 30f, y, w);
                PlayerSpawnController.HomingDuration = spHDur;

                float spHStay = PlayerSpawnController.HomingStayDuration;
                y = Slider("Homing Stay Duration", ref spHStay, 0f, 30f, y, w);
                PlayerSpawnController.HomingStayDuration = spHStay;

                bool spHRot = PlayerSpawnController.HomingRotationLock;
                y = Toggle(ref spHRot, "Homing Rotation Lock", y, w);
                PlayerSpawnController.HomingRotationLock = spHRot;

                bool spHt = PlayerSpawnController.HomingTargetHead;
                y = Toggle(ref spHt, "Target Head", y, w);
                PlayerSpawnController.HomingTargetHead = spHt;

                bool spHMom = PlayerSpawnController.HomingMomentum;
                y = Toggle(ref spHMom, "Homing Momentum", y, w);
                PlayerSpawnController.HomingMomentum = spHMom;

                bool spHAccel = PlayerSpawnController.HomingAccelEnabled;
                y = Toggle(ref spHAccel, "Homing Acceleration", y, w);
                PlayerSpawnController.HomingAccelEnabled = spHAccel;

                float spHAr = PlayerSpawnController.HomingAccelRate;
                y = Slider("Homing Accel Rate", ref spHAr, 0.1f, 10f, y, w);
                PlayerSpawnController.HomingAccelRate = spHAr;

                y = DrawSpawnableSearch("Search Drop Item", ref _spawnOnPlayerSearchQuery, _spawnOnPlayerSearchResults,
                    (barcode, title) => { PlayerSpawnController.CurrentBarcode = barcode; PlayerSpawnController.CurrentItemName = title; }, y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("WAYPOINT PROJECTILE", ref _combatWaypointProj, y, w);
            if (_combatWaypointProj)
            {

                y = Label("Item: " + WaypointController.CurrentItemName, y, w);
                y = Label("Waypoints: " + WaypointController.WaypointCount + (WaypointController.HasSavedSpawnPosition ? " | Spawn Pos: Saved" : " | Spawn Pos: None"), y, w);

                bool wpSc = WaypointController.ControllerShortcut;
                y = Toggle(ref wpSc, "Controller Shortcut (B+Y)", y, w);
                WaypointController.ControllerShortcut = wpSc;

                float wpHeight = WaypointController.SpawnHeight;
                y = Slider("Spawn Height", ref wpHeight, 0f, 50f, y, w);
                WaypointController.SpawnHeight = wpHeight;

                float wpForce = WaypointController.LaunchForce;
                y = Slider("Launch Force", ref wpForce, 0f, 1000f, y, w);
                WaypointController.LaunchForce = wpForce;

                float wpCount = WaypointController.ProjectileCount;
                y = Slider("Projectile Count", ref wpCount, 1f, 25f, y, w, "F0");
                WaypointController.ProjectileCount = (int)wpCount;

                float wpSpacing = WaypointController.ProjectileSpacing;
                y = Slider("Projectile Spacing", ref wpSpacing, 0.1f, 5f, y, w);
                WaypointController.ProjectileSpacing = wpSpacing;

                float wpScale = WaypointController.SpawnScale;
                y = Slider("Spawn Scale", ref wpScale, 0.1f, 10f, y, w);
                WaypointController.SpawnScale = wpScale;

                float wpSpin = WaypointController.SpinVelocity;
                y = Slider("Spin Velocity", ref wpSpin, 0f, 10000f, y, w, "F0");
                WaypointController.SpinVelocity = wpSpin;

                bool wpAim = WaypointController.AimRotationEnabled;
                y = Toggle(ref wpAim, "Aim Rotation", y, w);
                WaypointController.AimRotationEnabled = wpAim;

                bool wpPre = WaypointController.PreActivateMenuTap;
                y = Toggle(ref wpPre, "Pre-Activate on Menu Tap", y, w);
                WaypointController.PreActivateMenuTap = wpPre;

                float wpFDelay = WaypointController.SpawnForceDelay;
                y = Slider("Spawn Force Delay", ref wpFDelay, 0f, 0.5f, y, w, "F3");
                WaypointController.SpawnForceDelay = wpFDelay;

                float wpFd = WaypointController.ForceDelay;
                y = Slider("Force Delay", ref wpFd, 0f, 0.5f, y, w, "F3");
                WaypointController.ForceDelay = wpFd;

                y = Button("Save Spawn Position", y, w * 0.48f, 28f, () => WaypointController.SaveSpawnPosition());
                y = Button("Spawn at Position", y, w * 0.48f, 28f, () => WaypointController.SpawnAtSavedPosition());

                y = Gap(y);
                y = Section("Waypoint Proj Homing", y, w);

                bool wpH = WaypointController.HomingEnabled;
                y = Toggle(ref wpH, "Homing Enabled", y, w);
                WaypointController.HomingEnabled = wpH;

                TargetFilter wpHf = WaypointController.HomingFilter;
                y = EnumCycle("Homing Filter", ref wpHf, y, w);
                WaypointController.HomingFilter = wpHf;

                float wpHs = WaypointController.HomingStrength;
                y = Slider("Homing Strength", ref wpHs, 1f, 50f, y, w);
                WaypointController.HomingStrength = wpHs;

                float wpHsp = WaypointController.HomingSpeed;
                y = Slider("Homing Speed", ref wpHsp, 0f, 100f, y, w);
                WaypointController.HomingSpeed = wpHsp;

                float wpHDur = WaypointController.HomingDuration;
                y = Slider("Homing Duration", ref wpHDur, 0f, 30f, y, w);
                WaypointController.HomingDuration = wpHDur;

                float wpHStay = WaypointController.HomingStayDuration;
                y = Slider("Homing Stay Duration", ref wpHStay, 0f, 30f, y, w);
                WaypointController.HomingStayDuration = wpHStay;

                bool wpHRot = WaypointController.HomingRotationLock;
                y = Toggle(ref wpHRot, "Homing Rotation Lock", y, w);
                WaypointController.HomingRotationLock = wpHRot;

                bool wpHth = WaypointController.HomingTargetHead;
                y = Toggle(ref wpHth, "Target Head", y, w);
                WaypointController.HomingTargetHead = wpHth;

                bool wpHMom = WaypointController.HomingMomentum;
                y = Toggle(ref wpHMom, "Homing Momentum", y, w);
                WaypointController.HomingMomentum = wpHMom;

                bool wpHAccel = WaypointController.HomingAccelEnabled;
                y = Toggle(ref wpHAccel, "Homing Acceleration", y, w);
                WaypointController.HomingAccelEnabled = wpHAccel;

                float wpHAr = WaypointController.HomingAccelRate;
                y = Slider("Homing Accel Rate", ref wpHAr, 0.1f, 10f, y, w);
                WaypointController.HomingAccelRate = wpHAr;

                y = DrawSpawnableSearch("Search Waypoint Item", ref _waypointSearchQuery, _waypointSearchResults,
                    (barcode, title) => { WaypointController.CurrentBarcode = barcode; WaypointController.CurrentItemName = title; }, y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("OBJECT LAUNCHER", ref _combatObjLauncher, y, w);
            if (_combatObjLauncher)
            {

                bool oe = ObjectLauncherController.IsLauncherEnabled;
                y = Toggle(ref oe, "Enabled", y, w);
                ObjectLauncherController.IsLauncherEnabled = oe;

                bool os = ObjectLauncherController.SafetyEnabled;
                y = Toggle(ref os, "Safety (Grip+Trigger)", y, w);
                ObjectLauncherController.SafetyEnabled = os;

                bool ol = ObjectLauncherController.UseLeftHand;
                y = Toggle(ref ol, "Left Hand", y, w);
                ObjectLauncherController.UseLeftHand = ol;

                bool oa = ObjectLauncherController.IsFullAuto;
                y = Toggle(ref oa, "Full-Auto", y, w);
                ObjectLauncherController.IsFullAuto = oa;

                float oFaDelay = ObjectLauncherController.FullAutoDelay;
                y = Slider("Full-Auto Delay", ref oFaDelay, 0.01f, 1f, y, w, "F2");
                ObjectLauncherController.FullAutoDelay = oFaDelay;

                bool ot = ObjectLauncherController.ShowTrajectory;
                y = Toggle(ref ot, "Show Trajectory", y, w);
                ObjectLauncherController.ShowTrajectory = ot;

                float of1 = ObjectLauncherController.LaunchForce;
                y = Slider("Launch Force", ref of1, 0f, 10000f, y, w, "F0");
                ObjectLauncherController.LaunchForce = of1;

                float od = ObjectLauncherController.SpawnDistance;
                y = Slider("Spawn Distance", ref od, 0.5f, 10f, y, w);
                ObjectLauncherController.SpawnDistance = od;

                float oOffX = ObjectLauncherController.SpawnOffsetX;
                y = Slider("Spawn Offset X", ref oOffX, -5f, 5f, y, w, "F2");
                ObjectLauncherController.SpawnOffsetX = oOffX;

                float oOffY = ObjectLauncherController.SpawnOffsetY;
                y = Slider("Spawn Offset Y", ref oOffY, -5f, 5f, y, w, "F2");
                ObjectLauncherController.SpawnOffsetY = oOffY;

                float oc = ObjectLauncherController.ProjectileCount;
                y = Slider("Projectile Count", ref oc, 1f, 25f, y, w, "F0");
                ObjectLauncherController.ProjectileCount = (int)oc;

                float osp = ObjectLauncherController.ProjectileSpacing;
                y = Slider("Projectile Spacing", ref osp, 0.1f, 100f, y, w);
                ObjectLauncherController.ProjectileSpacing = osp;

                float osv = ObjectLauncherController.SpinVelocity;
                y = Slider("Spin Velocity", ref osv, 0f, 10000f, y, w, "F0");
                ObjectLauncherController.SpinVelocity = osv;

                float osc = ObjectLauncherController.SpawnScale;
                y = Slider("Spawn Scale", ref osc, 0.1f, 10f, y, w);
                ObjectLauncherController.SpawnScale = osc;

                bool oar = ObjectLauncherController.AimRotationEnabled;
                y = Toggle(ref oar, "Aim Rotation (Face Launch Dir)", y, w);
                ObjectLauncherController.AimRotationEnabled = oar;

                float oRotX = ObjectLauncherController.RotationX;
                y = Slider("Rotation X", ref oRotX, -180f, 180f, y, w, "F0");
                ObjectLauncherController.RotationX = oRotX;

                float oRotY = ObjectLauncherController.RotationY;
                y = Slider("Rotation Y", ref oRotY, -180f, 180f, y, w, "F0");
                ObjectLauncherController.RotationY = oRotY;

                float oRotZ = ObjectLauncherController.RotationZ;
                y = Slider("Rotation Z", ref oRotZ, -180f, 180f, y, w, "F0");
                ObjectLauncherController.RotationZ = oRotZ;

                bool oPre = ObjectLauncherController.PreActivateMenuTap;
                y = Toggle(ref oPre, "Pre-Activate on Menu Tap", y, w);
                ObjectLauncherController.PreActivateMenuTap = oPre;

                float oForceDelay = ObjectLauncherController.ForceDelay;
                y = Slider("Force Delay", ref oForceDelay, 0f, 0.5f, y, w, "F3");
                ObjectLauncherController.ForceDelay = oForceDelay;

                y = Gap(y);
                y = Section("Homing", y, w);

                bool oh = ObjectLauncherController.HomingEnabled;
                y = Toggle(ref oh, "Homing Enabled", y, w);
                ObjectLauncherController.HomingEnabled = oh;

                TargetFilter oHf = ObjectLauncherController.HomingFilter;
                y = EnumCycle("Homing Filter", ref oHf, y, w);
                ObjectLauncherController.HomingFilter = oHf;

                float ohs = ObjectLauncherController.HomingStrength;
                y = Slider("Homing Strength", ref ohs, 0.5f, 50f, y, w);
                ObjectLauncherController.HomingStrength = ohs;

                float ohDur = ObjectLauncherController.HomingDuration;
                y = Slider("Homing Duration", ref ohDur, 0f, 30f, y, w);
                ObjectLauncherController.HomingDuration = ohDur;

                float ohSpd = ObjectLauncherController.HomingSpeed;
                y = Slider("Homing Speed", ref ohSpd, 0f, 500f, y, w);
                ObjectLauncherController.HomingSpeed = ohSpd;

                float ohStay = ObjectLauncherController.HomingStayDuration;
                y = Slider("Homing Stay Duration", ref ohStay, 0f, 30f, y, w);
                ObjectLauncherController.HomingStayDuration = ohStay;

                bool ohRot = ObjectLauncherController.HomingRotationLock;
                y = Toggle(ref ohRot, "Homing Rotation Lock", y, w);
                ObjectLauncherController.HomingRotationLock = ohRot;

                bool ohHead = ObjectLauncherController.HomingTargetHead;
                y = Toggle(ref ohHead, "Homing Target Head", y, w);
                ObjectLauncherController.HomingTargetHead = ohHead;

                bool ohMom = ObjectLauncherController.HomingMomentum;
                y = Toggle(ref ohMom, "Homing Momentum", y, w);
                ObjectLauncherController.HomingMomentum = ohMom;

                bool ohAccel = ObjectLauncherController.HomingAccelEnabled;
                y = Toggle(ref ohAccel, "Homing Acceleration", y, w);
                ObjectLauncherController.HomingAccelEnabled = ohAccel;

                float ohAr = ObjectLauncherController.HomingAccelRate;
                y = Slider("Homing Accel Rate", ref ohAr, 0.1f, 10f, y, w);
                ObjectLauncherController.HomingAccelRate = ohAr;

                y = Gap(y);
                y = Section("Cleanup", y, w);

                y = Button("Despawn Launched Objects", y, w, 30f, () => ObjectLauncherController.DespawnLaunchedObjects());

                bool oac = ObjectLauncherController.AutoCleanupEnabled;
                y = Toggle(ref oac, "Auto Cleanup", y, w);
                ObjectLauncherController.AutoCleanupEnabled = oac;

                float oci = ObjectLauncherController.AutoCleanupInterval;
                y = Slider("Cleanup Interval (s)", ref oci, 1f, 120f, y, w);
                ObjectLauncherController.AutoCleanupInterval = oci;

                float ofd = ObjectLauncherController.SpawnForceDelay;
                y = Slider("Spawn-Force Delay (s)", ref ofd, 0f, 0.5f, y, w, "F3");
                ObjectLauncherController.SpawnForceDelay = ofd;

                bool oad = ObjectLauncherController.AutoDespawnEnabled;
                y = Toggle(ref oad, "Auto Despawn", y, w);
                ObjectLauncherController.AutoDespawnEnabled = oad;

                float odd = ObjectLauncherController.AutoDespawnDelay;
                y = Slider("Despawn Delay (s)", ref odd, 1f, 120f, y, w);
                ObjectLauncherController.AutoDespawnDelay = odd;

                y = Gap(y, 10f);
                y = Label("Current: " + ObjectLauncherController.CurrentItemName, y, w);
                y = DrawSpawnableSearch("Search Spawnable", ref _launcherSearchQuery, _launcherSearchResults,
                    (barcode, title) => { ObjectLauncherController.CurrentBarcodeID = barcode; ObjectLauncherController.CurrentItemName = title; }, y, w);
            }

            y = CollapsibleHeader("RECOIL RAGDOLL", ref _combatRecoilRagdoll, y, w);
            if (_combatRecoilRagdoll)
            {
                bool rrEnabled = RecoilRagdollController.Enabled;
                y = Toggle(ref rrEnabled, "Enabled", y, w);
                RecoilRagdollController.Enabled = rrEnabled;

                float rrDelay = RecoilRagdollController.Delay;
                y = Slider("Delay (s)", ref rrDelay, 0f, 2f, y, w, "F2");
                RecoilRagdollController.Delay = rrDelay;

                float rrCooldown = RecoilRagdollController.Cooldown;
                y = Slider("Cooldown (s)", ref rrCooldown, 0.1f, 10f, y, w, "F1");
                RecoilRagdollController.Cooldown = rrCooldown;

                float rrForce = RecoilRagdollController.ForceMultiplier;
                y = Slider("Knockback Force", ref rrForce, 0f, 10f, y, w, "F1");
                RecoilRagdollController.ForceMultiplier = rrForce;

                bool rrDrop = RecoilRagdollController.DropGun;
                y = Toggle(ref rrDrop, "Drop Gun", y, w);
                RecoilRagdollController.DropGun = rrDrop;
            }

            y = CollapsibleHeader("HOMING THROW", ref _combatHomingThrow, y, w);
            if (_combatHomingThrow)
            {
                bool htEnabled = HomingThrowController.Enabled;
                y = Toggle(ref htEnabled, "Enabled", y, w);
                HomingThrowController.Enabled = htEnabled;

                TargetFilter htFilter = HomingThrowController.Filter;
                y = EnumCycle("Target Filter", ref htFilter, y, w);
                HomingThrowController.Filter = htFilter;

                float htStrength = HomingThrowController.Strength;
                y = Slider("Strength", ref htStrength, 1f, 50f, y, w, "F1");
                HomingThrowController.Strength = htStrength;

                float htSpeed = HomingThrowController.Speed;
                y = Slider("Speed (0=throw)", ref htSpeed, 0f, 500f, y, w, "F0");
                HomingThrowController.Speed = htSpeed;

                float htDuration = HomingThrowController.Duration;
                y = Slider("Duration (0=inf)", ref htDuration, 0f, 30f, y, w, "F1");
                HomingThrowController.Duration = htDuration;

                float htMinSpd = HomingThrowController.MinThrowSpeed;
                y = Slider("Min Throw Speed", ref htMinSpd, 0f, 20f, y, w, "F1");
                HomingThrowController.MinThrowSpeed = htMinSpd;

                bool htRot = HomingThrowController.RotationLock;
                y = Toggle(ref htRot, "Rotation Lock", y, w);
                HomingThrowController.RotationLock = htRot;

                bool htAccel = HomingThrowController.AccelEnabled;
                y = Toggle(ref htAccel, "Acceleration", y, w);
                HomingThrowController.AccelEnabled = htAccel;

                float htAccelRate = HomingThrowController.AccelRate;
                y = Slider("Accel Rate", ref htAccelRate, 0.1f, 10f, y, w, "F1");
                HomingThrowController.AccelRate = htAccelRate;

                bool htHead = HomingThrowController.TargetHead;
                y = Toggle(ref htHead, "Target Head", y, w);
                HomingThrowController.TargetHead = htHead;

                bool htMom = HomingThrowController.Momentum;
                y = Toggle(ref htMom, "Momentum", y, w);
                HomingThrowController.Momentum = htMom;

                float htStay = HomingThrowController.StayDuration;
                y = Slider("Stay Duration", ref htStay, 0f, 30f, y, w, "F1");
                HomingThrowController.StayDuration = htStay;

                bool htRecall = HomingThrowController.RecallEnabled;
                y = Toggle(ref htRecall, "Recall (Shield)", y, w);
                HomingThrowController.RecallEnabled = htRecall;

                float htRecSpd = HomingThrowController.RecallSpeed;
                y = Slider("Recall Speed", ref htRecSpd, 1f, 50f, y, w, "F0");
                HomingThrowController.RecallSpeed = htRecSpd;

                float htRecStr = HomingThrowController.RecallStrength;
                y = Slider("Recall Strength", ref htRecStr, 1f, 30f, y, w, "F1");
                HomingThrowController.RecallStrength = htRecStr;

                bool htFov = HomingThrowController.FovConeEnabled;
                y = Toggle(ref htFov, "FOV Cone", y, w);
                HomingThrowController.FovConeEnabled = htFov;

                float htFovAngle = HomingThrowController.FovAngle;
                y = Slider("FOV Angle", ref htFovAngle, 10f, 180f, y, w, "F0");
                HomingThrowController.FovAngle = htFovAngle;
            }

            y = CollapsibleHeader("ESP", ref _combatESP, y, w);
            if (_combatESP)
            {
                bool espOn = ESPController.Enabled;
                y = Toggle(ref espOn, "Enabled", y, w);
                ESPController.Enabled = espOn;

                ESPMode espMode = ESPController.Mode;
                y = EnumCycle("Mode", ref espMode, y, w);
                ESPController.Mode = espMode;

                ESPColorMode espColor = ESPController.ColorMode;
                y = EnumCycle("Color Mode", ref espColor, y, w);
                ESPController.ColorMode = espColor;

                float espNear = ESPController.NearColor;
                y = Slider("Near Color Dist", ref espNear, 1f, 100f, y, w, "F0");
                ESPController.NearColor = espNear;

                float espFar = ESPController.FarColor;
                y = Slider("Far Color Dist", ref espFar, 10f, 500f, y, w, "F0");
                ESPController.FarColor = espFar;

                float espTW = ESPController.TracerWidth;
                y = Slider("Tracer Width", ref espTW, 0.001f, 0.05f, y, w, "F3");
                ESPController.TracerWidth = espTW;

                float espSW = ESPController.SkeletonWidth;
                y = Slider("Skeleton Width", ref espSW, 0.001f, 0.05f, y, w, "F3");
                ESPController.SkeletonWidth = espSW;

                if (ESPController.ColorMode == ESPColorMode.CustomRGB || ESPController.ColorMode == ESPColorMode.Gradient)
                {
                    float ecR = ESPController.CustomR;
                    y = Slider("Color R", ref ecR, 0f, 1f, y, w, "F2");
                    ESPController.CustomR = ecR;
                    float ecG = ESPController.CustomG;
                    y = Slider("Color G", ref ecG, 0f, 1f, y, w, "F2");
                    ESPController.CustomG = ecG;
                    float ecB = ESPController.CustomB;
                    y = Slider("Color B", ref ecB, 0f, 1f, y, w, "F2");
                    ESPController.CustomB = ecB;
                }
                if (ESPController.ColorMode == ESPColorMode.Gradient)
                {
                    float egR2 = ESPController.GradientR2;
                    y = Slider("Gradient R2", ref egR2, 0f, 1f, y, w, "F2");
                    ESPController.GradientR2 = egR2;
                    float egG2 = ESPController.GradientG2;
                    y = Slider("Gradient G2", ref egG2, 0f, 1f, y, w, "F2");
                    ESPController.GradientG2 = egG2;
                    float egB2 = ESPController.GradientB2;
                    y = Slider("Gradient B2", ref egB2, 0f, 1f, y, w, "F2");
                    ESPController.GradientB2 = egB2;
                }
                if (ESPController.ColorMode == ESPColorMode.Rainbow)
                {
                    float espRS = ESPController.RainbowSpeed;
                    y = Slider("Rainbow Speed", ref espRS, 0.1f, 5f, y, w, "F1");
                    ESPController.RainbowSpeed = espRS;
                }
            }

            y = Gap(y, 5f);
            y = CollapsibleHeader("ITEM ESP", ref _combatItemESP, y, w);
            if (_combatItemESP)
            {
                bool ie = ESPController.ItemESPEnabled;
                y = Toggle(ref ie, "Enabled", y, w);
                ESPController.ItemESPEnabled = ie;

                ItemESPFilter ifilt = ESPController.ItemFilter;
                y = EnumCycle("Filter", ref ifilt, y, w);
                ESPController.ItemFilter = ifilt;

                float imd = ESPController.ItemMaxDistance;
                y = Slider("Max Distance", ref imd, 10f, 500f, y, w, "F0");
                ESPController.ItemMaxDistance = imd;

                float isi = ESPController.ItemScanInterval;
                y = Slider("Scan Interval", ref isi, 0.1f, 2f, y, w, "F1");
                ESPController.ItemScanInterval = isi;

                bool il = ESPController.ItemShowLabels;
                y = Toggle(ref il, "Show Labels", y, w);
                ESPController.ItemShowLabels = il;

                float ibh = ESPController.ItemBeamHeight;
                y = Slider("Beam Height", ref ibh, 5f, 200f, y, w, "F0");
                ESPController.ItemBeamHeight = ibh;

                float ibw = ESPController.ItemBeamWidth;
                y = Slider("Beam Width", ref ibw, 0.01f, 0.5f, y, w, "F2");
                ESPController.ItemBeamWidth = ibw;

                y = Label("--- Category Colors ---", y, w);

                float pcr = ESPController.ItemColorR;
                y = Slider("Prop R", ref pcr, 0f, 1f, y, w, "F2");
                ESPController.ItemColorR = pcr;
                float pcg = ESPController.ItemColorG;
                y = Slider("Prop G", ref pcg, 0f, 1f, y, w, "F2");
                ESPController.ItemColorG = pcg;
                float pcb = ESPController.ItemColorB;
                y = Slider("Prop B", ref pcb, 0f, 1f, y, w, "F2");
                ESPController.ItemColorB = pcb;

                float gcr = ESPController.ItemGunR;
                y = Slider("Gun R", ref gcr, 0f, 1f, y, w, "F2");
                ESPController.ItemGunR = gcr;
                float gcg = ESPController.ItemGunG;
                y = Slider("Gun G", ref gcg, 0f, 1f, y, w, "F2");
                ESPController.ItemGunG = gcg;
                float gcb = ESPController.ItemGunB;
                y = Slider("Gun B", ref gcb, 0f, 1f, y, w, "F2");
                ESPController.ItemGunB = gcb;

                float ncr = ESPController.ItemNpcR;
                y = Slider("NPC R", ref ncr, 0f, 1f, y, w, "F2");
                ESPController.ItemNpcR = ncr;
                float ncg = ESPController.ItemNpcG;
                y = Slider("NPC G", ref ncg, 0f, 1f, y, w, "F2");
                ESPController.ItemNpcG = ncg;
                float ncb = ESPController.ItemNpcB;
                y = Slider("NPC B", ref ncb, 0f, 1f, y, w, "F2");
                ESPController.ItemNpcB = ncb;

                float mcr = ESPController.ItemMeleeR;
                y = Slider("Melee R", ref mcr, 0f, 1f, y, w, "F2");
                ESPController.ItemMeleeR = mcr;
                float mcg = ESPController.ItemMeleeG;
                y = Slider("Melee G", ref mcg, 0f, 1f, y, w, "F2");
                ESPController.ItemMeleeG = mcg;
                float mcb = ESPController.ItemMeleeB;
                y = Slider("Melee B", ref mcb, 0f, 1f, y, w, "F2");
                ESPController.ItemMeleeB = mcb;
            }

            y = CollapsibleHeader("AIM ASSIST", ref _combatAimAssist, y, w);
            if (_combatAimAssist)
            {
                bool aaOn = AimAssistController.Enabled;
                y = Toggle(ref aaOn, "Enabled", y, w);
                AimAssistController.Enabled = aaOn;

                bool aaAim = AimAssistController.AimBotEnabled;
                y = Toggle(ref aaAim, "Aimbot", y, w);
                AimAssistController.AimBotEnabled = aaAim;

                float aaFov = AimAssistController.AimFOV;
                y = Slider("Aimbot FOV", ref aaFov, 5f, 360f, y, w, "F0");
                AimAssistController.AimFOV = aaFov;

                AimTarget aaTgt = AimAssistController.Target;
                y = EnumCycle("Target", ref aaTgt, y, w);
                AimAssistController.Target = aaTgt;

                bool aaTrig = AimAssistController.TriggerBotEnabled;
                y = Toggle(ref aaTrig, "Triggerbot", y, w);
                AimAssistController.TriggerBotEnabled = aaTrig;

                float aaTrigDelay = AimAssistController.TriggerBotDelay;
                y = Slider("Triggerbot Delay", ref aaTrigDelay, 0f, 0.5f, y, w, "F2");
                AimAssistController.TriggerBotDelay = aaTrigDelay;

                bool aaHS = AimAssistController.HeadshotsOnly;
                y = Toggle(ref aaHS, "Headshots Only", y, w);
                AimAssistController.HeadshotsOnly = aaHS;

                bool aaBD = AimAssistController.BulletDropComp;
                y = Toggle(ref aaBD, "Bullet Drop Comp", y, w);
                AimAssistController.BulletDropComp = aaBD;

                bool aaMC = AimAssistController.MovementComp;
                y = Toggle(ref aaMC, "Movement Comp", y, w);
                AimAssistController.MovementComp = aaMC;

                bool aaAC = AimAssistController.AccelerationComp;
                y = Toggle(ref aaAC, "Acceleration Comp", y, w);
                AimAssistController.AccelerationComp = aaAC;

                CompensationSmoothing aaSmooth = AimAssistController.Smoothing;
                y = EnumCycle("Smoothing", ref aaSmooth, y, w);
                AimAssistController.Smoothing = aaSmooth;
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Cosmetics
        // ═══════════════════════════════════════════
        private static void DrawCosmeticsPage(float y, float w)
        {
            y = CollapsibleHeader("WEEPING ANGEL", ref _cosWeepingAngel, y, w);
            if (_cosWeepingAngel)
            {

                bool wa1 = WeepingAngelController.Enabled;
                y = Toggle(ref wa1, "Enabled", y, w);
                WeepingAngelController.Enabled = wa1;

                bool waAll = WeepingAngelController.TargetEveryone;
                y = Toggle(ref waAll, "Target Everyone", y, w);
                WeepingAngelController.TargetEveryone = waAll;

                if (!waAll)
                    y = Label($"Target: {WeepingAngelController.TargetPlayerName}", y, w);

                float waAngle = WeepingAngelController.ViewAngle;
                y = Slider("View Angle", ref waAngle, 10f, 180f, y, w);
                WeepingAngelController.ViewAngle = waAngle;

                float waDist = WeepingAngelController.ViewDistance;
                y = Slider("View Distance", ref waDist, 5f, 500f, y, w);
                WeepingAngelController.ViewDistance = waDist;

                y = Label(WeepingAngelController.IsFrozen ? "Status: FROZEN (being watched)" : "Status: Free", y, w);
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AVATAR COPIER", ref _cosAvatarCopier, y, w);
            if (_cosAvatarCopier)
            {

                bool acpN = AvatarCopierController.CopyNickname;
                y = Toggle(ref acpN, "Copy Nickname", y, w);
                AvatarCopierController.CopyNickname = acpN;

                bool acpD = AvatarCopierController.CopyDescription;
                y = Toggle(ref acpD, "Copy Description", y, w);
                AvatarCopierController.CopyDescription = acpD;

                bool acpC = AvatarCopierController.CopyCosmetics;
                y = Toggle(ref acpC, "Copy Cosmetics", y, w);
                AvatarCopierController.CopyCosmetics = acpC;

                y = Label(AvatarCopierController.LastCopiedInfo, y, w);
                y = Button("Refresh Players", y, 200f, 28f, () => AvatarCopierController.RefreshPlayerList());

                var acPlayers = AvatarCopierController.Players;
                if (acPlayers.Count > 0)
                {
                    int acTotalPages = (acPlayers.Count + AVATAR_ITEMS_PER_PAGE - 1) / AVATAR_ITEMS_PER_PAGE;
                    _acPlayerPage = Math.Clamp(_acPlayerPage, 0, acTotalPages - 1);
                    int acStart = _acPlayerPage * AVATAR_ITEMS_PER_PAGE;
                    int acEnd = Math.Min(acStart + AVATAR_ITEMS_PER_PAGE, acPlayers.Count);

                    y = Section($"Players: {acPlayers.Count} | Page {_acPlayerPage + 1}/{acTotalPages}", y, w);

                    for (int i = acStart; i < acEnd; i++)
                    {
                        int idx = i;
                        var p = acPlayers[i];
                        GUI.Label(new Rect(PAD, y, w - 90f, ROW), $"{p.DisplayName} ({p.AvatarTitle})", _cachedLabelStyle);
                        if (GUI.Button(new Rect(PAD + w - 80f, y, 70f, ROW - 2f), "Copy", _cachedButtonStyle))
                            AvatarCopierController.SelectAndCopy(idx);
                        y += ROW;
                    }

                    float acNavW = (w - 10f) / 2f;
                    if (GUI.Button(new Rect(PAD, y, acNavW, ROW), "< Prev Page", _cachedButtonStyle))
                        _acPlayerPage = Math.Max(0, _acPlayerPage - 1);
                    if (GUI.Button(new Rect(PAD + acNavW + 10f, y, acNavW, ROW), "Next Page >", _cachedButtonStyle))
                        _acPlayerPage = Math.Min(acTotalPages - 1, _acPlayerPage + 1);
                    y += ROW + 4f;
                }
                else
                {
                    y = Label("No players. Click Refresh.", y, w);
                }

                if (AvatarCopierController.HasRevertState)
                    y = Button("Revert Avatar", y, 200f, 28f, () => AvatarCopierController.RevertAvatar());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("BODYLOG COLOR", ref _cosBodyLogColor, y, w);
            if (_cosBodyLogColor)
            {

                bool blc = BodyLogColorController.Enabled;
                y = Toggle(ref blc, "Enabled", y, w);
                BodyLogColorController.Enabled = blc;

                y = Section("BodyLog Hologram", y, w);
                float blr = BodyLogColorController.BodyLogR;
                y = Slider("R", ref blr, 0f, 255f, y, w, "F0"); BodyLogColorController.BodyLogR = blr;
                float blg = BodyLogColorController.BodyLogG;
                y = Slider("G", ref blg, 0f, 255f, y, w, "F0"); BodyLogColorController.BodyLogG = blg;
                float blb = BodyLogColorController.BodyLogB;
                y = Slider("B", ref blb, 0f, 255f, y, w, "F0"); BodyLogColorController.BodyLogB = blb;
                float bla = BodyLogColorController.BodyLogA;
                y = Slider("A", ref bla, 0f, 255f, y, w, "F0"); BodyLogColorController.BodyLogA = bla;

                y = Section("Ball / Sphere Grip", y, w);
                float bar = BodyLogColorController.BallR;
                y = Slider("R", ref bar, 0f, 255f, y, w, "F0"); BodyLogColorController.BallR = bar;
                float bag = BodyLogColorController.BallG;
                y = Slider("G", ref bag, 0f, 255f, y, w, "F0"); BodyLogColorController.BallG = bag;
                float bab = BodyLogColorController.BallB;
                y = Slider("B", ref bab, 0f, 255f, y, w, "F0"); BodyLogColorController.BallB = bab;
                float baa = BodyLogColorController.BallA;
                y = Slider("A", ref baa, 0f, 255f, y, w, "F0"); BodyLogColorController.BallA = baa;

                y = Section("Line Renderer", y, w);
                float lir = BodyLogColorController.LineR;
                y = Slider("R", ref lir, 0f, 255f, y, w, "F0"); BodyLogColorController.LineR = lir;
                float lig = BodyLogColorController.LineG;
                y = Slider("G", ref lig, 0f, 255f, y, w, "F0"); BodyLogColorController.LineG = lig;
                float lib = BodyLogColorController.LineB;
                y = Slider("B", ref lib, 0f, 255f, y, w, "F0"); BodyLogColorController.LineB = lib;
                float lia = BodyLogColorController.LineA;
                y = Slider("A", ref lia, 0f, 255f, y, w, "F0"); BodyLogColorController.LineA = lia;

                y = Section("Radial Menu", y, w);
                float rdr = BodyLogColorController.RadialR;
                y = Slider("R", ref rdr, 0f, 255f, y, w, "F0"); BodyLogColorController.RadialR = rdr;
                float rdg = BodyLogColorController.RadialG;
                y = Slider("G", ref rdg, 0f, 255f, y, w, "F0"); BodyLogColorController.RadialG = rdg;
                float rdb = BodyLogColorController.RadialB;
                y = Slider("B", ref rdb, 0f, 255f, y, w, "F0"); BodyLogColorController.RadialB = rdb;
                float rda = BodyLogColorController.RadialA;
                y = Slider("A", ref rda, 0f, 255f, y, w, "F0"); BodyLogColorController.RadialA = rda;

                y = Button("Apply All Colors", y, 200f, 28f, () => BodyLogColorController.ApplyAll());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AVATAR SWITCH FX", ref _cosAvatarFx, y, w);
            if (_cosAvatarFx)
            {
                bool avfx = DisableAvatarFXController.Enabled;
                y = Toggle(ref avfx, "Disable Switch Effects", y, w);
                DisableAvatarFXController.Enabled = avfx;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("HOLSTER HIDER", ref _cosHolsterHider, y, w);
            if (_cosHolsterHider)
            {
                bool hh = HolsterHiderController.HideHolsters;
                y = Toggle(ref hh, "Hide Holsters", y, w);
                if (hh != HolsterHiderController.HideHolsters) HolsterHiderController.HideHolsters = hh;

                bool ha = HolsterHiderController.HideAmmoPouch;
                y = Toggle(ref ha, "Hide Ammo Pouches", y, w);
                if (ha != HolsterHiderController.HideAmmoPouch) HolsterHiderController.HideAmmoPouch = ha;

                bool hbl = HolsterHiderController.HideBodyLog;
                y = Toggle(ref hbl, "Hide Body Log", y, w);
                if (hbl != HolsterHiderController.HideBodyLog) HolsterHiderController.HideBodyLog = hbl;

                y = Button("Apply Now", y, 200f, 28f, () => HolsterHiderController.Apply());
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Utility
        // ═══════════════════════════════════════════
        private static void DrawUtilityPage(float y, float w)
        {
            y = CollapsibleHeader("DESPAWN ALL", ref _utilDespawn, y, w);
            if (_utilDespawn)
            {
                y = Button("Despawn Now", y, w, 30f, () => DespawnAllController.DespawnAll());

                DespawnFilter dFilter = DespawnAllController.Filter;
                y = EnumCycle("Filter", ref dFilter, y, w);
                DespawnAllController.Filter = dFilter;

                bool dKeepHolstered = DespawnAllController.KeepHolsteredItems;
                y = Toggle(ref dKeepHolstered, "Keep Holstered Items", y, w);
                DespawnAllController.KeepHolsteredItems = dKeepHolstered;

                bool dKeepMyH = DespawnAllController.KeepOnlyMyHolsters;
                y = Toggle(ref dKeepMyH, "Keep Only My Holsters", y, w);
                DespawnAllController.KeepOnlyMyHolsters = dKeepMyH;

                bool ad = DespawnAllController.AutoDespawnEnabled;
                y = Toggle(ref ad, "Auto Despawn", y, w);
                DespawnAllController.AutoDespawnEnabled = ad;

                float di = DespawnAllController.AutoDespawnIntervalMins;
                y = Slider("Auto Interval (min)", ref di, 0.5f, 30f, y, w);
                DespawnAllController.AutoDespawnIntervalMins = di;

                bool dod = DespawnAllController.DespawnOnDisconnect;
                y = Toggle(ref dod, "Despawn on Disconnect", y, w);
                DespawnAllController.DespawnOnDisconnect = dod;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SPAWN LIMITER", ref _utilSpawnLimiter, y, w);
            if (_utilSpawnLimiter)
            {
                bool sle = SpawnLimiterController.Enabled;
                y = Toggle(ref sle, "Enabled", y, w);
                SpawnLimiterController.Enabled = sle;

                bool slh = SpawnLimiterController.HostOnly;
                y = Toggle(ref slh, "Host Only", y, w);
                SpawnLimiterController.HostOnly = slh;

                float sld = SpawnLimiterController.SpawnDelay;
                y = Slider("Spawn Delay (s)", ref sld, 0.05f, 5f, y, w);
                SpawnLimiterController.SpawnDelay = sld;

                int slm = SpawnLimiterController.MaxPerFrame;
                float slmf = slm;
                y = Slider("Max Per Frame", ref slmf, 1f, 50f, y, w);
                SpawnLimiterController.MaxPerFrame = (int)slmf;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-DESPAWN", ref _utilAntiDespawn, y, w);
            if (_utilAntiDespawn)
            {
                bool ae = AntiDespawnController.Enabled;
                y = Toggle(ref ae, "Enabled", y, w);
                AntiDespawnController.Enabled = ae;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("ANTI-GRAB", ref _utilAntiGrab, y, w);
            if (_utilAntiGrab)
            {
                bool ag = AntiGrabController.Enabled;
                y = Toggle(ref ag, "Enabled", y, w);
                AntiGrabController.Enabled = ag;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("FORCE SPAWNER", ref _utilForceSpawner, y, w);
            if (_utilForceSpawner)
            {
                bool fs1 = ForceSpawnerController.Enabled;
                y = Toggle(ref fs1, "Enabled", y, w);
                ForceSpawnerController.Enabled = fs1;

                bool fsUnredact = ForceSpawnerController.UnredactAll;
                y = Toggle(ref fsUnredact, "Unredact All", y, w);
                ForceSpawnerController.UnredactAll = fsUnredact;

                float fsDist = ForceSpawnerController.Distance;
                y = Slider("Distance", ref fsDist, 1f, 50f, y, w, "F0");
                ForceSpawnerController.Distance = (int)fsDist;

                float fsX = ForceSpawnerController.OffsetX;
                y = Slider("Offset X", ref fsX, -10f, 10f, y, w);
                ForceSpawnerController.OffsetX = fsX;

                float fsY = ForceSpawnerController.OffsetY;
                y = Slider("Offset Y", ref fsY, -10f, 10f, y, w);
                ForceSpawnerController.OffsetY = fsY;

                float fsZ = ForceSpawnerController.OffsetZ;
                y = Slider("Offset Z", ref fsZ, -10f, 10f, y, w);
                ForceSpawnerController.OffsetZ = fsZ;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("REMOVE WIND SFX", ref _utilWindSfx, y, w);
            if (_utilWindSfx)
            {
                bool rw = RemoveWindSFXController.Enabled;
                y = Toggle(ref rw, "Enabled", y, w);
                RemoveWindSFXController.Enabled = rw;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("MAP CHANGE", ref _utilMapChange, y, w);
            if (_utilMapChange)
            {
                y = Button("Reload Current Level", y, 200f, 28f, () => MapChangeController.ReloadLevel());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("NOTIFICATIONS", ref _utilNotifications, y, w);
            if (_utilNotifications)
            {
                bool notif = NotificationHelper.NotificationsEnabled;
                y = Toggle(ref notif, "Notifications Enabled", y, w);
                if (notif != NotificationHelper.NotificationsEnabled)
                    NotificationHelper.NotificationsEnabled = notif;
            }

            y = Gap(y, 10f);
            y = Button("Fix Wobbly Avatar", y, 200f, 28f, () => BlockController.FixWobblyAvatar());

            y = Gap(y, 10f);
            y = CollapsibleHeader("SPAWN MENU", ref _utilSpawnMenu, y, w);
            if (_utilSpawnMenu)
            {
                y = Label($"Items: {SpawnMenuController.TotalItemCount} | Filtered: {SpawnMenuController.FilteredItemCount}", y, w);
                y = Label($"Selected: {SpawnMenuController.GetSelectedItemName()}", y, w);

                y = Button("Refresh Item List", y, 200f, 28f, () => SpawnMenuController.RefreshItemList());

                GUI.Label(new Rect(PAD, y, 70f, ROW), "Search:");
                string smOld = SpawnMenuController.SearchQuery ?? "";
                string smNew = GUI.TextField(new Rect(PAD + 70f, y, w - 80f, ROW), smOld);
                if (smNew != smOld) SpawnMenuController.SearchQuery = smNew;
                y += ROW + 4f;

                float smDist = SpawnMenuController.SpawnDistance;
                y = Slider("Spawn Distance", ref smDist, 0.5f, 5f, y, w);
                SpawnMenuController.SpawnDistance = smDist;

                y = Button("Spawn Selected", y, 200f, 28f, () => SpawnMenuController.SpawnSelectedItem());

                y = Label(SpawnMenuController.GetPageInfo(), y, w);

                float smNavW = (w - 10f) / 2f;
                if (GUI.Button(new Rect(PAD, y, smNavW, ROW), "< Prev Page", _cachedButtonStyle))
                    SpawnMenuController.PreviousPage();
                if (GUI.Button(new Rect(PAD + smNavW + 10f, y, smNavW, ROW), "Next Page >", _cachedButtonStyle))
                    SpawnMenuController.NextPage();
                y += ROW + 4f;

                var pageItems = SpawnMenuController.GetCurrentPageItems();
                var selItem = SpawnMenuController.SelectedItem;
                for (int i = 0; i < pageItems.Count; i++)
                {
                    var item = pageItems[i];
                    bool isSel = selItem.HasValue && selItem.Value.BarcodeID == item.BarcodeID;
                    GUI.color = isSel ? Color.green : Color.white;
                    float itemBtnW = w - 80f;
                    if (GUI.Button(new Rect(PAD, y, itemBtnW, ROW), item.Title, _cachedButtonStyle))
                    {
                        SpawnMenuController.SearchQuery = item.Title;
                    }
                    GUI.color = Color.green;
                    if (GUI.Button(new Rect(PAD + itemBtnW + 5f, y, 70f, ROW), "Spawn", _cachedButtonStyle))
                    {
                        SpawnMenuController.SpawnItem(item.BarcodeID, item.Title);
                    }
                    GUI.color = Color.white;
                    y += ROW + 1f;
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AI NPC CONTROLS", ref _utilAINpc, y, w);
            if (_utilAINpc)
            {
                y = Button("Apply State to Held NPC", y, 250f, 28f, () => AINpcController.ApplyStateToHeld());
                y = Button("Apply State to ALL NPCs", y, 250f, 28f, () => AINpcController.ApplyStateToAll());
                y = Button("Apply HP to Held NPC", y, 250f, 28f, () => AINpcController.ApplyHpToHeld());
                y = Button("Apply Mass to Held NPC", y, 250f, 28f, () => AINpcController.ApplyMassToHeld());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AVATAR LOGGER", ref _utilAvatarLogger, y, w);
            if (_utilAvatarLogger)
            {
                bool avl = AvatarLoggerController.Enabled;
                y = Toggle(ref avl, "Enabled", y, w);
                AvatarLoggerController.Enabled = avl;

                bool avlN = AvatarLoggerController.ShowNotifications;
                y = Toggle(ref avlN, "Show Notifications", y, w);
                AvatarLoggerController.ShowNotifications = avlN;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("PLAYER ACTION LOGGER", ref _utilPlayerActionLog, y, w);
            if (_utilPlayerActionLog)
            {
                bool pal = PlayerActionLoggerController.Enabled;
                y = Toggle(ref pal, "Enabled", y, w);
                PlayerActionLoggerController.Enabled = pal;

                bool palJ = PlayerActionLoggerController.LogJoins;
                y = Toggle(ref palJ, "Log Joins", y, w);
                PlayerActionLoggerController.LogJoins = palJ;

                bool palL = PlayerActionLoggerController.LogLeaves;
                y = Toggle(ref palL, "Log Leaves", y, w);
                PlayerActionLoggerController.LogLeaves = palL;

                bool palD = PlayerActionLoggerController.LogDeaths;
                y = Toggle(ref palD, "Log Deaths", y, w);
                PlayerActionLoggerController.LogDeaths = palD;

                bool palN = PlayerActionLoggerController.ShowNotifications;
                y = Toggle(ref palN, "Show Notifications", y, w);
                PlayerActionLoggerController.ShowNotifications = palN;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SPAWN LOGGER", ref _utilSpawnLogger, y, w);
            if (_utilSpawnLogger)
            {
                bool sl = SpawnLoggerController.Enabled;
                y = Toggle(ref sl, "Enabled", y, w);
                SpawnLoggerController.Enabled = sl;

                bool slN = SpawnLoggerController.ShowNotifications;
                y = Toggle(ref slN, "Show Notifications", y, w);
                SpawnLoggerController.ShowNotifications = slN;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("LOBBY BROWSER", ref _utilLobbyBrowser, y, w);
            if (_utilLobbyBrowser)
            {
                y = Button("Refresh Lobbies", y, 250f, 28f, () => LobbyBrowserController.RefreshLobbies());
                y = Label(LobbyBrowserController.StatusText, y, w);
                var lobbies = LobbyBrowserController.CachedLobbies;
                if (lobbies.Count > 0)
                {
                    foreach (var lobby in lobbies)
                    {
                        string code = lobby.LobbyCode;
                        string info = $"[{lobby.PlayerCount}/{lobby.MaxPlayers}] {lobby.HostName} — {lobby.LevelTitle}";
                        y = Button(info, y, w, 28f, () => LobbyBrowserController.JoinLobby(code));
                    }
                }
            }

            // Spoofing integration (only if StandaloneSpoofing.dll is loaded)
            if (!_spoofingModChecked)
            {
                _spoofingModChecked = true;
                try
                {
                    _spoofingModType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                        .FirstOrDefault(t => t.FullName == "StandaloneSpoofing.SpoofingMod");
                    if (_spoofingModType != null)
                    {
                        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                        _spoofUserEnabled = _spoofingModType.GetProperty("UsernameSpoofEnabled", flags);
                        _spoofNickEnabled = _spoofingModType.GetProperty("NicknameSpoofEnabled", flags);
                        _spoofDescEnabled = _spoofingModType.GetProperty("DescriptionSpoofEnabled", flags);
                        _spoofFakeUsername = _spoofingModType.GetProperty("FakeUsername", flags);
                        _spoofFakeNickname = _spoofingModType.GetProperty("FakeNickname", flags);
                        _spoofFakeDescription = _spoofingModType.GetProperty("FakeDescription", flags);
                    }
                }
                catch { }
            }

            if (_spoofingModType != null)
            {
                y = Gap(y, 10f);
                y = CollapsibleHeader("SPOOFING", ref _utilSpoofing, y, w);
                if (_utilSpoofing)
                {
                    try
                    {
                        // Username spoof
                        bool userEn = (bool)(_spoofUserEnabled?.GetValue(null) ?? false);
                        y = Toggle(ref userEn, "Username Spoof", y, w);
                        _spoofUserEnabled?.SetValue(null, userEn);
                        GUI.Label(new Rect(PAD, y, 80f, ROW), "Username:");
                        _spoofUsernameInput = GUI.TextField(new Rect(PAD + 85f, y, w - 95f, ROW), _spoofUsernameInput ?? "");
                        y += ROW + 2f;
                        if (_spoofFakeUsername != null)
                        {
                            string cur = _spoofFakeUsername.GetValue(null) as string ?? "";
                            if (cur != _spoofUsernameInput) _spoofFakeUsername.SetValue(null, _spoofUsernameInput);
                        }

                        // Nickname spoof
                        bool nickEn = (bool)(_spoofNickEnabled?.GetValue(null) ?? false);
                        y = Toggle(ref nickEn, "Nickname Spoof", y, w);
                        _spoofNickEnabled?.SetValue(null, nickEn);
                        GUI.Label(new Rect(PAD, y, 80f, ROW), "Nickname:");
                        _spoofNicknameInput = GUI.TextField(new Rect(PAD + 85f, y, w - 95f, ROW), _spoofNicknameInput ?? "");
                        y += ROW + 2f;
                        if (_spoofFakeNickname != null)
                        {
                            string cur = _spoofFakeNickname.GetValue(null) as string ?? "";
                            if (cur != _spoofNicknameInput) _spoofFakeNickname.SetValue(null, _spoofNicknameInput);
                        }

                        // Description spoof
                        bool descEn = (bool)(_spoofDescEnabled?.GetValue(null) ?? false);
                        y = Toggle(ref descEn, "Description Spoof", y, w);
                        _spoofDescEnabled?.SetValue(null, descEn);
                        GUI.Label(new Rect(PAD, y, 80f, ROW), "Description:");
                        _spoofDescriptionInput = GUI.TextField(new Rect(PAD + 85f, y, w - 95f, ROW), _spoofDescriptionInput ?? "");
                        y += ROW + 2f;
                        if (_spoofFakeDescription != null)
                        {
                            string cur = _spoofFakeDescription.GetValue(null) as string ?? "";
                            if (cur != _spoofDescriptionInput) _spoofFakeDescription.SetValue(null, _spoofDescriptionInput);
                        }

                        y = Button("Disable ALL Spoofs", y, 200f, 28f, () =>
                        {
                            _spoofUserEnabled?.SetValue(null, false);
                            _spoofNickEnabled?.SetValue(null, false);
                            _spoofDescEnabled?.SetValue(null, false);
                        });
                    }
                    catch { }
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("AUTO-UPDATER", ref _utilAutoUpdater, y, w);
            if (_utilAutoUpdater)
            {
                bool auCheck = AutoUpdaterController.AutoCheckEnabled;
                y = Toggle(ref auCheck, "Auto Check", y, w);
                AutoUpdaterController.AutoCheckEnabled = auCheck;

                bool auInstall = AutoUpdaterController.AutoInstallEnabled;
                y = Toggle(ref auInstall, "Auto Install", y, w);
                AutoUpdaterController.AutoInstallEnabled = auInstall;

                bool auBackup = AutoUpdaterController.BackupOldDlls;
                y = Toggle(ref auBackup, "Backup Old DLLs", y, w);
                AutoUpdaterController.BackupOldDlls = auBackup;

                bool auNotify = AutoUpdaterController.NotifyOnUpdate;
                y = Toggle(ref auNotify, "Notify on Update", y, w);
                AutoUpdaterController.NotifyOnUpdate = auNotify;

                float auInterval = AutoUpdaterController.CheckIntervalHours;
                y = Slider("Check Interval (hrs)", ref auInterval, 0.5f, 168f, y, w, "F1");
                AutoUpdaterController.CheckIntervalHours = auInterval;

                y = Label("Status: " + AutoUpdaterController.StatusMessage, y, w);
                y = Label("DLLs: " + AutoUpdaterController.InstalledDllCount, y, w);
                y = Button("Scan DLLs", y, w, 28f, () => AutoUpdaterController.RefreshDllList());
                y = Button("Open Mods Folder", y, w, 28f, () => AutoUpdaterController.OpenModsFolder());
                y = Button("Open Backups Folder", y, w, 28f, () => AutoUpdaterController.OpenBackupsFolder());

                var dlls = AutoUpdaterController.GetInstalledDlls();
                for (int i = 0; i < dlls.Count && i < 20; i++)
                {
                    var dll = dlls[i];
                    y = Label($"  {dll.FileName} ({dll.SizeText})", y, w);
                }
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Server
        // ═══════════════════════════════════════════
        private static void DrawServerPage(float y, float w)
        {
            y = CollapsibleHeader("AUTO HOST", ref _netAutoHost, y, w);
            if (_netAutoHost)
            {

                bool ah = AutoHostController.Enabled;
                y = Toggle(ref ah, "Friends-Only on Launch", y, w);
                if (ah != AutoHostController.Enabled)
                {
                    AutoHostController.Enabled = ah;
                    SettingsManager.MarkDirty();
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SERVER QUEUE", ref _netServerQueue, y, w);
            if (_netServerQueue)
            {

                bool sq = ServerQueueController.Enabled;
                y = Toggle(ref sq, "Enabled", y, w);
                ServerQueueController.Enabled = sq;

                float sqPoll = ServerQueueController.PollInterval;
                y = Slider("Poll Interval (s)", ref sqPoll, 5f, 60f, y, w);
                ServerQueueController.PollInterval = sqPoll;

                y = Label(ServerQueueController.StatusText, y, w);

                GUI.Label(new Rect(PAD, y, 90f, ROW), "Server Code:");
                ServerQueueController.LastServerCode = GUI.TextField(new Rect(PAD + 95f, y, w - 105f, ROW), ServerQueueController.LastServerCode ?? "");
                y += ROW + 4f;

                if (!ServerQueueController.IsInQueue)
                    y = Button("Start Queue", y, 200f, 28f, () => ServerQueueController.StartQueueForCode(ServerQueueController.LastServerCode));
                else
                    y = Button("Stop Queue", y, 200f, 28f, () => ServerQueueController.StopQueue());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SERVER SETTINGS", ref _netServerSettings, y, w);
            if (_netServerSettings)
            {

                float ssHalf = (w - 10f) / 2f;
                if (GUI.Button(new Rect(PAD, y, ssHalf, ROW), "Toggle NameTags", _cachedButtonStyle))
                    ServerSettingsController.ToggleNameTags();
                if (GUI.Button(new Rect(PAD + ssHalf + 10f, y, ssHalf, ROW), "Toggle VoiceChat", _cachedButtonStyle))
                    ServerSettingsController.ToggleVoiceChat();
                y += ROW + 4f;
                if (GUI.Button(new Rect(PAD, y, ssHalf, ROW), "Toggle Mortality", _cachedButtonStyle))
                    ServerSettingsController.ToggleMortality();
                if (GUI.Button(new Rect(PAD + ssHalf + 10f, y, ssHalf, ROW), "Toggle FriendlyFire", _cachedButtonStyle))
                    ServerSettingsController.ToggleFriendlyFire();
                y += ROW + 4f;
                if (GUI.Button(new Rect(PAD, y, ssHalf, ROW), "Toggle Knockout", _cachedButtonStyle))
                    ServerSettingsController.ToggleKnockout();
                if (GUI.Button(new Rect(PAD + ssHalf + 10f, y, ssHalf, ROW), "Toggle Constraining", _cachedButtonStyle))
                    ServerSettingsController.TogglePlayerConstraining();
                y += ROW + 4f;
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("FREEZE PLAYER", ref _netFreezePlayer, y, w);
            if (_netFreezePlayer)
            {

                y = Button("Refresh Players", y, w * 0.48f, 28f, () => FreezePlayerController.RefreshPlayers());
                y = Button("Unfreeze All", y, w * 0.48f, 28f, () => FreezePlayerController.UnfreezeAll());

                var fpPlayers = FreezePlayerController.CachedPlayers;
                if (fpPlayers.Count > 0)
                {
                    int fpTotalPages = (fpPlayers.Count + AVATAR_ITEMS_PER_PAGE - 1) / AVATAR_ITEMS_PER_PAGE;
                    _fpPlayerPage = Math.Clamp(_fpPlayerPage, 0, fpTotalPages - 1);
                    int fpStart = _fpPlayerPage * AVATAR_ITEMS_PER_PAGE;
                    int fpEnd = Math.Min(fpStart + AVATAR_ITEMS_PER_PAGE, fpPlayers.Count);

                    y = Section($"Players: {fpPlayers.Count} | Page {_fpPlayerPage + 1}/{fpTotalPages}", y, w);

                    for (int i = fpStart; i < fpEnd; i++)
                    {
                        var p = fpPlayers[i];
                        bool frozen = FreezePlayerController.IsFrozen(p.SmallID);
                        var capturedId = p.SmallID;
                        var capturedName = p.DisplayName;
                        var capturedRig = p.Rig;
                        string label = frozen ? $"[FROZEN] {p.DisplayName}" : p.DisplayName;
                        GUI.Label(new Rect(PAD, y, w - 100f, ROW), label, _cachedLabelStyle);
                        string btnText = frozen ? "Unfreeze" : "Freeze";
                        if (GUI.Button(new Rect(PAD + w - 90f, y, 80f, ROW - 2f), btnText, _cachedButtonStyle))
                            FreezePlayerController.ToggleFreeze(capturedId, capturedName, capturedRig);
                        y += ROW;
                    }

                    float fpNavW = (w - 10f) / 2f;
                    if (GUI.Button(new Rect(PAD, y, fpNavW, ROW), "< Prev Page", _cachedButtonStyle))
                        _fpPlayerPage = Math.Max(0, _fpPlayerPage - 1);
                    if (GUI.Button(new Rect(PAD + fpNavW + 10f, y, fpNavW, ROW), "Next Page >", _cachedButtonStyle))
                        _fpPlayerPage = Math.Min(fpTotalPages - 1, _fpPlayerPage + 1);
                    y += ROW + 4f;
                }
                else
                {
                    y = Label("No players. Click Refresh.", y, w);
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("BLOCK SYSTEM", ref _netBlockSystem, y, w);
            if (_netBlockSystem)
            {

                y = Button("Despawn Held Item", y, 200f, 28f, () => BlockController.DespawnHeldItem());

                // ── Player Block ──
                y = Gap(y, 10f);
                y = Section("Player Block", y, w);

                bool bpb = BlockController.PlayerBlockEnabled;
                y = Toggle(ref bpb, "Player Block Enabled", y, w);
                BlockController.PlayerBlockEnabled = bpb;

                // Player search
                GUI.Label(new Rect(PAD, y, 70f, ROW), "Search:");
                string pbq = GUI.TextField(new Rect(PAD + 70f, y, w - 200f, ROW), _playerBlockSearchQuery ?? "");
                if (pbq != _playerBlockSearchQuery) _playerBlockSearchQuery = pbq;
                if (GUI.Button(new Rect(PAD + w - 120f, y, 55f, ROW), "Find", _cachedButtonStyle))
                {
                    TeleportController.RefreshPlayerList();
                    _playerBlockSearchResults.Clear();
                    string searchLower = (_playerBlockSearchQuery ?? "").ToLower();
                    foreach (var p in TeleportController.GetCachedPlayers())
                    {
                        if (string.IsNullOrEmpty(searchLower) || p.DisplayName.ToLower().Contains(searchLower))
                            _playerBlockSearchResults.Add(p);
                    }
                }
                if (GUI.Button(new Rect(PAD + w - 60f, y, 55f, ROW), "All", _cachedButtonStyle))
                {
                    TeleportController.RefreshPlayerList();
                    _playerBlockSearchResults.Clear();
                    _playerBlockSearchResults.AddRange(TeleportController.GetCachedPlayers());
                }
                y += ROW + 2f;

                // Player search results
                for (int i = 0; i < _playerBlockSearchResults.Count; i++)
                {
                    var p = _playerBlockSearchResults[i];
                    bool alreadyBlocked = false;
                    foreach (var bp in BlockController.BlockedPlayers)
                        if (bp.SmallID == p.SmallID) { alreadyBlocked = true; break; }

                    GUI.color = alreadyBlocked ? Color.gray : Color.green;
                    string btnLabel = alreadyBlocked ? $"{p.DisplayName} [Blocked]" : $"+ Block {p.DisplayName}";
                    if (GUI.Button(new Rect(PAD, y, w, ROW), btnLabel, _cachedButtonStyle))
                    {
                        if (!alreadyBlocked)
                            BlockController.AddBlockedPlayer(p.SmallID, p.DisplayName);
                    }
                    GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
                    y += ROW + 1f;
                }

                // Blocked players list
                if (BlockController.BlockedPlayers.Count > 0)
                {
                    y += 4f;
                    y = Label("Blocked Players:", y, w);
                    for (int i = 0; i < BlockController.BlockedPlayers.Count; i++)
                    {
                        var bp = BlockController.BlockedPlayers[i];
                        GUI.color = Color.red;
                        if (GUI.Button(new Rect(PAD, y, w, ROW), $"✕ Unblock {bp.DisplayName} (ID:{bp.SmallID})", _cachedButtonStyle))
                            BlockController.RemoveBlockedPlayer(bp.SmallID);
                        GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
                        y += ROW + 1f;
                    }
                }

                // ── Item Block (Server) ──
                y = Gap(y, 10f);
                y = Section("Item Block (Server)", y, w);

                bool bib = BlockController.ItemBlockEnabled;
                y = Toggle(ref bib, "Item Block Enabled", y, w);
                BlockController.ItemBlockEnabled = bib;

                y = DrawSpawnableSearch("Search Item to Block", ref _itemBlockSearchQuery, _itemBlockSearchResults,
                    (barcode, title) => { BlockController.AddBlockedItem(barcode, title); }, y, w);

                // Blocked items list
                if (BlockController.BlockedItems.Count > 0)
                {
                    y = Label("Blocked Items:", y, w);
                    for (int i = 0; i < BlockController.BlockedItems.Count; i++)
                    {
                        var bi = BlockController.BlockedItems[i];
                        GUI.color = Color.red;
                        if (GUI.Button(new Rect(PAD, y, w, ROW), $"✕ Unblock {bi.DisplayName}", _cachedButtonStyle))
                            BlockController.RemoveBlockedItem(bi.Barcode);
                        GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
                        y += ROW + 1f;
                    }
                }

                // ── Item Block (Local) ──
                y = Gap(y, 10f);
                y = Section("Item Block (Local)", y, w);

                bool blb = BlockController.LocalBlockEnabled;
                y = Toggle(ref blb, "Local Block Enabled", y, w);
                BlockController.LocalBlockEnabled = blb;

                y = DrawSpawnableSearch("Search Item to Local Block", ref _localBlockSearchQuery, _localBlockSearchResults,
                    (barcode, title) => { BlockController.AddLocalBlockedItem(barcode, title); }, y, w);

                // Local blocked items list
                if (BlockController.LocalBlockedItems.Count > 0)
                {
                    y = Label("Local Blocked Items:", y, w);
                    for (int i = 0; i < BlockController.LocalBlockedItems.Count; i++)
                    {
                        var bi = BlockController.LocalBlockedItems[i];
                        GUI.color = Color.magenta;
                        if (GUI.Button(new Rect(PAD, y, w, ROW), $"✕ Unblock {bi.DisplayName}", _cachedButtonStyle))
                            BlockController.RemoveLocalBlockedItem(bi.Barcode);
                        GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
                        y += ROW + 1f;
                    }
                }
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("SCREEN SHARE", ref _netScreenShare, y, w);
            if (_netScreenShare)
            {

                bool sse = ScreenShareController.Enabled;
                y = Toggle(ref sse, "Enabled", y, w);
                if (sse != ScreenShareController.Enabled) ScreenShareController.SetEnabled(sse);

                bool ssp = ScreenShareController.PreviewVisible;
                y = Toggle(ref ssp, "Preview Visible", y, w);
                if (ssp != ScreenShareController.PreviewVisible) ScreenShareController.SetPreviewVisible(ssp);

                float ssScale = ScreenShareController.Scale;
                y = Slider("Scale %", ref ssScale, 10f, 100f, y, w, "F0");
                if ((int)ssScale != ScreenShareController.Scale) ScreenShareController.SetScale((int)ssScale);

                float ssFps = ScreenShareController.TargetFps;
                y = Slider("Target FPS", ref ssFps, 1f, 60f, y, w, "F0");
                if ((int)ssFps != ScreenShareController.TargetFps) ScreenShareController.SetFps((int)ssFps);

                y = Label("Source: " + ScreenShareController.SourceDisplayName, y, w);
                if (!string.IsNullOrEmpty(ScreenShareController.StreamUrl))
                    y = Label("Stream: " + ScreenShareController.StreamUrl, y, w);

                y = Button("Reposition", y, 200f, 28f, () => ScreenShareController.Reposition());
            }

            y = Gap(y, 10f);
            y = CollapsibleHeader("PLAYER INFO", ref _netPlayerInfo, y, w);
            if (_netPlayerInfo)
            {

                y = Button("Refresh Player List", y, 200f, 28f, () => PlayerInfoController.ForceRefresh());

                var players = PlayerInfoController.Players;
                if (players.Count == 0)
                {
                    y = Label("No players (not in a server?)", y, w);
                }
                else
                {
                    foreach (var p in players)
                    {
                        string localTag = p.IsLocal ? " (YOU)" : "";
                        string spoofTag = p.IsSuspectedSpoof ? " [SPOOF?]" : "";
                        Color labelColor = p.IsSuspectedSpoof ? Color.red : (p.IsLocal ? Color.cyan : Color.white);
                        GUI.color = labelColor;
                        y = Label($"[{p.SmallID}] {p.Username}{localTag}{spoofTag}", y, w);
                        GUI.color = Color.white;

                        // SteamID + clickable profile button
                        if (p.SteamID != 0)
                        {
                            GUI.Label(new Rect(PAD, y, w * 0.5f, ROW), $"   SteamID: {p.SteamID}");
                            GUI.color = _accentColor;
                            if (GUI.Button(new Rect(PAD + w * 0.5f, y, 130f, ROW - 2f), "Steam Profile", _cachedButtonStyle))
                            {
                                Application.OpenURL($"steam://url/SteamIDPage/{p.SteamID}");
                            }
                            GUI.color = Color.white;
                            y += ROW;
                        }
                        else
                        {
                            y = Label($"   SteamID: {p.SteamID}", y, w);
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Keybinds
        // ═══════════════════════════════════════════
        private static void DrawKeybindsPage(float y, float w)
        {
            y = Header("═══ KEYBINDS ═══", y, w);

            if (KeybindManager.IsRebinding)
            {
                y = Label($"Press a key to bind '{KeybindManager.RebindingName}' (ESC to cancel)", y, w);
                y = Gap(y, 10f);
            }

            var keybinds = KeybindManager.Keybinds;

            for (int i = 0; i < keybinds.Count; i++)
            {
                var kb = keybinds[i];
                string keyLabel = kb.Key == KeyCode.None ? "---" : kb.Key.ToString();
                bool isRebinding = KeybindManager.IsRebinding && KeybindManager.RebindingName == kb.Name;

                // Name label
                GUI.Label(new Rect(PAD, y, w * 0.5f, ROW), kb.Name);

                // Key button (click to rebind)
                GUI.color = isRebinding ? Color.yellow : (kb.Key == KeyCode.None ? Color.gray : Color.green);
                string btnText = isRebinding ? "[ PRESS KEY ]" : $"[{keyLabel}]";
                float btnW = 150f;
                if (GUI.Button(new Rect(PAD + w * 0.5f, y, btnW, ROW - 2f), btnText, _cachedButtonStyle))
                {
                    int idx = i;
                    KeybindManager.StartRebind(idx);
                }

                // Reset button
                GUI.color = Color.red;
                if (GUI.Button(new Rect(PAD + w * 0.5f + btnW + 5f, y, 60f, ROW - 2f), "Reset", _cachedButtonStyle))
                {
                    int idx = i;
                    KeybindManager.ResetToDefault(idx);
                }
                GUI.color = Color.white;

                y += ROW;
            }

            y = Gap(y, 20f);
            if (GUI.Button(new Rect(PAD, y, 200f, 30f), "Reset All to Defaults", _cachedButtonStyle))
                KeybindManager.ResetAllDefaults();
        }

        // ═══════════════════════════════════════════
        //  PAGE: Avatar (Searcher)
        // ═══════════════════════════════════════════
        private static void DrawAvatarPage(float y, float w)
        {
            y = Header("═══ AVATAR SEARCHER ═══", y, w);

            GUI.Label(new Rect(PAD, y, 70f, ROW), "Search:");
            _avatarSearchQuery = GUI.TextField(new Rect(PAD + 70f, y, w - 80f, ROW), _avatarSearchQuery ?? "");
            y += ROW + 4f;

            // Auto-search when text changes
            if (_avatarSearchQuery != _prevAvatarSearchQuery)
            {
                _prevAvatarSearchQuery = _avatarSearchQuery;
                _avatarSearchPage = 0;
                if (!string.IsNullOrEmpty(_avatarSearchQuery))
                    AvatarSearchController.SearchAvatars(_avatarSearchQuery);
            }

            var results = AvatarSearchController.GetLastResults();
            if (results != null && results.Count > 0)
            {
                int totalPages = (results.Count + AVATAR_ITEMS_PER_PAGE - 1) / AVATAR_ITEMS_PER_PAGE;
                _avatarSearchPage = Math.Clamp(_avatarSearchPage, 0, totalPages - 1);
                int start = _avatarSearchPage * AVATAR_ITEMS_PER_PAGE;
                int end = Math.Min(start + AVATAR_ITEMS_PER_PAGE, results.Count);

                y = Section($"Results: {results.Count} | Page {_avatarSearchPage + 1}/{totalPages}", y, w);

                for (int i = start; i < end; i++)
                {
                    var (name, barcode) = results[i];
                    GUI.Label(new Rect(PAD, y, w - 90f, ROW), name, _cachedLabelStyle);
                    if (GUI.Button(new Rect(PAD + w - 80f, y, 70f, ROW - 2f), "Swap", _cachedButtonStyle))
                        AvatarSearchController.SwapAvatar(barcode);
                    y += ROW;
                }

                // Nav buttons
                float navW = (w - 10f) / 2f;
                if (GUI.Button(new Rect(PAD, y, navW, ROW), "< Prev Page", _cachedButtonStyle))
                    _avatarSearchPage = Math.Max(0, _avatarSearchPage - 1);
                if (GUI.Button(new Rect(PAD + navW + 10f, y, navW, ROW), "Next Page >", _cachedButtonStyle))
                    _avatarSearchPage = Math.Min(totalPages - 1, _avatarSearchPage + 1);
                y += ROW + 4f;
            }
            else
            {
                y = Label("No results. Enter a query to search.", y, w);
            }
        }

        // ═══════════════════════════════════════════
        //  PAGE: Settings (Overlay Customization)
        // ═══════════════════════════════════════════
        private static float _accentR = 0f, _accentG = 1f, _accentB = 1f;
        private static float _sectionR = 1f, _sectionG = 0.92f, _sectionB = 0.016f;
        private static float _bgR = 0.15f, _bgG = 0.15f, _bgB = 0.15f, _bgA = 0.92f;
        private static float _gradEndR = 1f, _gradEndG = 0f, _gradEndB = 1f;
        private static bool _settingsInited = false;

        private static void DrawSettingsPage(float y, float w)
        {
            if (!_settingsInited)
            {
                _accentR = _baseAccentColor.r; _accentG = _baseAccentColor.g; _accentB = _baseAccentColor.b;
                _sectionR = _baseSectionColor.r; _sectionG = _baseSectionColor.g; _sectionB = _baseSectionColor.b;
                _bgR = _baseBgColor.r; _bgG = _baseBgColor.g; _bgB = _baseBgColor.b; _bgA = _baseBgColor.a;
                _gradEndR = _baseGradientEndColor.r; _gradEndG = _baseGradientEndColor.g; _gradEndB = _baseGradientEndColor.b;
                _settingsInited = true;
            }

            y = Header("═══ OVERLAY SETTINGS ═══", y, w);

            // Opacity
            float op = _menuOpacity;
            y = Slider("Opacity", ref op, 0.2f, 1f, y, w, "F2");
            _menuOpacity = op;

            // Font size
            float fs = (float)_fontSize;
            y = Slider("Font Size", ref fs, 10f, 24f, y, w, "F0");
            _fontSize = (int)fs;

            // Rainbow title
            bool rb = _rainbowTitle;
            y = Toggle(ref rb, "Rainbow Headers", y, w);
            if (rb != _rainbowTitle)
            {
                _rainbowTitle = rb;
                if (!rb) { _accentColor = _baseAccentColor; _sectionColor = _baseSectionColor; _bgColor = _baseBgColor; _bgTexDirty = true; _gradientEndColor = _baseGradientEndColor; }
            }

            // Gradient mode
            bool gr = _gradientEnabled;
            y = Toggle(ref gr, "Gradient Text", y, w);
            _gradientEnabled = gr;

            // Accent Color (used for headers + sidebar active)
            y = Gap(y, 10f);
            y = Section("Accent Color (Headers)", y, w);

            y = Slider("R", ref _accentR, 0f, 1f, y, w, "F2");
            y = Slider("G", ref _accentG, 0f, 1f, y, w, "F2");
            y = Slider("B", ref _accentB, 0f, 1f, y, w, "F2");
            _baseAccentColor = new Color(_accentR, _accentG, _accentB, 1f);
            if (!_rainbowTitle) _accentColor = _baseAccentColor;

            // Preview
            GUI.color = _accentColor;
            y = Label("■■■■■ Preview ■■■■■", y, w);
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);

            // Gradient End Color (only shown when gradient enabled)
            if (_gradientEnabled)
            {
                y = Gap(y, 10f);
                y = Section("Gradient End Color", y, w);

                y = Slider("R", ref _gradEndR, 0f, 1f, y, w, "F2");
                y = Slider("G", ref _gradEndG, 0f, 1f, y, w, "F2");
                y = Slider("B", ref _gradEndB, 0f, 1f, y, w, "F2");
                _baseGradientEndColor = new Color(_gradEndR, _gradEndG, _gradEndB, 1f);
                if (!_rainbowTitle) _gradientEndColor = _baseGradientEndColor;

                // Gradient preview
                _cachedHeaderStyle.richText = true;
                _cachedHeaderStyle.fontSize = _fontSize + 3;
                _cachedHeaderStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(PAD, y, w, 30f), GradientText("■■■ Gradient Preview ■■■", _accentColor, _gradientEndColor), _cachedHeaderStyle);
                _cachedHeaderStyle.richText = false;
                y += 30f;
            }

            // Section Color
            y = Gap(y, 10f);
            y = Section("Section Color", y, w);

            y = Slider("R", ref _sectionR, 0f, 1f, y, w, "F2");
            y = Slider("G", ref _sectionG, 0f, 1f, y, w, "F2");
            y = Slider("B", ref _sectionB, 0f, 1f, y, w, "F2");
            _baseSectionColor = new Color(_sectionR, _sectionG, _sectionB, 1f);
            if (!_rainbowTitle) _sectionColor = _baseSectionColor;

            GUI.color = _sectionColor;
            y = Label("■■■■■ Preview ■■■■■", y, w);
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);

            // Background Color
            y = Gap(y, 10f);
            y = Section("Background Color", y, w);

            y = Slider("R", ref _bgR, 0f, 1f, y, w, "F2");
            y = Slider("G", ref _bgG, 0f, 1f, y, w, "F2");
            y = Slider("B", ref _bgB, 0f, 1f, y, w, "F2");
            y = Slider("A", ref _bgA, 0.1f, 1f, y, w, "F2");
            Color newBaseBg = new Color(_bgR, _bgG, _bgB, _bgA);
            if (_baseBgColor != newBaseBg) { _baseBgColor = newBaseBg; if (!_rainbowTitle) { _bgColor = newBaseBg; _bgTexDirty = true; } }

            GUI.color = _bgColor;
            y = Label("■■■■■ Preview ■■■■■", y, w);
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);

            // Preset buttons
            y = Gap(y, 10f);
            y = Section("Presets", y, w);

            float presetW = (w - 20f) / 4f;
            if (GUI.Button(new Rect(PAD, y, presetW, ROW), "Default", _cachedButtonStyle))
            { _rainbowTitle = false; _gradientEnabled = false; _menuOpacity = 1f; _fontSize = 14; _baseAccentColor = Color.cyan; _accentColor = Color.cyan; _accentR = 0f; _accentG = 1f; _accentB = 1f; _baseSectionColor = Color.yellow; _sectionColor = Color.yellow; _sectionR = 1f; _sectionG = 0.92f; _sectionB = 0.016f; _baseBgColor = new Color(0.15f, 0.15f, 0.15f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.15f; _bgG = 0.15f; _bgB = 0.15f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = Color.magenta; _gradientEndColor = Color.magenta; _gradEndR = 1f; _gradEndG = 0f; _gradEndB = 1f; }
            GUI.color = Color.red;
            if (GUI.Button(new Rect(PAD + presetW + 5f, y, presetW, ROW), "Red", _cachedButtonStyle))
            { _rainbowTitle = false; _baseAccentColor = Color.red; _accentColor = Color.red; _accentR = 1f; _accentG = 0f; _accentB = 0f; _baseSectionColor = new Color(1f, 0.4f, 0.4f); _sectionColor = _baseSectionColor; _sectionR = 1f; _sectionG = 0.4f; _sectionB = 0.4f; _baseBgColor = new Color(0.2f, 0.05f, 0.05f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.2f; _bgG = 0.05f; _bgB = 0.05f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = new Color(1f, 0.6f, 0f); _gradientEndColor = _baseGradientEndColor; _gradEndR = 1f; _gradEndG = 0.6f; _gradEndB = 0f; }
            GUI.color = Color.green;
            if (GUI.Button(new Rect(PAD + (presetW + 5f) * 2f, y, presetW, ROW), "Green", _cachedButtonStyle))
            { _rainbowTitle = false; _baseAccentColor = Color.green; _accentColor = Color.green; _accentR = 0f; _accentG = 1f; _accentB = 0f; _baseSectionColor = new Color(0.4f, 1f, 0.4f); _sectionColor = _baseSectionColor; _sectionR = 0.4f; _sectionG = 1f; _sectionB = 0.4f; _baseBgColor = new Color(0.05f, 0.15f, 0.05f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.05f; _bgG = 0.15f; _bgB = 0.05f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = Color.cyan; _gradientEndColor = Color.cyan; _gradEndR = 0f; _gradEndG = 1f; _gradEndB = 1f; }
            GUI.color = new Color(1f, 0.5f, 0f);
            if (GUI.Button(new Rect(PAD + (presetW + 5f) * 3f, y, presetW, ROW), "Orange", _cachedButtonStyle))
            { _rainbowTitle = false; _baseAccentColor = new Color(1f, 0.5f, 0f); _accentColor = _baseAccentColor; _accentR = 1f; _accentG = 0.5f; _accentB = 0f; _baseSectionColor = new Color(1f, 0.7f, 0.3f); _sectionColor = _baseSectionColor; _sectionR = 1f; _sectionG = 0.7f; _sectionB = 0.3f; _baseBgColor = new Color(0.2f, 0.1f, 0.02f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.2f; _bgG = 0.1f; _bgB = 0.02f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = Color.yellow; _gradientEndColor = Color.yellow; _gradEndR = 1f; _gradEndG = 1f; _gradEndB = 0f; }
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
            y += ROW + 4f;

            float preset2W = (w - 15f) / 3f;
            GUI.color = Color.magenta;
            if (GUI.Button(new Rect(PAD, y, preset2W, ROW), "Purple", _cachedButtonStyle))
            { _rainbowTitle = false; _baseAccentColor = Color.magenta; _accentColor = Color.magenta; _accentR = 1f; _accentG = 0f; _accentB = 1f; _baseSectionColor = new Color(0.8f, 0.5f, 1f); _sectionColor = _baseSectionColor; _sectionR = 0.8f; _sectionG = 0.5f; _sectionB = 1f; _baseBgColor = new Color(0.12f, 0.02f, 0.15f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.12f; _bgG = 0.02f; _bgB = 0.15f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = Color.cyan; _gradientEndColor = Color.cyan; _gradEndR = 0f; _gradEndG = 1f; _gradEndB = 1f; }
            GUI.color = new Color(1f, 0.84f, 0f);
            if (GUI.Button(new Rect(PAD + preset2W + 5f, y, preset2W, ROW), "Gold", _cachedButtonStyle))
            { _rainbowTitle = false; _baseAccentColor = new Color(1f, 0.84f, 0f); _accentColor = _baseAccentColor; _accentR = 1f; _accentG = 0.84f; _accentB = 0f; _baseSectionColor = new Color(1f, 0.6f, 0f); _sectionColor = _baseSectionColor; _sectionR = 1f; _sectionG = 0.6f; _sectionB = 0f; _baseBgColor = new Color(0.18f, 0.12f, 0.02f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.18f; _bgG = 0.12f; _bgB = 0.02f; _bgA = 0.92f; _bgTexDirty = true; _baseGradientEndColor = new Color(1f, 0.4f, 0f); _gradientEndColor = _baseGradientEndColor; _gradEndR = 1f; _gradEndG = 0.4f; _gradEndB = 0f; }
            GUI.color = Color.white;
            if (GUI.Button(new Rect(PAD + (preset2W + 5f) * 2f, y, preset2W, ROW), "Rainbow", _cachedButtonStyle))
            { _rainbowTitle = true; _gradientEnabled = true; Color initAccent = Color.HSVToRGB(0f, 0.8f, 1f); _baseAccentColor = initAccent; _accentColor = initAccent; _accentR = initAccent.r; _accentG = initAccent.g; _accentB = initAccent.b; Color initSec = Color.HSVToRGB(0.15f, 0.8f, 1f); _baseSectionColor = initSec; _sectionColor = initSec; _sectionR = initSec.r; _sectionG = initSec.g; _sectionB = initSec.b; _baseBgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f); _bgColor = _baseBgColor; _bgR = 0.08f; _bgG = 0.08f; _bgB = 0.12f; _bgA = 0.92f; _bgTexDirty = true; Color initGrad = Color.HSVToRGB(0.65f, 0.8f, 1f); _baseGradientEndColor = initGrad; _gradientEndColor = initGrad; _gradEndR = initGrad.r; _gradEndG = initGrad.g; _gradEndB = initGrad.b; }
            GUI.color = new Color(1f, 1f, 1f, _menuOpacity);
            y += ROW + 4f;

            y = Gap(y, 10f);
            y = Button("Save Settings", y, 200f, 28f, () => SettingsManager.MarkDirty());
        }
    }
}
