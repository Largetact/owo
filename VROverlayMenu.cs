using MelonLoader;
using UnityEngine;
using BoneLib;
using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppSLZ.Marrow;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Seralyth-style 3D wrist menu.
    /// A physical panel attached above the left wrist, rendered in world space
    /// using TextMesh lines.
    ///
    /// Controls (one-handed, left hand):
    ///   Toggle Menu:         Left Grip + X
    ///   Scroll Up:           Y (without Grip)
    ///   Scroll Down:         X
    ///   Select / Expand:     Left Thumbstick Click
    ///   Increase Slider:     Left Trigger (hold)
    ///   Decrease Slider:     Left Grip (hold, without Y)
    ///   Next Page:           Y + Left Trigger
    ///   Previous Page:       X + Left Trigger (without Grip)
    ///   Laser Select:        Right hand aim + Right Trigger
    ///   Page Change:         Laser click on arrow buttons
    /// </summary>
    public static class VROverlayMenu
    {
        // ═══════════════════════════════════════════
        //  Menu State
        // ═══════════════════════════════════════════

        private static bool _visible;
        private static int _currentPage;
        private static int _cursorIndex;
        private static int _scrollOffset;

        // ─── Input debounce ───
        private static float _lastScrollTime;
        private const float SCROLL_REPEAT = 0.16f;
        private const float SCROLL_INITIAL = 0.28f;
        private static float _scrollHeld;
        private static int _lastScrollDir;

        private static float _lastAdjustTime;
        private const float ADJUST_REPEAT = 0.10f;
        private const float ADJUST_INITIAL = 0.28f;
        private static float _adjustHeld;
        private static int _lastAdjDir;

        private static bool _wasX;
        private static bool _wasTrigger;
        private static bool _wasLStick;
        private static bool _laserHovering;
        private static bool _laserOnLeftArrow, _laserOnRightArrow;

        // ─── Panel dimensions (world-space meters) ───
        private const float PW = 0.30f;
        private const float PH = 0.30f;
        private const float CS = 0.0020f;
        private const float LH = 0.012f;
        private const int MAX_LINES = 18;
        private const float CONTENT_START_Y = PH * 0.5f - 0.040f;
        private const float TITLE_Y = PH * 0.5f - 0.005f;
        private const float HINT_Y = PH * 0.5f - 0.020f;
        private const float ARROW_SIZE = 0.035f;

        // ─── Customization ───
        private static float _vrOpacity = 0.93f;
        private static float _vrAccentR = 0f, _vrAccentG = 0.95f, _vrAccentB = 1f;
        private static float _vrBgR = 0.05f, _vrBgG = 0.05f, _vrBgB = 0.09f;
        private static bool _vrRainbow = false;
        private static float _vrRainbowHue = 0f;

        // ─── Hand dominance ───
        // true = left hand dominant (panel on left, laser from right)
        // false = right hand dominant (panel on right, laser from left)
        private static bool _leftHandDominant = true;

        // ─── Page cycling debounce ───
        private static float _lastPageTime;
        private const float PAGE_REPEAT = 0.28f;
        private const float PAGE_INITIAL = 0.40f;
        private static float _pageHeld;
        private static int _lastPageDir;

        // Public accessors for SettingsManager persistence
        public static float VrOpacity { get => _vrOpacity; set => _vrOpacity = value; }
        public static float VrAccentR { get => _vrAccentR; set => _vrAccentR = value; }
        public static float VrAccentG { get => _vrAccentG; set => _vrAccentG = value; }
        public static float VrAccentB { get => _vrAccentB; set => _vrAccentB = value; }
        public static float VrBgR { get => _vrBgR; set => _vrBgR = value; }
        public static float VrBgG { get => _vrBgG; set => _vrBgG = value; }
        public static float VrBgB { get => _vrBgB; set => _vrBgB = value; }
        public static bool VrRainbow { get => _vrRainbow; set => _vrRainbow = value; }
        public static bool LeftHandDominant { get => _leftHandDominant; set => _leftHandDominant = value; }

        // ─── Collapsible section state ───
        // Movement
        private static bool _mDash, _mFlight, _mBunnyHop, _mAutoRun, _mSpinbot, _mTeleport, _mWaypoints;
        // Player
        private static bool _pGodMode, _pRagdoll, _pForceGrab;
        private static bool _pAntiConstraint, _pAntiKnockout, _pUnbreakGrip, _pAntiGrab;
        private static bool _pAntiGravChange, _pGhostMode, _pXyzScale;
        private static bool _pAntiRagdoll, _pAntiSlowmo, _pAntiTeleport;
        // Weapons
        private static bool _wChaosGun, _wFullAuto, _wInfAmmo, _wDamageMult;
        // Gun Visuals
        private static bool _gvCustomColor, _gvShaderLib, _gvTexEditor;
        // Combat
        private static bool _cExpPunch, _cGroundSlam, _cExpImpact, _cRandExplode;
        private static bool _cObjLauncher = true;
        private static bool _cRecoilRagdoll;
        private static bool _cHomingThrow;
        private static bool _cESP;
        private static bool _cItemESP;
        private static bool _cAimAssist;
        // Cosmetics
        private static bool _cosWeepingAngel, _cosAvatarCopier, _cosBodyLogColor;
        private static bool _cosHolsterHider, _cosAvatarFx;
        // Server
        private static bool _nAutoHost, _nServerQueue, _nFreezePlayer, _nBlockSystem;
        private static bool _nScreenShare, _nPlayerInfo, _nServerSettings;
        // Utility
        private static bool _uDespawn, _uAntiDespawn, _uSpawnLimiter, _uForceSpawner;
        private static bool _uMapChange, _uNotifications;
        private static bool _uAutoUpdater;

        // ─── Item Data Model ───
        private enum ItemType { Header, Toggle, Slider, Button, Label, EnumCycle }
        private struct MenuItem
        {
            public ItemType Type;
            public string Label;
            public bool BoolValue;
            public Action<bool> OnBoolChanged;
            public float FloatValue, Min, Max, Step;
            public Action<float> OnFloatChanged;
            public string FloatFmt;
            public Action OnClick;
            public string[] EnumNames;
            public int EnumIndex;
            public Action<int> OnEnumChanged;
            public bool Expanded;
            public Action<bool> OnExpandChanged;
        }
        private static readonly List<MenuItem> _items = new List<MenuItem>(128);

        // ═══════════════════════════════════════════
        //  3D Panel Objects
        // ═══════════════════════════════════════════

        private static bool _created;
        private static GameObject _root;
        private static GameObject _bgQuad;
        private static GameObject _cursorQuad;
        private static MeshRenderer _cursorRend;
        private static MeshRenderer _bgRend;
        private static MeshRenderer _borderRend;
        private static TextMesh _titleTM;
        private static TextMesh _hintTM;
        private static TextMesh _leftArrowTM;
        private static TextMesh _rightArrowTM;
        private static TextMesh[] _lineTMs;
        private static MeshRenderer[] _lineRends;
        private static Font _font;

        // ─── Laser pointer ───
        private static GameObject _laserObj;
        private static LineRenderer _laserLR;
        private static GameObject _laserDot;
        private static MeshRenderer _laserDotRend;

        public static bool IsVisible => _visible;

        private static readonly string[] PageNames = {
            "Movement", "Player", "Weapons", "Gun Visuals",
            "Combat", "Cosmetics", "Server", "Utility", "Settings"
        };

        // ═══════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════

        public static void CheckInput()
        {
            // Axis names depend on which hand is dominant (panel hand)
            string panelGripAxis = _leftHandDominant
                ? "Oculus_CrossPlatform_PrimaryHandTrigger"
                : "Oculus_CrossPlatform_SecondaryHandTrigger";
            string panelTrigAxis = _leftHandDominant
                ? "Oculus_CrossPlatform_PrimaryIndexTrigger"
                : "Oculus_CrossPlatform_SecondaryIndexTrigger";
            string laserTrigAxis = _leftHandDominant
                ? "Oculus_CrossPlatform_SecondaryIndexTrigger"
                : "Oculus_CrossPlatform_PrimaryIndexTrigger";

            // ─── Grip+X: toggle menu ───
            bool gripHeld = false;
            try { gripHeld = Input.GetAxis(panelGripAxis) > 0.7f; } catch { }
            bool xDown = Input.GetKey(KeyCode.JoystickButton2);

            if (gripHeld && xDown && !_wasX)
            {
                // Block opening if the panel hand is holding an object
                if (!_visible)
                {
                    try
                    {
                        var physRig = Player.PhysicsRig;
                        if (physRig != null)
                        {
                            var hand = _leftHandDominant ? physRig.leftHand : physRig.rightHand;
                            if (hand != null && hand.m_CurrentAttachedGO != null)
                            {
                                _wasX = xDown;
                                return; // holding an object — don't open
                            }
                        }
                    }
                    catch { }
                }
                _visible = !_visible;
                if (_visible) { _cursorIndex = 0; _scrollOffset = 0; }
            }
            _wasX = xDown;

            if (!_visible) return;

            float t = Time.unscaledTime;

            // ─── Read panel-hand trigger for combined checks ───
            float lTrig = 0f;
            try { lTrig = Input.GetAxis(panelTrigAxis); } catch { }
            bool yDown = Input.GetKey(KeyCode.JoystickButton3);

            // ─── Y + Panel Trigger = page forward, X + Panel Trigger = page backward ───
            bool pageFwd = yDown && lTrig > 0.5f;
            bool pageBack = xDown && lTrig > 0.5f && !gripHeld;

            if (pageFwd || pageBack)
            {
                int dir = pageFwd ? 1 : -1;
                if (dir != _lastPageDir)
                {
                    _currentPage = (_currentPage + dir + PageNames.Length) % PageNames.Length;
                    _cursorIndex = 0; _scrollOffset = 0;
                    _lastPageTime = t; _pageHeld = 0f; _lastPageDir = dir;
                }
                else
                {
                    _pageHeld += Time.unscaledDeltaTime;
                    float delay = _pageHeld < PAGE_INITIAL ? PAGE_INITIAL : PAGE_REPEAT;
                    if (t - _lastPageTime >= delay)
                    {
                        _currentPage = (_currentPage + dir + PageNames.Length) % PageNames.Length;
                        _cursorIndex = 0; _scrollOffset = 0;
                        _lastPageTime = t;
                    }
                }
            }
            else
            {
                _pageHeld = 0f; _lastPageDir = 0;
            }

            // ─── X = scroll down (no grip, no trigger), Y = scroll up (no grip, no trigger) ───
            bool scrollUp = yDown && !gripHeld && lTrig <= 0.5f;
            bool scrollDown = xDown && !gripHeld && lTrig <= 0.5f;

            if (scrollUp || scrollDown)
            {
                int dir = scrollDown ? 1 : -1;
                if (dir != _lastScrollDir)
                {
                    Move(dir); _lastScrollTime = t; _scrollHeld = 0f; _lastScrollDir = dir;
                }
                else
                {
                    _scrollHeld += Time.unscaledDeltaTime;
                    float delay = _scrollHeld < SCROLL_INITIAL ? SCROLL_INITIAL : SCROLL_REPEAT;
                    if (t - _lastScrollTime >= delay) { Move(dir); _lastScrollTime = t; }
                }
            }
            else
            {
                _scrollHeld = 0f; _lastScrollDir = 0;
            }

            // ─── Left thumbstick click = select/activate (ONLY way to select) ───
            bool lStick = Input.GetKey(KeyCode.JoystickButton8);
            if (lStick && !_wasLStick) Activate();
            _wasLStick = lStick;

            // ─── Panel Trigger = increase slider, Panel Grip (no X) = decrease slider ───
            // Only adjusts sliders — does NOT activate toggles/enums
            bool adjUp = lTrig > 0.5f && !yDown;  // exclude Y+Trigger (page cycling)
            bool adjDown = gripHeld && !xDown && lTrig <= 0.5f;

            if (adjUp || adjDown)
            {
                int dir = adjUp ? 1 : -1;
                if (dir != _lastAdjDir)
                {
                    Adjust(dir); _lastAdjustTime = t; _adjustHeld = 0f; _lastAdjDir = dir;
                }
                else
                {
                    _adjustHeld += Time.unscaledDeltaTime;
                    float delay = _adjustHeld < ADJUST_INITIAL ? ADJUST_INITIAL : ADJUST_REPEAT;
                    if (t - _lastAdjustTime >= delay) { Adjust(dir); _lastAdjustTime = t; }
                }
            }
            else
            {
                _adjustHeld = 0f; _lastAdjDir = 0;
            }

            // ─── Laser trigger: laser click (arrows or item) ───
            float rTrig = 0f;
            try { rTrig = Input.GetAxis(laserTrigAxis); } catch { }
            bool trigDown = rTrig > 0.7f;
            if (trigDown && !_wasTrigger)
            {
                if (_laserOnLeftArrow)
                {
                    _currentPage = (_currentPage - 1 + PageNames.Length) % PageNames.Length;
                    _cursorIndex = 0; _scrollOffset = 0;
                }
                else if (_laserOnRightArrow)
                {
                    _currentPage = (_currentPage + 1) % PageNames.Length;
                    _cursorIndex = 0; _scrollOffset = 0;
                }
                else if (_laserHovering)
                {
                    Activate();
                }
            }
            _wasTrigger = trigDown;
        }

        /// <summary>Called from OnUpdate after CheckInput — manages 3D panel.</summary>
        public static void UpdatePanel()
        {
            if (!_visible)
            {
                if (_created && _root != null) _root.SetActive(false);
                if (_laserObj != null) _laserObj.SetActive(false);
                if (_laserDot != null) _laserDot.SetActive(false);
                return;
            }

            if (!_created) CreatePanel();
            if (_root == null) { _created = false; return; }
            _root.SetActive(true);

            // ─── Position panel on dominant hand (clipboard style) ───
            Transform handT = null;
            try
            {
                var h = _leftHandDominant ? Player.LeftHand : Player.RightHand;
                if (h != null) handT = ((Component)h).transform;
            }
            catch { }
            if (handT == null) { _root.SetActive(false); return; }

            // Palm-up: panel hovers above the wrist, facing upward toward player
            Vector3 pos = handT.position
                + handT.up * 0.08f          // lift above wrist surface
                - handT.forward * 0.06f;    // slide back along forearm toward elbow
            // Panel face (-Z) points up toward eyes when palm is up
            Quaternion rot = Quaternion.LookRotation(-handT.up, handT.forward);
            // Snap directly — no interpolation lag
            _root.transform.position = pos;
            _root.transform.rotation = rot;

            // ─── Rainbow title cycling ───
            Color accent;
            if (_vrRainbow)
            {
                _vrRainbowHue += Time.unscaledDeltaTime * 0.3f;
                if (_vrRainbowHue > 1f) _vrRainbowHue -= 1f;
                accent = Color.HSVToRGB(_vrRainbowHue, 0.85f, 1f);
            }
            else
            {
                accent = new Color(_vrAccentR, _vrAccentG, _vrAccentB);
            }
            if (_titleTM != null) _titleTM.color = accent;
            if (_leftArrowTM != null) _leftArrowTM.color = _laserOnLeftArrow ? Color.white : accent;
            if (_rightArrowTM != null) _rightArrowTM.color = _laserOnRightArrow ? Color.white : accent;

            // ─── Apply dynamic colors ───
            if (_bgRend != null && _bgRend.material != null)
                _bgRend.material.color = new Color(_vrBgR, _vrBgG, _vrBgB, _vrOpacity);
            if (_borderRend != null && _borderRend.material != null)
                _borderRend.material.color = new Color(accent.r * 0.6f, accent.g * 0.6f, accent.b * 0.6f, 0.7f);
            if (_cursorRend != null && _cursorRend.material != null)
                _cursorRend.material.color = new Color(accent.r * 0.4f, accent.g * 0.4f, accent.b * 0.4f, 0.45f);

            // ─── Build items for current page ───
            _items.Clear();
            switch (_currentPage)
            {
                case 0: BuildMovementPage(); break;
                case 1: BuildPlayerPage(); break;
                case 2: BuildWeaponsPage(); break;
                case 3: BuildGunVisualsPage(); break;
                case 4: BuildCombatPage(); break;
                case 5: BuildCosmeticsPage(); break;
                case 6: BuildServerPage(); break;
                case 7: BuildUtilityPage(); break;
                case 8: BuildSettingsPage(); break;
            }

            if (_cursorIndex >= _items.Count && _items.Count > 0)
                _cursorIndex = _items.Count - 1;
            EnsureVisible();

            // ─── Update title + arrows ───
            _titleTM.text = PageNames[_currentPage] + "  [" + (_currentPage + 1) + "/" + PageNames.Length + "]";

            // ─── Update content lines ───
            for (int i = 0; i < MAX_LINES; i++)
            {
                int idx = _scrollOffset + i;
                if (idx >= _items.Count)
                {
                    _lineTMs[i].text = "";
                    continue;
                }
                var item = _items[idx];
                bool cur = (idx == _cursorIndex);
                _lineTMs[i].text = FormatItem(item, cur);
                _lineTMs[i].color = GetItemColor(item, cur);
            }

            // ─── Position cursor highlight ───
            if (_cursorIndex >= _scrollOffset && _cursorIndex < _scrollOffset + MAX_LINES)
            {
                _cursorQuad.SetActive(true);
                int visualIdx = _cursorIndex - _scrollOffset;
                float cy = CONTENT_START_Y - visualIdx * LH - LH * 0.5f;
                _cursorQuad.transform.localPosition = new Vector3(0f, cy, -0.001f);
            }
            else
            {
                _cursorQuad.SetActive(false);
            }

            // ═══════════════════════════════════════════
            //  Laser Pointer Raycast
            // ═══════════════════════════════════════════
            _laserHovering = false;
            _laserOnLeftArrow = false;
            _laserOnRightArrow = false;
            Transform rHandT = null;
            try
            {
                var rh = _leftHandDominant ? Player.RightHand : Player.LeftHand;
                if (rh != null) rHandT = ((Component)rh).transform;
            }
            catch { }

            if (rHandT != null && _root != null && _laserObj != null)
            {
                Vector3 rayOrigin = rHandT.position;
                Vector3 rayDir = rHandT.forward;

                Plane panelPlane = new Plane(-_root.transform.forward, _root.transform.position);
                Ray ray = new Ray(rayOrigin, rayDir);

                if (panelPlane.Raycast(ray, out float enter) && enter > 0.05f && enter < 1.5f)
                {
                    Vector3 worldHit = rayOrigin + rayDir * enter;
                    Vector3 localHit = _root.transform.InverseTransformPoint(worldHit);

                    float halfW = PW * 0.5f;
                    float contentTop = CONTENT_START_Y + LH * 0.5f;

                    // ─── Arrow hit zones (title area) ───
                    float arrowZoneBot = HINT_Y - 0.005f;
                    float arrowZoneTop = TITLE_Y + 0.008f;

                    if (localHit.y > arrowZoneBot && localHit.y < arrowZoneTop)
                    {
                        if (localHit.x > -halfW && localHit.x < -halfW + ARROW_SIZE)
                        {
                            _laserOnLeftArrow = true;
                            _laserHovering = true;
                        }
                        else if (localHit.x > halfW - ARROW_SIZE && localHit.x < halfW)
                        {
                            _laserOnRightArrow = true;
                            _laserHovering = true;
                        }
                    }

                    // ─── Content area hit (select items) ───
                    if (Mathf.Abs(localHit.x) < halfW &&
                        localHit.y < contentTop &&
                        localHit.y > CONTENT_START_Y - MAX_LINES * LH - LH)
                    {
                        int lineIdx = Mathf.FloorToInt((CONTENT_START_Y - localHit.y) / LH);
                        lineIdx = Mathf.Clamp(lineIdx, 0, MAX_LINES - 1);
                        int itemIdx = _scrollOffset + lineIdx;
                        if (itemIdx < _items.Count)
                        {
                            _cursorIndex = itemIdx;
                            _laserHovering = true;
                        }
                    }

                    // ─── Show laser beam and dot ───
                    bool onPanel = Mathf.Abs(localHit.x) < halfW + 0.02f &&
                                   localHit.y < PH * 0.5f + 0.01f &&
                                   localHit.y > -PH * 0.5f - 0.01f;
                    if (onPanel)
                    {
                        _laserObj.SetActive(true);
                        _laserLR.SetPosition(0, rayOrigin);
                        _laserLR.SetPosition(1, worldHit);

                        _laserDot.SetActive(true);
                        _laserDot.transform.position = worldHit - _root.transform.forward * 0.001f;
                    }
                    else
                    {
                        _laserObj.SetActive(true);
                        _laserLR.SetPosition(0, rayOrigin);
                        _laserLR.SetPosition(1, rayOrigin + rayDir * 0.4f);
                        _laserDot.SetActive(false);
                    }
                }
                else
                {
                    _laserObj.SetActive(true);
                    _laserLR.SetPosition(0, rayOrigin);
                    _laserLR.SetPosition(1, rayOrigin + rayDir * 0.4f);
                    _laserDot.SetActive(false);
                }
            }
            else
            {
                if (_laserObj != null) _laserObj.SetActive(false);
                if (_laserDot != null) _laserDot.SetActive(false);
            }
        }

        /// <summary>No-op — rendering is 3D world-space now. Kept for API compat.</summary>
        public static void Draw() { }

        /// <summary>Cleanup when level unloads — clear stale references.</summary>
        public static void OnLevelUnloaded()
        {
            if (_root != null) _root.SetActive(false);
            if (_laserObj != null) _laserObj.SetActive(false);
            if (_laserDot != null) _laserDot.SetActive(false);
            _visible = false;
        }

        // ═══════════════════════════════════════════
        //  Input Helpers
        // ═══════════════════════════════════════════

        private static void Move(int dir)
        {
            if (_items.Count == 0) return;
            _cursorIndex = Mathf.Clamp(_cursorIndex + dir, 0, _items.Count - 1);
            EnsureVisible();
        }

        private static void EnsureVisible()
        {
            if (_cursorIndex < _scrollOffset)
                _scrollOffset = _cursorIndex;
            else if (_cursorIndex >= _scrollOffset + MAX_LINES)
                _scrollOffset = _cursorIndex - MAX_LINES + 1;
            _scrollOffset = Mathf.Max(0, _scrollOffset);
        }

        private static void Activate()
        {
            if (_cursorIndex < 0 || _cursorIndex >= _items.Count) return;
            var item = _items[_cursorIndex];
            switch (item.Type)
            {
                case ItemType.Header:
                    item.OnExpandChanged?.Invoke(!item.Expanded);
                    break;
                case ItemType.Toggle:
                    item.OnBoolChanged?.Invoke(!item.BoolValue);
                    SettingsManager.MarkDirty();
                    break;
                case ItemType.Button:
                    item.OnClick?.Invoke();
                    break;
                case ItemType.EnumCycle:
                    if (item.EnumNames != null && item.EnumNames.Length > 0)
                    {
                        item.OnEnumChanged?.Invoke((item.EnumIndex + 1) % item.EnumNames.Length);
                        SettingsManager.MarkDirty();
                    }
                    break;
                case ItemType.Slider:
                    break;
            }
        }

        private static void Adjust(int dir)
        {
            if (_cursorIndex < 0 || _cursorIndex >= _items.Count) return;
            var item = _items[_cursorIndex];
            // Trigger/Grip adjust ONLY works on sliders — use thumbstick click for toggles/enums
            if (item.Type != ItemType.Slider) return;
            float step = item.Step > 0f ? item.Step : (item.Max - item.Min) * 0.05f;
            item.OnFloatChanged?.Invoke(Mathf.Clamp(item.FloatValue + step * dir, item.Min, item.Max));
            SettingsManager.MarkDirty();
        }

        private static void Back()
        {
            if (_cursorIndex >= 0 && _cursorIndex < _items.Count)
            {
                for (int i = _cursorIndex; i >= 0; i--)
                {
                    if (_items[i].Type == ItemType.Header && _items[i].Expanded)
                    {
                        _items[i].OnExpandChanged?.Invoke(false);
                        _cursorIndex = i;
                        return;
                    }
                }
            }
            _visible = false;
        }

        // ═══════════════════════════════════════════
        //  Panel Creation
        // ═══════════════════════════════════════════

        private static void CreatePanel()
        {
            if (_created) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _font = null;
            try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (_font == null) try { _font = Font.CreateDynamicFontFromOSFont("Arial", 64); } catch { }

            // ─── Root ───
            _root = new GameObject("VR_WristMenu");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            // ─── Background Quad ───
            _bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _bgQuad.name = "WM_BG";
            SafeDestroyCollider(_bgQuad);
            _bgQuad.transform.SetParent(_root.transform, false);
            _bgQuad.transform.localPosition = Vector3.zero;
            _bgQuad.transform.localScale = new Vector3(PW, PH, 1f);
            var bgMat = new Material(shader);
            bgMat.color = new Color(_vrBgR, _vrBgG, _vrBgB, _vrOpacity);
            bgMat.renderQueue = 3000;
            _bgQuad.GetComponent<MeshRenderer>().material = bgMat;
            _bgRend = _bgQuad.GetComponent<MeshRenderer>();

            // ─── Border Quad ───
            var border = GameObject.CreatePrimitive(PrimitiveType.Quad);
            border.name = "WM_Border";
            SafeDestroyCollider(border);
            border.transform.SetParent(_root.transform, false);
            border.transform.localPosition = new Vector3(0f, 0f, 0.0005f);
            border.transform.localScale = new Vector3(PW + 0.006f, PH + 0.006f, 1f);
            var borderMat = new Material(shader);
            borderMat.color = new Color(0f, 0.6f, 0.8f, 0.7f);
            borderMat.renderQueue = 2999;
            border.GetComponent<MeshRenderer>().material = borderMat;
            _borderRend = border.GetComponent<MeshRenderer>();

            // ─── Cursor Highlight Quad ───
            _cursorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cursorQuad.name = "WM_Cursor";
            SafeDestroyCollider(_cursorQuad);
            _cursorQuad.transform.SetParent(_root.transform, false);
            _cursorQuad.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            _cursorQuad.transform.localScale = new Vector3(PW * 0.94f, LH * 0.9f, 1f);
            var curMat = new Material(shader);
            curMat.color = new Color(0f, 0.55f, 0.75f, 0.45f);
            curMat.renderQueue = 3010;
            _cursorQuad.GetComponent<MeshRenderer>().material = curMat;
            _cursorRend = _cursorQuad.GetComponent<MeshRenderer>();

            // ─── Title Text (centered) ───
            _titleTM = MakeText("WM_Title", CS * 1.3f, new Color(_vrAccentR, _vrAccentG, _vrAccentB));
            _titleTM.anchor = TextAnchor.UpperCenter;
            _titleTM.alignment = TextAlignment.Center;
            _titleTM.transform.localPosition = new Vector3(0f, TITLE_Y, -0.002f);

            // ─── Left Arrow ───
            _leftArrowTM = MakeText("WM_LeftArrow", CS * 1.5f, new Color(_vrAccentR, _vrAccentG, _vrAccentB));
            _leftArrowTM.text = "\u25C0";
            _leftArrowTM.transform.localPosition = new Vector3(-PW * 0.44f, TITLE_Y, -0.002f);

            // ─── Right Arrow ───
            _rightArrowTM = MakeText("WM_RightArrow", CS * 1.5f, new Color(_vrAccentR, _vrAccentG, _vrAccentB));
            _rightArrowTM.anchor = TextAnchor.UpperRight;
            _rightArrowTM.alignment = TextAlignment.Right;
            _rightArrowTM.text = "\u25B6";
            _rightArrowTM.transform.localPosition = new Vector3(PW * 0.44f, TITLE_Y, -0.002f);

            // ─── Hint Text ───
            _hintTM = MakeText("WM_Hint", CS * 0.7f, new Color(0.45f, 0.45f, 0.5f));
            _hintTM.transform.localPosition = new Vector3(-PW * 0.45f, HINT_Y, -0.002f);
            _hintTM.text = "Grip+X:Menu  X/Y:Scroll  Stick:Select  Y+Trig:Page  Trig/Grip:Slider";

            // ─── Divider ───
            var divider = GameObject.CreatePrimitive(PrimitiveType.Quad);
            divider.name = "WM_Divider";
            SafeDestroyCollider(divider);
            divider.transform.SetParent(_root.transform, false);
            divider.transform.localPosition = new Vector3(0f, CONTENT_START_Y + LH * 0.3f, -0.0015f);
            divider.transform.localScale = new Vector3(PW * 0.9f, 0.001f, 1f);
            var divMat = new Material(shader);
            divMat.color = new Color(0f, 0.6f, 0.8f, 0.5f);
            divMat.renderQueue = 3005;
            divider.GetComponent<MeshRenderer>().material = divMat;

            // ─── Content Lines ───
            _lineTMs = new TextMesh[MAX_LINES];
            _lineRends = new MeshRenderer[MAX_LINES];
            for (int i = 0; i < MAX_LINES; i++)
            {
                _lineTMs[i] = MakeText($"WM_L{i}", CS, new Color(0.85f, 0.85f, 0.85f));
                _lineTMs[i].transform.localPosition = new Vector3(
                    -PW * 0.45f,
                    CONTENT_START_Y - i * LH,
                    -0.002f);
                _lineRends[i] = _lineTMs[i].GetComponent<MeshRenderer>();
            }

            // ─── Laser Pointer ───
            _laserObj = new GameObject("WM_Laser");
            UnityEngine.Object.DontDestroyOnLoad(_laserObj);
            _laserLR = _laserObj.AddComponent<LineRenderer>();
            _laserLR.useWorldSpace = true;
            _laserLR.startWidth = 0.003f;
            _laserLR.endWidth = 0.001f;
            _laserLR.positionCount = 2;
            _laserLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laserLR.receiveShadows = false;
            Shader laserShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (laserShader == null) laserShader = Shader.Find("SLZ/SLZ Unlit");
            if (laserShader == null) laserShader = Shader.Find("Unlit/Color");
            if (laserShader == null) laserShader = Shader.Find("Hidden/Internal-Colored");
            var laserMat = new Material(laserShader);
            laserMat.color = new Color(0f, 0.9f, 1f, 0.8f);
            laserMat.renderQueue = 3200;
            _laserLR.material = laserMat;
            _laserObj.SetActive(false);

            // ─── Laser Dot ───
            _laserDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _laserDot.name = "WM_LaserDot";
            SafeDestroyCollider(_laserDot);
            UnityEngine.Object.DontDestroyOnLoad(_laserDot);
            _laserDot.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);
            var dotMat = new Material(shader);
            dotMat.color = new Color(0f, 1f, 1f, 1f);
            dotMat.renderQueue = 3210;
            _laserDot.GetComponent<MeshRenderer>().material = dotMat;
            _laserDotRend = _laserDot.GetComponent<MeshRenderer>();
            _laserDot.SetActive(false);

            _root.SetActive(false);
            _created = true;
        }

        private static TextMesh MakeText(string name, float charSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshRenderer>();
            var tm = go.AddComponent<TextMesh>();
            tm.characterSize = charSize;
            tm.fontSize = 64;
            tm.color = color;
            tm.anchor = TextAnchor.UpperLeft;
            tm.richText = false;
            tm.text = "";
            if (_font != null)
            {
                tm.font = _font;
                var mr = go.GetComponent<MeshRenderer>();
                if (_font.material != null) mr.material = _font.material;
            }
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null && rend.material != null)
                rend.material.renderQueue = 3100;
            return tm;
        }

        private static void SafeDestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy((UnityEngine.Object)(object)col);
        }

        // ═══════════════════════════════════════════
        //  Item Formatting for TextMesh
        // ═══════════════════════════════════════════

        private static string FormatItem(MenuItem item, bool cur)
        {
            switch (item.Type)
            {
                case ItemType.Header:
                    return (item.Expanded ? "\u25BC " : "\u25BA ") + item.Label;
                case ItemType.Toggle:
                    return "  " + item.Label + (item.BoolValue ? "  ON" : "  OFF");
                case ItemType.Slider:
                    string fmt = item.FloatFmt ?? "F1";
                    return "  " + item.Label + ": " + item.FloatValue.ToString(fmt);
                case ItemType.Button:
                    return "  \u25B8 " + item.Label;
                case ItemType.Label:
                    return "  " + item.Label;
                case ItemType.EnumCycle:
                    string val = (item.EnumNames != null && item.EnumIndex >= 0 && item.EnumIndex < item.EnumNames.Length)
                        ? item.EnumNames[item.EnumIndex] : "?";
                    return "  " + item.Label + ": " + val;
                default:
                    return "";
            }
        }

        private static Color GetItemColor(MenuItem item, bool cur)
        {
            switch (item.Type)
            {
                case ItemType.Header:
                    return cur ? Color.white : new Color(_vrAccentR, _vrAccentG, _vrAccentB);
                case ItemType.Toggle:
                    if (item.BoolValue)
                        return cur ? new Color(0.4f, 1f, 0.7f) : new Color(0f, 0.85f, 0.45f);
                    else
                        return cur ? new Color(1f, 0.6f, 0.6f) : new Color(0.7f, 0.25f, 0.25f);
                case ItemType.Slider:
                    return cur ? Color.white : new Color(0.75f, 0.75f, 0.8f);
                case ItemType.Button:
                    return cur ? Color.yellow : new Color(0.3f, 0.7f, 1f);
                case ItemType.Label:
                    return new Color(0.5f, 0.5f, 0.55f);
                case ItemType.EnumCycle:
                    return cur ? Color.white : new Color(0.6f, 0.8f, 1f);
                default:
                    return Color.gray;
            }
        }

        // ═══════════════════════════════════════════
        //  Item Builders
        // ═══════════════════════════════════════════

        private static void AddToggle(string label, bool value, Action<bool> onChange)
        {
            _items.Add(new MenuItem { Type = ItemType.Toggle, Label = label, BoolValue = value, OnBoolChanged = onChange });
        }

        private static void AddSlider(string label, float value, float min, float max, float step, Action<float> onChange, string fmt = "F1")
        {
            _items.Add(new MenuItem { Type = ItemType.Slider, Label = label, FloatValue = value, Min = min, Max = max, Step = step, OnFloatChanged = onChange, FloatFmt = fmt });
        }

        private static void AddButton(string label, Action onClick)
        {
            _items.Add(new MenuItem { Type = ItemType.Button, Label = label, OnClick = onClick });
        }

        private static void AddLabel(string label)
        {
            _items.Add(new MenuItem { Type = ItemType.Label, Label = label });
        }

        private static void AddEnum(string label, string[] names, int index, Action<int> onChange)
        {
            _items.Add(new MenuItem { Type = ItemType.EnumCycle, Label = label, EnumNames = names, EnumIndex = index, OnEnumChanged = onChange });
        }

        // ═══════════════════════════════════════════
        //  Section Helper
        // ═══════════════════════════════════════════

        private static void Sec(string label, ref bool expanded, Action contents)
        {
            bool exp = expanded;
            int idx = _items.Count;
            _items.Add(new MenuItem { Type = ItemType.Header, Label = label, Expanded = exp });
            if (exp) contents();
            var p = _items[idx];
            p.OnExpandChanged = (v) => FlipSection(label, v);
            _items[idx] = p;
        }

        // ═══════════════════════════════════════════
        //  Page Builders
        // ═══════════════════════════════════════════

        private static void BuildMovementPage()
        {
            Sec("Dash", ref _mDash, () =>
            {
                AddToggle("Enabled", DashController.IsDashEnabled, v => DashController.IsDashEnabled = v);
                AddSlider("Dash Force", DashController.DashForce, 0f, 500f, 5f, v => DashController.DashForce = v, "F0");
                AddToggle("Instantaneous", DashController.IsDashInstantaneous, v => DashController.IsDashInstantaneous = v);
                AddToggle("Continuous", DashController.IsDashContinuous, v => DashController.IsDashContinuous = v);
                AddToggle("Hand Oriented", DashController.IsHandOriented, v => DashController.IsHandOriented = v);
                AddToggle("Use Left Hand", DashController.UseLeftHand, v => DashController.UseLeftHand = v);
                AddToggle("Lock-On", DashController.LockOnEnabled, v => DashController.LockOnEnabled = v);
                AddToggle("Kill Velocity On Land", DashController.KillVelocityOnLand, v => DashController.KillVelocityOnLand = v);
            });
            Sec("Flight", ref _mFlight, () =>
            {
                AddToggle("Enabled", FlightController.Enabled, v => FlightController.Enabled = v);
                AddSlider("Speed Multiplier", FlightController.SpeedMultiplier, 0.5f, 20f, 0.5f, v => FlightController.SpeedMultiplier = v, "F1");
                AddToggle("Acceleration", FlightController.AccelerationEnabled, v => FlightController.AccelerationEnabled = v);
                AddSlider("Accel Rate", FlightController.AccelerationRate, 0.1f, 10f, 0.1f, v => FlightController.AccelerationRate = v, "F1");
                AddToggle("Momentum", FlightController.MomentumEnabled, v => FlightController.MomentumEnabled = v);
                AddToggle("Lock-On", FlightController.LockOnEnabled, v => FlightController.LockOnEnabled = v);
            });
            Sec("Bunny Hop", ref _mBunnyHop, () =>
            {
                AddToggle("Enabled", BunnyHopController.Enabled, v => BunnyHopController.Enabled = v);
                AddSlider("Hop Boost", BunnyHopController.HopBoost, 0f, 20f, 0.5f, v => BunnyHopController.HopBoost = v, "F1");
                AddSlider("Max Speed", BunnyHopController.MaxSpeed, 5f, 200f, 5f, v => BunnyHopController.MaxSpeed = v, "F0");
                AddSlider("Air Strafe Force", BunnyHopController.AirStrafeForce, 0f, 50f, 1f, v => BunnyHopController.AirStrafeForce = v, "F1");
                AddSlider("Jump Force", BunnyHopController.JumpForce, 1f, 20f, 0.5f, v => BunnyHopController.JumpForce = v, "F1");
                AddToggle("Auto Hop", BunnyHopController.AutoHop, v => BunnyHopController.AutoHop = v);
                AddToggle("Auto Jump Toggle", BunnyHopController.AutoJumpToggle, v => BunnyHopController.AutoJumpToggle = v);
                AddToggle("Trimping", BunnyHopController.TrimpEnabled, v => BunnyHopController.TrimpEnabled = v);
                AddSlider("Trimp Multiplier", BunnyHopController.TrimpMultiplier, 0f, 3f, 0.1f, v => BunnyHopController.TrimpMultiplier = v, "F2");
            });
            Sec("Auto Run", ref _mAutoRun, () =>
            {
                AddToggle("Enabled", AutoRunController.Enabled, v => AutoRunController.Enabled = v);
            });
            Sec("Spinbot", ref _mSpinbot, () =>
            {
                AddToggle("Enabled", SpinbotController.Enabled, v => SpinbotController.Enabled = v);
                AddSlider("Speed (deg/s)", SpinbotController.Speed, 10f, 7200f, 90f, v => SpinbotController.Speed = v, "F0");
                AddEnum("Direction", Enum.GetNames(typeof(SpinDirection)), (int)SpinbotController.Direction, v => SpinbotController.Direction = (SpinDirection)v);
            });
            Sec("Teleport", ref _mTeleport, () =>
            {
                AddLabel(TeleportController.HasSavedPosition ? "Saved: " + TeleportController.SavedPositionText : "No saved position");
                AddButton("Save Position", () => TeleportController.SaveCurrentPosition());
                AddButton("Teleport to Saved", () => TeleportController.TeleportToSavedPosition());
                AddButton("Clear Saved", () => TeleportController.ClearSavedPosition());
                AddButton("Refresh Players", () => TeleportController.RefreshPlayerList());
                var tpPlayers = TeleportController.GetCachedPlayers();
                if (tpPlayers.Count > 0)
                {
                    for (int i = 0; i < tpPlayers.Count; i++)
                    {
                        var p = tpPlayers[i];
                        var capturedId = p.SmallID;
                        var capturedName = p.DisplayName;
                        AddButton($"TP > {capturedName}", () => TeleportController.TeleportToPlayerBySmallID(capturedId, capturedName));
                    }
                }
                else
                {
                    AddLabel("No players. Hit Refresh.");
                }
            });
            Sec("Waypoints", ref _mWaypoints, () =>
            {
                AddLabel("Waypoints: " + WaypointController.WaypointCount);
                AddSlider("Teleport Hold Time", WaypointController.TeleportHoldTime, 0.5f, 5f, 0.1f, v => WaypointController.TeleportHoldTime = v, "F1");
                AddButton("Create Waypoint", () => WaypointController.CreateWaypoint());
                AddButton("Clear All Waypoints", () => WaypointController.ClearAllWaypoints());
            });
        }

        private static void BuildPlayerPage()
        {
            Sec("God Mode", ref _pGodMode, () =>
            {
                AddToggle("God Mode", GodModeController.IsGodModeEnabled, v => GodModeController.IsGodModeEnabled = v);
            });
            Sec("Ragdoll", ref _pRagdoll, () =>
            {
                AddToggle("Enabled", RagdollController.Enabled, v => RagdollController.Enabled = v);
                AddEnum("Mode", Enum.GetNames(typeof(RagdollMode)), (int)RagdollController.Mode, v => RagdollController.Mode = (RagdollMode)v);
                AddToggle("Grab Ragdoll", RagdollController.GrabEnabled, v => RagdollController.GrabEnabled = v);
                AddToggle("Tantrum Mode", RagdollController.TantrumMode, v => RagdollController.TantrumMode = v);
                AddEnum("Keybind", Enum.GetNames(typeof(RagdollBinding)), (int)RagdollController.Binding, v => RagdollController.Binding = (RagdollBinding)v);
                AddButton("Unragdoll Now", () => { try { RagdollController.UnragdollPlayer(Player.PhysicsRig); } catch { } });
            });
            Sec("Force Grab", ref _pForceGrab, () =>
            {
                AddToggle("Enabled", ForceGrabController.IsEnabled, v => ForceGrabController.IsEnabled = v);
                AddToggle("Instant Mode", ForceGrabController.InstantMode, v => ForceGrabController.InstantMode = v);
                AddToggle("Global Mode", ForceGrabController.GlobalMode, v => ForceGrabController.GlobalMode = v);
                AddSlider("Fly Speed", ForceGrabController.FlySpeed, 5f, 200f, 5f, v => ForceGrabController.FlySpeed = v, "F0");
                AddToggle("Grip Only", ForceGrabController.GripOnly, v => ForceGrabController.GripOnly = v);
                AddToggle("Force Push", ForceGrabController.ForcePush, v => ForceGrabController.ForcePush = v);
                AddSlider("Push Force", ForceGrabController.PushForce, 5f, 500f, 5f, v => ForceGrabController.PushForce = v, "F0");
                AddButton("Clear Selected", () => ForceGrabController.ClearSelected());
            });
            Sec("Anti-Grab", ref _pAntiGrab, () =>
            {
                AddToggle("Enabled", AntiGrabController.Enabled, v => AntiGrabController.Enabled = v);
            });
            Sec("Anti-Constraint", ref _pAntiConstraint, () =>
            {
                AddToggle("Enabled", AntiConstraintController.IsEnabled, v => AntiConstraintController.IsEnabled = v);
                AddButton("Clear Constraints", () => AntiConstraintController.ClearConstraints());
            });
            Sec("Anti-Knockout", ref _pAntiKnockout, () =>
            {
                AddToggle("Enabled", AntiKnockoutController.IsEnabled, v => AntiKnockoutController.IsEnabled = v);
            });
            Sec("Unbreakable Grip", ref _pUnbreakGrip, () =>
            {
                AddToggle("Enabled", UnbreakableGripController.IsEnabled, v => UnbreakableGripController.IsEnabled = v);
            });
            Sec("Earth Loop (Lock Gravity)", ref _pAntiGravChange, () =>
            {
                AddToggle("Enabled", AntiGravityChangeController.Enabled, v => AntiGravityChangeController.Enabled = v);
            });
            Sec("Ghost Mode", ref _pGhostMode, () =>
            {
                AddToggle("Enabled", GhostModeController.Enabled, v => GhostModeController.Enabled = v);
            });
            Sec("XYZ Scale", ref _pXyzScale, () =>
            {
                AddToggle("Enabled", XYZScaleController.Enabled, v => XYZScaleController.Enabled = v);
                AddSlider("Scale X", XYZScaleController.ScaleX, 0.1f, 10f, 0.1f, v => XYZScaleController.ScaleX = v, "F1");
                AddSlider("Scale Y", XYZScaleController.ScaleY, 0.1f, 10f, 0.1f, v => XYZScaleController.ScaleY = v, "F1");
                AddSlider("Scale Z", XYZScaleController.ScaleZ, 0.1f, 10f, 0.1f, v => XYZScaleController.ScaleZ = v, "F1");
                AddButton("Apply Scale", () => XYZScaleController.ApplyScale());
            });
            Sec("Anti-Ragdoll", ref _pAntiRagdoll, () =>
            {
                AddToggle("Enabled", AntiRagdollController.Enabled, v => AntiRagdollController.Enabled = v);
            });
            Sec("Anti-Slowmo", ref _pAntiSlowmo, () =>
            {
                AddToggle("Enabled", AntiSlowmoController.Enabled, v => AntiSlowmoController.Enabled = v);
            });
            Sec("Anti-Teleport", ref _pAntiTeleport, () =>
            {
                AddToggle("Enabled", AntiTeleportController.Enabled, v => AntiTeleportController.Enabled = v);
            });
        }

        private static void BuildWeaponsPage()
        {
            Sec("Chaos Gun", ref _wChaosGun, () =>
            {
                AddToggle("Insane Damage", ChaosGunController.InsaneDamage, v => ChaosGunController.InsaneDamage = v);
                AddToggle("No Recoil", ChaosGunController.NoRecoil, v => ChaosGunController.NoRecoil = v);
                AddToggle("Insane Firerate", ChaosGunController.InsaneFirerate, v => ChaosGunController.InsaneFirerate = v);
                AddToggle("No Weight", ChaosGunController.NoWeight, v => ChaosGunController.NoWeight = v);
                AddToggle("Guns Bounce", ChaosGunController.GunsBounce, v => ChaosGunController.GunsBounce = v);
                AddToggle("No Reload", ChaosGunController.NoReload, v => ChaosGunController.NoReload = v);
            });
            Sec("Full Auto", ref _wFullAuto, () =>
            {
                AddToggle("Enabled", FullAutoController.IsFullAutoEnabled, v => FullAutoController.IsFullAutoEnabled = v);
                AddSlider("Fire Rate Multiplier", FullAutoController.FireRateMultiplier, 1f, 1000f, 10f, v => FullAutoController.FireRateMultiplier = v, "F0");
            });
            Sec("Infinite Ammo", ref _wInfAmmo, () =>
            {
                AddToggle("Infinite Ammo", InfiniteAmmoController.IsEnabled, v => InfiniteAmmoController.IsEnabled = v);
            });
            Sec("Damage Multiplier", ref _wDamageMult, () =>
            {
                AddSlider("Gun Multiplier", DamageMultiplierController.GunMultiplier, 0.1f, 100f, 0.5f, v => DamageMultiplierController.GunMultiplier = v, "F1");
                AddSlider("Melee Multiplier", DamageMultiplierController.MeleeMultiplier, 0.1f, 100f, 0.5f, v => DamageMultiplierController.MeleeMultiplier = v, "F1");
                AddButton("Apply Now", () => DamageMultiplierController.ApplyMultipliersNow());
                AddButton("Reset to 1x", () => { DamageMultiplierController.GunMultiplier = 1f; DamageMultiplierController.MeleeMultiplier = 1f; });
            });
        }

        private static void BuildGunVisualsPage()
        {
            Sec("Custom Gun Color", ref _gvCustomColor, () =>
            {
                AddToggle("Enabled", ChaosGunController.CustomGunColorEnabled, v => ChaosGunController.CustomGunColorEnabled = v);
                AddToggle("Rainbow", ChaosGunController.RainbowEnabled, v => ChaosGunController.RainbowEnabled = v);
                AddToggle("Emission", ChaosGunController.EmissionEnabled, v => ChaosGunController.EmissionEnabled = v);
                AddToggle("Reflection", ChaosGunController.ReflectionEnabled, v => ChaosGunController.ReflectionEnabled = v);
                AddToggle("Transparency", ChaosGunController.TransparencyEnabled, v => ChaosGunController.TransparencyEnabled = v);
                AddSlider("Transparency Amount", ChaosGunController.TransparencyAmount, 0f, 1f, 0.05f, v => ChaosGunController.TransparencyAmount = v, "F2");
                AddSlider("Emission Intensity", ChaosGunController.EmissionIntensity, 0f, 20f, 0.5f, v => ChaosGunController.EmissionIntensity = v, "F1");
                AddSlider("Rainbow Speed", ChaosGunController.RainbowSpeed, 0.01f, 2f, 0.05f, v => ChaosGunController.RainbowSpeed = v, "F2");
                AddSlider("Color R", ChaosGunController.ColorR, 0f, 1f, 0.05f, v => ChaosGunController.ColorR = v, "F2");
                AddSlider("Color G", ChaosGunController.ColorG, 0f, 1f, 0.05f, v => ChaosGunController.ColorG = v, "F2");
                AddSlider("Color B", ChaosGunController.ColorB, 0f, 1f, 0.05f, v => ChaosGunController.ColorB = v, "F2");
                AddToggle("Gradient", ChaosGunController.GradientEnabled, v => ChaosGunController.GradientEnabled = v);
                AddSlider("Gradient Speed", ChaosGunController.GradientSpeed, 0f, 5f, 0.1f, v => ChaosGunController.GradientSpeed = v, "F1");
                AddSlider("Gradient Spread", ChaosGunController.GradientSpread, 0.1f, 5f, 0.1f, v => ChaosGunController.GradientSpread = v, "F1");
                AddSlider("Color 2 R", ChaosGunController.Color2R, 0f, 1f, 0.05f, v => ChaosGunController.Color2R = v, "F2");
                AddSlider("Color 2 G", ChaosGunController.Color2G, 0f, 1f, 0.05f, v => ChaosGunController.Color2G = v, "F2");
                AddSlider("Color 2 B", ChaosGunController.Color2B, 0f, 1f, 0.05f, v => ChaosGunController.Color2B = v, "F2");
            });
            Sec("Shader Library", ref _gvShaderLib, () =>
            {
                AddToggle("Shader Library", ChaosGunController.ShaderLibraryEnabled, v => ChaosGunController.ShaderLibraryEnabled = v);
                AddToggle("Auto-Apply on Grab", ChaosGunController.AutoApplyShader, v => ChaosGunController.AutoApplyShader = v);
                AddToggle("Favorites Only", ChaosGunController.ShowFavoritesOnly, v => { ChaosGunController.ShowFavoritesOnly = v; SettingsManager.MarkDirty(); });
                AddButton("Refresh Shaders (" + ChaosGunController.ShaderCount + ")", () => ChaosGunController.RefreshShaderList());
                if (ChaosGunController.ShaderCount > 0)
                {
                    ChaosGunController.RebuildFilterIfNeeded();
                    string favStar = ChaosGunController.IsCurrentShaderFavorited() ? "\u2605" : "\u2606";
                    AddLabel("Shader: " + ChaosGunController.FilteredShaderName);
                    AddLabel("  (" + (ChaosGunController.FilteredCursor + 1) + "/" + ChaosGunController.FilteredCount + ")");
                    AddButton(favStar + " Toggle Favorite", () => { ChaosGunController.ToggleFavoriteCurrent(); SettingsManager.MarkDirty(); });
                    AddButton("<< Prev Shader", () => ChaosGunController.PrevShader());
                    AddButton("Next Shader >>", () => ChaosGunController.NextShader());
                    AddButton("Apply Shader", () => ChaosGunController.ApplyShaderToGun());
                    AddButton("Revert Shaders", () => ChaosGunController.RevertShaders());
                }
            });
            Sec("Texture Editor", ref _gvTexEditor, () =>
            {
                AddEnum("Mode", ChaosGunController.TextureModeNames, ChaosGunController.TextureMode,
                    v => ChaosGunController.TextureMode = v);
                if (ChaosGunController.TextureMode >= 2)
                {
                    AddSlider("Tex Color2 R", ChaosGunController.TexGradR2, 0f, 1f, 0.05f, v => ChaosGunController.TexGradR2 = v, "F2");
                    AddSlider("Tex Color2 G", ChaosGunController.TexGradG2, 0f, 1f, 0.05f, v => ChaosGunController.TexGradG2 = v, "F2");
                    AddSlider("Tex Color2 B", ChaosGunController.TexGradB2, 0f, 1f, 0.05f, v => ChaosGunController.TexGradB2 = v, "F2");
                }
                if (ChaosGunController.TextureMode == 3)
                    AddSlider("Noise Scale", ChaosGunController.TexNoiseScale, 1f, 50f, 1f, v => ChaosGunController.TexNoiseScale = v, "F0");
                AddSlider("Scroll Speed", ChaosGunController.TexScrollSpeed, 0f, 5f, 0.1f, v => ChaosGunController.TexScrollSpeed = v, "F1");
                AddButton("Apply Texture", () => ChaosGunController.ApplyTextureToGun());
                AddButton("Restore Textures", () => ChaosGunController.RestoreTextures());
            });
        }

        private static void BuildCombatPage()
        {
            Sec("Explosive Punch", ref _cExpPunch, () =>
            {
                AddToggle("Explosive Punch", ExplosivePunchController.IsExplosivePunchEnabled, v => ExplosivePunchController.IsExplosivePunchEnabled = v);
                AddToggle("Super Explosive", ExplosivePunchController.IsSuperExplosivePunchEnabled, v => ExplosivePunchController.IsSuperExplosivePunchEnabled = v);
                AddToggle("BLACKFLASH", ExplosivePunchController.IsBlackFlashEnabled, v => ExplosivePunchController.IsBlackFlashEnabled = v);
                AddToggle("Tiny Explosive", ExplosivePunchController.IsTinyExplosiveEnabled, v => ExplosivePunchController.IsTinyExplosiveEnabled = v);
                AddToggle("BOOM", ExplosivePunchController.IsBoomEnabled, v => ExplosivePunchController.IsBoomEnabled = v);
                AddToggle("Legacy Punch", ExplosivePunchController.IsLegacyPunchEnabled, v => ExplosivePunchController.IsLegacyPunchEnabled = v);
                AddSlider("Punch Speed", ExplosivePunchController.PunchVelocityThreshold, 1f, 15f, 0.5f, v => ExplosivePunchController.PunchVelocityThreshold = v, "F1");
                AddSlider("Cooldown", ExplosivePunchController.PunchCooldown, 0.05f, 1f, 0.05f, v => ExplosivePunchController.PunchCooldown = v, "F2");
            });
            Sec("Ground Slam", ref _cGroundSlam, () =>
            {
                AddToggle("Enabled", GroundPoundController.Enabled, v => GroundPoundController.Enabled = v);
                AddSlider("Velocity Threshold", GroundPoundController.VelocityThreshold, 1f, 50f, 1f, v => GroundPoundController.VelocityThreshold = v, "F1");
                AddSlider("Cooldown", GroundPoundController.Cooldown, 0.05f, 5f, 0.05f, v => GroundPoundController.Cooldown = v, "F2");
                AddEnum("Explosion Type", Enum.GetNames(typeof(ExplosionType)), (int)GroundPoundController.SelectedExplosion, v => GroundPoundController.SelectedExplosion = (ExplosionType)v);
            });
            Sec("Explosive Impact", ref _cExpImpact, () =>
            {
                AddToggle("Enabled", ExplosiveImpactController.Enabled, v => ExplosiveImpactController.Enabled = v);
                AddSlider("Velocity Threshold", ExplosiveImpactController.VelocityThreshold, 1f, 50f, 1f, v => ExplosiveImpactController.VelocityThreshold = v, "F1");
                AddSlider("Cooldown", ExplosiveImpactController.Cooldown, 0.05f, 5f, 0.05f, v => ExplosiveImpactController.Cooldown = v, "F2");
                AddEnum("Explosion Type", Enum.GetNames(typeof(ExplosionType)), (int)ExplosiveImpactController.SelectedExplosion, v => ExplosiveImpactController.SelectedExplosion = (ExplosionType)v);
            });
            Sec("Random Explode", ref _cRandExplode, () =>
            {
                AddToggle("Enabled", RandomExplodeController.Enabled, v => RandomExplodeController.Enabled = v);
                AddEnum("Explosion Type", Enum.GetNames(typeof(ExplosionType)), (int)RandomExplodeController.SelectedExplosion, v => RandomExplodeController.SelectedExplosion = (ExplosionType)v);
                AddSlider("Interval (s)", RandomExplodeController.Interval, 0.1f, 60f, 0.5f, v => RandomExplodeController.Interval = v, "F1");
                AddSlider("Launch Force", RandomExplodeController.LaunchForce, 0f, 1000f, 10f, v => RandomExplodeController.LaunchForce = v, "F0");
                AddToggle("Ragdoll on Explode", RandomExplodeController.RagdollOnExplode, v => RandomExplodeController.RagdollOnExplode = v);
                AddButton("Test Explosion Now", () => RandomExplodeController.TriggerExplosion());
            });
            Sec("Object Launcher", ref _cObjLauncher, () =>
            {
                AddToggle("Enabled", ObjectLauncherController.IsLauncherEnabled, v => ObjectLauncherController.IsLauncherEnabled = v);
                AddToggle("Safety (Grip+Trigger)", ObjectLauncherController.SafetyEnabled, v => ObjectLauncherController.SafetyEnabled = v);
                AddToggle("Left Hand", ObjectLauncherController.UseLeftHand, v => ObjectLauncherController.UseLeftHand = v);
                AddToggle("Full-Auto", ObjectLauncherController.IsFullAuto, v => ObjectLauncherController.IsFullAuto = v);
                AddSlider("Full-Auto Delay", ObjectLauncherController.FullAutoDelay, 0.01f, 1f, 0.01f, v => ObjectLauncherController.FullAutoDelay = v, "F2");
                AddToggle("Show Trajectory", ObjectLauncherController.ShowTrajectory, v => ObjectLauncherController.ShowTrajectory = v);
                AddSlider("Launch Force", ObjectLauncherController.LaunchForce, 0f, 10000f, 50f, v => ObjectLauncherController.LaunchForce = v, "F0");
                AddSlider("Spawn Distance", ObjectLauncherController.SpawnDistance, 0.5f, 10f, 0.1f, v => ObjectLauncherController.SpawnDistance = v, "F1");
                AddSlider("Projectile Count", ObjectLauncherController.ProjectileCount, 1f, 25f, 1f, v => ObjectLauncherController.ProjectileCount = (int)v, "F0");
                AddSlider("Spawn Scale", ObjectLauncherController.SpawnScale, 0.1f, 10f, 0.1f, v => ObjectLauncherController.SpawnScale = v, "F1");
                AddToggle("Homing", ObjectLauncherController.HomingEnabled, v => ObjectLauncherController.HomingEnabled = v);
                AddSlider("Homing Strength", ObjectLauncherController.HomingStrength, 0.5f, 50f, 0.5f, v => ObjectLauncherController.HomingStrength = v, "F1");
                AddSlider("Homing Speed", ObjectLauncherController.HomingSpeed, 0f, 500f, 5f, v => ObjectLauncherController.HomingSpeed = v, "F0");
                AddButton("Launch Object", () => ObjectLauncherController.LaunchObject());
                AddButton("Despawn Launched", () => ObjectLauncherController.DespawnLaunchedObjects());
                AddLabel("Current: " + (ObjectLauncherController.CurrentItemName ?? "None"));
            });
            Sec("Recoil Ragdoll", ref _cRecoilRagdoll, () =>
            {
                AddToggle("Enabled", RecoilRagdollController.Enabled, v => RecoilRagdollController.Enabled = v);
                AddSlider("Delay (s)", RecoilRagdollController.Delay, 0f, 2f, 0.01f, v => RecoilRagdollController.Delay = v, "F2");
                AddSlider("Cooldown (s)", RecoilRagdollController.Cooldown, 0.1f, 10f, 0.1f, v => RecoilRagdollController.Cooldown = v, "F1");
                AddSlider("Knockback Force", RecoilRagdollController.ForceMultiplier, 0f, 10f, 0.5f, v => RecoilRagdollController.ForceMultiplier = v, "F1");
                AddToggle("Drop Gun", RecoilRagdollController.DropGun, v => RecoilRagdollController.DropGun = v);
            });
            Sec("Homing Throw", ref _cHomingThrow, () =>
            {
                AddToggle("Enabled", HomingThrowController.Enabled, v => HomingThrowController.Enabled = v);
                AddEnum("Target Filter", System.Enum.GetNames(typeof(TargetFilter)), (int)HomingThrowController.Filter, v => HomingThrowController.Filter = (TargetFilter)v);
                AddSlider("Strength", HomingThrowController.Strength, 1f, 50f, 1f, v => HomingThrowController.Strength = v, "F1");
                AddSlider("Speed (0=throw)", HomingThrowController.Speed, 0f, 500f, 5f, v => HomingThrowController.Speed = v, "F0");
                AddSlider("Duration (0=inf)", HomingThrowController.Duration, 0f, 30f, 0.5f, v => HomingThrowController.Duration = v, "F1");
                AddSlider("Min Throw Speed", HomingThrowController.MinThrowSpeed, 0f, 20f, 0.5f, v => HomingThrowController.MinThrowSpeed = v, "F1");
                AddToggle("Rotation Lock", HomingThrowController.RotationLock, v => HomingThrowController.RotationLock = v);
                AddToggle("Acceleration", HomingThrowController.AccelEnabled, v => HomingThrowController.AccelEnabled = v);
                AddSlider("Accel Rate", HomingThrowController.AccelRate, 0.1f, 10f, 0.5f, v => HomingThrowController.AccelRate = v, "F1");
                AddToggle("Target Head", HomingThrowController.TargetHead, v => HomingThrowController.TargetHead = v);
                AddToggle("Momentum", HomingThrowController.Momentum, v => HomingThrowController.Momentum = v);
                AddSlider("Stay Duration", HomingThrowController.StayDuration, 0f, 30f, 0.5f, v => HomingThrowController.StayDuration = v, "F1");
                AddToggle("Recall (Shield)", HomingThrowController.RecallEnabled, v => HomingThrowController.RecallEnabled = v);
                AddSlider("Recall Speed", HomingThrowController.RecallSpeed, 1f, 50f, 1f, v => HomingThrowController.RecallSpeed = v, "F0");
                AddSlider("Recall Strength", HomingThrowController.RecallStrength, 1f, 30f, 1f, v => HomingThrowController.RecallStrength = v, "F1");
                AddToggle("FOV Cone", HomingThrowController.FovConeEnabled, v => HomingThrowController.FovConeEnabled = v);
                AddSlider("FOV Angle", HomingThrowController.FovAngle, 10f, 180f, 5f, v => HomingThrowController.FovAngle = v, "F0");
            });
            Sec("ESP", ref _cESP, () =>
            {
                AddToggle("Enabled", ESPController.Enabled, v => ESPController.Enabled = v);
                AddEnum("Mode", System.Enum.GetNames(typeof(ESPMode)), (int)ESPController.Mode, v => ESPController.Mode = (ESPMode)v);
                AddEnum("Color Mode", System.Enum.GetNames(typeof(ESPColorMode)), (int)ESPController.ColorMode, v => ESPController.ColorMode = (ESPColorMode)v);
                AddSlider("Near Color Dist", ESPController.NearColor, 1f, 100f, 5f, v => ESPController.NearColor = v, "F0");
                AddSlider("Far Color Dist", ESPController.FarColor, 10f, 500f, 10f, v => ESPController.FarColor = v, "F0");
                AddSlider("Tracer Width", ESPController.TracerWidth, 0.001f, 0.05f, 0.001f, v => ESPController.TracerWidth = v, "F3");
                AddSlider("Skeleton Width", ESPController.SkeletonWidth, 0.001f, 0.05f, 0.001f, v => ESPController.SkeletonWidth = v, "F3");
                if (ESPController.ColorMode == ESPColorMode.CustomRGB || ESPController.ColorMode == ESPColorMode.Gradient)
                {
                    AddSlider("Color R", ESPController.CustomR, 0f, 1f, 0.05f, v => ESPController.CustomR = v, "F2");
                    AddSlider("Color G", ESPController.CustomG, 0f, 1f, 0.05f, v => ESPController.CustomG = v, "F2");
                    AddSlider("Color B", ESPController.CustomB, 0f, 1f, 0.05f, v => ESPController.CustomB = v, "F2");
                }
                if (ESPController.ColorMode == ESPColorMode.Gradient)
                {
                    AddSlider("Gradient R2", ESPController.GradientR2, 0f, 1f, 0.05f, v => ESPController.GradientR2 = v, "F2");
                    AddSlider("Gradient G2", ESPController.GradientG2, 0f, 1f, 0.05f, v => ESPController.GradientG2 = v, "F2");
                    AddSlider("Gradient B2", ESPController.GradientB2, 0f, 1f, 0.05f, v => ESPController.GradientB2 = v, "F2");
                }
                if (ESPController.ColorMode == ESPColorMode.Rainbow)
                {
                    AddSlider("Rainbow Speed", ESPController.RainbowSpeed, 0.1f, 5f, 0.1f, v => ESPController.RainbowSpeed = v, "F1");
                }
            });
            Sec("Item ESP", ref _cItemESP, () =>
            {
                AddToggle("Enabled", ESPController.ItemESPEnabled, v => ESPController.ItemESPEnabled = v);
                AddEnum("Filter", System.Enum.GetNames(typeof(ItemESPFilter)), (int)ESPController.ItemFilter, v => ESPController.ItemFilter = (ItemESPFilter)v);
                AddSlider("Max Distance", ESPController.ItemMaxDistance, 10f, 500f, 10f, v => ESPController.ItemMaxDistance = v, "F0");
                AddSlider("Scan Interval", ESPController.ItemScanInterval, 0.1f, 2f, 0.1f, v => ESPController.ItemScanInterval = v, "F1");
                AddToggle("Show Labels", ESPController.ItemShowLabels, v => ESPController.ItemShowLabels = v);
                AddSlider("Beam Height", ESPController.ItemBeamHeight, 5f, 200f, 5f, v => ESPController.ItemBeamHeight = v, "F0");
                AddSlider("Beam Width", ESPController.ItemBeamWidth, 0.01f, 0.5f, 0.01f, v => ESPController.ItemBeamWidth = v, "F2");
                AddLabel("--- Category Colors ---");
                AddSlider("Prop R", ESPController.ItemColorR, 0f, 1f, 0.05f, v => ESPController.ItemColorR = v, "F2");
                AddSlider("Prop G", ESPController.ItemColorG, 0f, 1f, 0.05f, v => ESPController.ItemColorG = v, "F2");
                AddSlider("Prop B", ESPController.ItemColorB, 0f, 1f, 0.05f, v => ESPController.ItemColorB = v, "F2");
                AddSlider("Gun R", ESPController.ItemGunR, 0f, 1f, 0.05f, v => ESPController.ItemGunR = v, "F2");
                AddSlider("Gun G", ESPController.ItemGunG, 0f, 1f, 0.05f, v => ESPController.ItemGunG = v, "F2");
                AddSlider("Gun B", ESPController.ItemGunB, 0f, 1f, 0.05f, v => ESPController.ItemGunB = v, "F2");
                AddSlider("NPC R", ESPController.ItemNpcR, 0f, 1f, 0.05f, v => ESPController.ItemNpcR = v, "F2");
                AddSlider("NPC G", ESPController.ItemNpcG, 0f, 1f, 0.05f, v => ESPController.ItemNpcG = v, "F2");
                AddSlider("NPC B", ESPController.ItemNpcB, 0f, 1f, 0.05f, v => ESPController.ItemNpcB = v, "F2");
                AddSlider("Melee R", ESPController.ItemMeleeR, 0f, 1f, 0.05f, v => ESPController.ItemMeleeR = v, "F2");
                AddSlider("Melee G", ESPController.ItemMeleeG, 0f, 1f, 0.05f, v => ESPController.ItemMeleeG = v, "F2");
                AddSlider("Melee B", ESPController.ItemMeleeB, 0f, 1f, 0.05f, v => ESPController.ItemMeleeB = v, "F2");
            });
            Sec("Aim Assist", ref _cAimAssist, () =>
            {
                AddToggle("Enabled", AimAssistController.Enabled, v => AimAssistController.Enabled = v);
                AddToggle("Aimbot", AimAssistController.AimBotEnabled, v => AimAssistController.AimBotEnabled = v);
                AddSlider("Aimbot FOV", AimAssistController.AimFOV, 5f, 360f, 5f, v => AimAssistController.AimFOV = v, "F0");
                AddEnum("Target", System.Enum.GetNames(typeof(AimTarget)), (int)AimAssistController.Target, v => AimAssistController.Target = (AimTarget)v);
                AddToggle("Triggerbot", AimAssistController.TriggerBotEnabled, v => AimAssistController.TriggerBotEnabled = v);
                AddSlider("Triggerbot Delay", AimAssistController.TriggerBotDelay, 0f, 0.5f, 0.01f, v => AimAssistController.TriggerBotDelay = v, "F2");
                AddToggle("Headshots Only", AimAssistController.HeadshotsOnly, v => AimAssistController.HeadshotsOnly = v);
                AddToggle("Bullet Drop Comp", AimAssistController.BulletDropComp, v => AimAssistController.BulletDropComp = v);
                AddToggle("Movement Comp", AimAssistController.MovementComp, v => AimAssistController.MovementComp = v);
                AddToggle("Acceleration Comp", AimAssistController.AccelerationComp, v => AimAssistController.AccelerationComp = v);
                AddEnum("Smoothing", System.Enum.GetNames(typeof(CompensationSmoothing)), (int)AimAssistController.Smoothing, v => AimAssistController.Smoothing = (CompensationSmoothing)v);
            });
        }

        private static void BuildCosmeticsPage()
        {
            Sec("Weeping Angel", ref _cosWeepingAngel, () =>
            {
                AddToggle("Enabled", WeepingAngelController.Enabled, v => WeepingAngelController.Enabled = v);
                AddToggle("Target Everyone", WeepingAngelController.TargetEveryone, v => WeepingAngelController.TargetEveryone = v);
                AddSlider("View Angle", WeepingAngelController.ViewAngle, 10f, 180f, 5f, v => WeepingAngelController.ViewAngle = v, "F0");
                AddSlider("View Distance", WeepingAngelController.ViewDistance, 5f, 500f, 10f, v => WeepingAngelController.ViewDistance = v, "F0");
            });
            Sec("Avatar Copier", ref _cosAvatarCopier, () =>
            {
                AddToggle("Copy Nickname", AvatarCopierController.CopyNickname, v => AvatarCopierController.CopyNickname = v);
                AddToggle("Copy Description", AvatarCopierController.CopyDescription, v => AvatarCopierController.CopyDescription = v);
                AddToggle("Copy Cosmetics", AvatarCopierController.CopyCosmetics, v => AvatarCopierController.CopyCosmetics = v);
                AddLabel("Last: " + AvatarCopierController.LastCopiedInfo);
                AddButton("Refresh Players", () => AvatarCopierController.RefreshPlayerList());
                var acPlayers = AvatarCopierController.Players;
                if (acPlayers.Count > 0)
                {
                    AddLabel($"Players: {acPlayers.Count}");
                    for (int i = 0; i < acPlayers.Count; i++)
                    {
                        int idx = i;
                        var p = acPlayers[i];
                        AddButton($"{p.DisplayName} ({p.AvatarTitle})", () => AvatarCopierController.SelectAndCopy(idx));
                    }
                }
                else
                {
                    AddLabel("No players. Click Refresh.");
                }
                if (AvatarCopierController.HasRevertState)
                    AddButton("Revert Avatar", () => AvatarCopierController.RevertAvatar());
            });
            Sec("Body Log Color", ref _cosBodyLogColor, () =>
            {
                AddToggle("Enabled", BodyLogColorController.Enabled, v => BodyLogColorController.Enabled = v);
                AddButton("Apply All Colors", () => BodyLogColorController.ApplyAll());
            });
            Sec("Holster Hider", ref _cosHolsterHider, () =>
            {
                AddToggle("Hide Holsters", HolsterHiderController.HideHolsters, v => HolsterHiderController.HideHolsters = v);
                AddToggle("Hide Ammo Pouches", HolsterHiderController.HideAmmoPouch, v => HolsterHiderController.HideAmmoPouch = v);
                AddToggle("Hide Body Log", HolsterHiderController.HideBodyLog, v => HolsterHiderController.HideBodyLog = v);
                AddButton("Apply Now", () => HolsterHiderController.Apply());
            });
            Sec("Disable Avatar FX", ref _cosAvatarFx, () =>
            {
                AddToggle("Disable Switch Effects", DisableAvatarFXController.Enabled, v => DisableAvatarFXController.Enabled = v);
            });
        }

        private static void BuildServerPage()
        {
            Sec("Auto Host", ref _nAutoHost, () =>
            {
                AddToggle("Friends-Only on Launch", AutoHostController.Enabled, v => AutoHostController.Enabled = v);
            });
            Sec("Server Queue", ref _nServerQueue, () =>
            {
                AddToggle("Enabled", ServerQueueController.Enabled, v => ServerQueueController.Enabled = v);
                AddSlider("Poll Interval (s)", ServerQueueController.PollInterval, 5f, 60f, 1f, v => ServerQueueController.PollInterval = v, "F0");
                AddLabel(ServerQueueController.StatusText ?? "");
                if (ServerQueueController.IsInQueue)
                    AddButton("Stop Queue", () => ServerQueueController.StopQueue());
            });
            Sec("Freeze Player", ref _nFreezePlayer, () =>
            {
                AddButton("Refresh Players", () => FreezePlayerController.RefreshPlayers());
                AddButton("Unfreeze All", () => FreezePlayerController.UnfreezeAll());
                var fpPlayers = FreezePlayerController.CachedPlayers;
                if (fpPlayers.Count > 0)
                {
                    AddLabel($"Players: {fpPlayers.Count}");
                    for (int i = 0; i < fpPlayers.Count; i++)
                    {
                        var p = fpPlayers[i];
                        bool frozen = FreezePlayerController.IsFrozen(p.SmallID);
                        var capturedId = p.SmallID;
                        var capturedName = p.DisplayName;
                        var capturedRig = p.Rig;
                        string label = frozen ? $"[FROZEN] {capturedName}" : capturedName;
                        AddButton(label, () => FreezePlayerController.ToggleFreeze(capturedId, capturedName, capturedRig));
                    }
                }
                else
                {
                    AddLabel("No players. Click Refresh.");
                }
            });
            Sec("Block System", ref _nBlockSystem, () =>
            {
                AddToggle("Player Block", BlockController.PlayerBlockEnabled, v => BlockController.PlayerBlockEnabled = v);
                AddToggle("Item Block", BlockController.ItemBlockEnabled, v => BlockController.ItemBlockEnabled = v);
                AddToggle("Local Block", BlockController.LocalBlockEnabled, v => BlockController.LocalBlockEnabled = v);
                AddButton("Despawn Held Item", () => BlockController.DespawnHeldItem());
                AddButton("Fix Wobbly Avatar", () => BlockController.FixWobblyAvatar());
            });
            Sec("Screen Share", ref _nScreenShare, () =>
            {
                AddToggle("Enabled", ScreenShareController.Enabled, v => ScreenShareController.SetEnabled(v));
                AddToggle("Preview Visible", ScreenShareController.PreviewVisible, v => ScreenShareController.SetPreviewVisible(v));
                AddButton("Reposition", () => ScreenShareController.Reposition());
            });
            Sec("Server Settings", ref _nServerSettings, () =>
            {
                AddButton("Toggle NameTags", () => ServerSettingsController.ToggleNameTags());
                AddButton("Toggle VoiceChat", () => ServerSettingsController.ToggleVoiceChat());
                AddButton("Toggle Mortality", () => ServerSettingsController.ToggleMortality());
                AddButton("Toggle FriendlyFire", () => ServerSettingsController.ToggleFriendlyFire());
                AddButton("Toggle Knockout", () => ServerSettingsController.ToggleKnockout());
                AddButton("Toggle Constraining", () => ServerSettingsController.TogglePlayerConstraining());
            });
            Sec("Player Info", ref _nPlayerInfo, () =>
            {
                AddButton("Refresh Player List", () => PlayerInfoController.ForceRefresh());
                var players = PlayerInfoController.Players;
                if (players.Count > 0)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        var p = players[i];
                        string localTag = p.IsLocal ? " (YOU)" : "";
                        string spoofTag = p.IsSuspectedSpoof ? " [SPOOF?]" : "";
                        AddLabel($"[{p.SmallID}] {p.Username}{localTag}{spoofTag}");
                        if (p.SteamID != 0)
                            AddLabel($"  SteamID: {p.SteamID}");
                    }
                }
                else
                {
                    AddLabel("No players (not in a server?)");
                }
            });
        }

        private static void BuildUtilityPage()
        {
            Sec("Despawn All", ref _uDespawn, () =>
            {
                AddButton("Despawn Now", () => DespawnAllController.DespawnAll());
                AddEnum("Filter", Enum.GetNames(typeof(DespawnFilter)), (int)DespawnAllController.Filter, v => DespawnAllController.Filter = (DespawnFilter)v);
                AddToggle("Keep Holstered Items", DespawnAllController.KeepHolsteredItems, v => DespawnAllController.KeepHolsteredItems = v);
                AddToggle("Auto Despawn", DespawnAllController.AutoDespawnEnabled, v => DespawnAllController.AutoDespawnEnabled = v);
                AddSlider("Auto Interval (min)", DespawnAllController.AutoDespawnIntervalMins, 0.5f, 30f, 0.5f, v => DespawnAllController.AutoDespawnIntervalMins = v, "F1");
                AddToggle("Despawn on Disconnect", DespawnAllController.DespawnOnDisconnect, v => DespawnAllController.DespawnOnDisconnect = v);
            });
            Sec("Anti-Despawn", ref _uAntiDespawn, () =>
            {
                AddToggle("Enabled", AntiDespawnController.Enabled, v => AntiDespawnController.Enabled = v);
            });
            Sec("Spawn Limiter", ref _uSpawnLimiter, () =>
            {
                AddToggle("Enabled", SpawnLimiterController.Enabled, v => SpawnLimiterController.Enabled = v);
                AddToggle("Host Only", SpawnLimiterController.HostOnly, v => SpawnLimiterController.HostOnly = v);
                AddSlider("Spawn Delay (s)", SpawnLimiterController.SpawnDelay, 0.05f, 5f, 0.05f, v => SpawnLimiterController.SpawnDelay = v, "F2");
                AddSlider("Max Per Frame", SpawnLimiterController.MaxPerFrame, 1f, 50f, 1f, v => SpawnLimiterController.MaxPerFrame = (int)v, "F0");
            });
            Sec("Force Spawner", ref _uForceSpawner, () =>
            {
                AddToggle("Enabled", ForceSpawnerController.Enabled, v => ForceSpawnerController.Enabled = v);
                AddToggle("Unredact All", ForceSpawnerController.UnredactAll, v => ForceSpawnerController.UnredactAll = v);
                AddSlider("Distance", ForceSpawnerController.Distance, 1f, 50f, 1f, v => ForceSpawnerController.Distance = (int)v, "F0");
            });
            Sec("Map Change", ref _uMapChange, () =>
            {
                AddButton("Reload Current Level", () => MapChangeController.ReloadLevel());
            });
            Sec("Notifications", ref _uNotifications, () =>
            {
                AddToggle("Notifications Enabled", NotificationHelper.NotificationsEnabled, v => NotificationHelper.NotificationsEnabled = v);
            });
            Sec("Auto-Updater", ref _uAutoUpdater, () =>
            {
                AddToggle("Auto Check", AutoUpdaterController.AutoCheckEnabled, v => AutoUpdaterController.AutoCheckEnabled = v);
                AddToggle("Auto Install", AutoUpdaterController.AutoInstallEnabled, v => AutoUpdaterController.AutoInstallEnabled = v);
                AddToggle("Backup Old DLLs", AutoUpdaterController.BackupOldDlls, v => AutoUpdaterController.BackupOldDlls = v);
                AddToggle("Notify on Update", AutoUpdaterController.NotifyOnUpdate, v => AutoUpdaterController.NotifyOnUpdate = v);
                AddSlider("Check Interval (hrs)", AutoUpdaterController.CheckIntervalHours, 0.5f, 168f, 0.5f, v => AutoUpdaterController.CheckIntervalHours = v, "F1");
                AddLabel("Status: " + AutoUpdaterController.StatusMessage);
                AddButton("Scan DLLs", () => AutoUpdaterController.RefreshDllList());
                AddButton("Open Mods Folder", () => AutoUpdaterController.OpenModsFolder());
                AddButton("Open Backups Folder", () => AutoUpdaterController.OpenBackupsFolder());
                AddLabel("DLLs: " + AutoUpdaterController.InstalledDllCount);
                var dlls = AutoUpdaterController.GetInstalledDlls();
                for (int i = 0; i < dlls.Count && i < 20; i++)
                {
                    var dll = dlls[i];
                    AddLabel($"  {dll.FileName} ({dll.SizeText})");
                }
            });
        }

        private static void BuildSettingsPage()
        {
            AddLabel("--- VR Wrist Menu Settings ---");
            AddSlider("Panel Opacity", _vrOpacity, 0.2f, 1f, 0.05f, v => _vrOpacity = v, "F2");
            AddToggle("Rainbow Title", _vrRainbow, v => _vrRainbow = v);
            AddLabel("--- Accent Color ---");
            AddSlider("Accent R", _vrAccentR, 0f, 1f, 0.05f, v => _vrAccentR = v, "F2");
            AddSlider("Accent G", _vrAccentG, 0f, 1f, 0.05f, v => _vrAccentG = v, "F2");
            AddSlider("Accent B", _vrAccentB, 0f, 1f, 0.05f, v => _vrAccentB = v, "F2");
            AddLabel("--- Background Color ---");
            AddSlider("BG R", _vrBgR, 0f, 1f, 0.05f, v => _vrBgR = v, "F2");
            AddSlider("BG G", _vrBgG, 0f, 1f, 0.05f, v => _vrBgG = v, "F2");
            AddSlider("BG B", _vrBgB, 0f, 1f, 0.05f, v => _vrBgB = v, "F2");
            AddLabel("--- Presets ---");
            AddButton("Default (Cyan)", () => { _vrAccentR = 0f; _vrAccentG = 0.95f; _vrAccentB = 1f; _vrBgR = 0.05f; _vrBgG = 0.05f; _vrBgB = 0.09f; _vrOpacity = 0.93f; _vrRainbow = false; });
            AddButton("Red", () => { _vrAccentR = 1f; _vrAccentG = 0.2f; _vrAccentB = 0.2f; _vrBgR = 0.12f; _vrBgG = 0.02f; _vrBgB = 0.02f; _vrRainbow = false; });
            AddButton("Green", () => { _vrAccentR = 0.2f; _vrAccentG = 1f; _vrAccentB = 0.3f; _vrBgR = 0.02f; _vrBgG = 0.1f; _vrBgB = 0.02f; _vrRainbow = false; });
            AddButton("Purple", () => { _vrAccentR = 0.7f; _vrAccentG = 0.3f; _vrAccentB = 1f; _vrBgR = 0.06f; _vrBgG = 0.02f; _vrBgB = 0.1f; _vrRainbow = false; });
            AddButton("Gold", () => { _vrAccentR = 1f; _vrAccentG = 0.85f; _vrAccentB = 0.3f; _vrBgR = 0.1f; _vrBgG = 0.08f; _vrBgB = 0.02f; _vrRainbow = false; });
            AddButton("Rainbow", () => { _vrRainbow = true; });
            AddLabel("");
            AddToggle("Notifications", NotificationHelper.NotificationsEnabled, v => NotificationHelper.NotificationsEnabled = v);
            AddButton("Save Settings Now", () => SettingsManager.ForceSave());
            AddLabel("");
            AddLabel("--- Hand Dominance ---");
            AddToggle("Left Hand Panel", _leftHandDominant, v => { _leftHandDominant = v; SettingsManager.MarkDirty(); });
        }

        // ═══════════════════════════════════════════
        //  FlipSection
        // ═══════════════════════════════════════════

        private static void FlipSection(string label, bool value)
        {
            // Movement
            if (label == "Dash") _mDash = value;
            else if (label == "Flight") _mFlight = value;
            else if (label == "Bunny Hop") _mBunnyHop = value;
            else if (label == "Auto Run") _mAutoRun = value;
            else if (label == "Spinbot") _mSpinbot = value;
            else if (label == "Teleport") _mTeleport = value;
            else if (label == "Waypoints") _mWaypoints = value;
            // Player
            else if (label == "God Mode") _pGodMode = value;
            else if (label == "Ragdoll") _pRagdoll = value;
            else if (label == "Force Grab") _pForceGrab = value;
            else if (label == "Anti-Grab") _pAntiGrab = value;
            else if (label == "Anti-Constraint") _pAntiConstraint = value;
            else if (label == "Anti-Knockout") _pAntiKnockout = value;
            else if (label == "Unbreakable Grip") _pUnbreakGrip = value;
            else if (label == "Earth Loop (Lock Gravity)") _pAntiGravChange = value;
            else if (label == "Ghost Mode") _pGhostMode = value;
            else if (label == "XYZ Scale") _pXyzScale = value;
            else if (label == "Anti-Ragdoll") _pAntiRagdoll = value;
            else if (label == "Anti-Slowmo") _pAntiSlowmo = value;
            else if (label == "Anti-Teleport") _pAntiTeleport = value;
            // Weapons
            else if (label == "Chaos Gun") _wChaosGun = value;
            else if (label == "Full Auto") _wFullAuto = value;
            else if (label == "Infinite Ammo") _wInfAmmo = value;
            else if (label == "Damage Multiplier") _wDamageMult = value;
            // Gun Visuals
            else if (label == "Custom Gun Color") _gvCustomColor = value;
            else if (label == "Shader Library") _gvShaderLib = value;
            else if (label == "Texture Editor") _gvTexEditor = value;
            // Combat
            else if (label == "Explosive Punch") _cExpPunch = value;
            else if (label == "Ground Slam") _cGroundSlam = value;
            else if (label == "Explosive Impact") _cExpImpact = value;
            else if (label == "Random Explode") _cRandExplode = value;
            else if (label == "Object Launcher") _cObjLauncher = value;
            else if (label == "Recoil Ragdoll") _cRecoilRagdoll = value;
            else if (label == "Homing Throw") _cHomingThrow = value;
            else if (label == "ESP") _cESP = value;
            else if (label == "Item ESP") _cItemESP = value;
            else if (label == "Aim Assist") _cAimAssist = value;
            // Cosmetics
            else if (label == "Weeping Angel") _cosWeepingAngel = value;
            else if (label == "Avatar Copier") _cosAvatarCopier = value;
            else if (label == "Body Log Color") _cosBodyLogColor = value;
            else if (label == "Holster Hider") _cosHolsterHider = value;
            else if (label == "Disable Avatar FX") _cosAvatarFx = value;
            // Server
            else if (label == "Auto Host") _nAutoHost = value;
            else if (label == "Server Queue") _nServerQueue = value;
            else if (label == "Freeze Player") _nFreezePlayer = value;
            else if (label == "Block System") _nBlockSystem = value;
            else if (label == "Screen Share") _nScreenShare = value;
            else if (label == "Server Settings") _nServerSettings = value;
            else if (label == "Player Info") _nPlayerInfo = value;
            // Utility
            else if (label == "Despawn All") _uDespawn = value;
            else if (label == "Anti-Despawn") _uAntiDespawn = value;
            else if (label == "Spawn Limiter") _uSpawnLimiter = value;
            else if (label == "Force Spawner") _uForceSpawner = value;
            else if (label == "Map Change") _uMapChange = value;
            else if (label == "Notifications") _uNotifications = value;
            else if (label == "Auto-Updater") _uAutoUpdater = value;
        }
    }

    // ═══════════════════════════════════════════
    //  Harmony patches: block game inputs while VR overlay is open
    // ═══════════════════════════════════════════

    [HarmonyPatch(typeof(OpenController), "CheckMenuTap")]
    internal static class BlockMenuTapPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix() => !VROverlayMenu.IsVisible;
    }

    [HarmonyPatch(typeof(TimeManager), "TOGGLE_TIMESCALE")]
    internal static class BlockSlowmoTogglePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix() => !VROverlayMenu.IsVisible;
    }

    [HarmonyPatch(typeof(TimeManager), "DECREASE_TIMESCALE")]
    internal static class BlockSlowmoDecreasePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix() => !VROverlayMenu.IsVisible;
    }
}
