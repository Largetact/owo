using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using System.Linq;

namespace BonelabUtilityMod
{
    [HarmonyPatch]
    public static class ChaosGunController
    {
        // ── Custom Gun Color system ──
        private static bool _customGunColorEnabled = false;
        public static bool CustomGunColorEnabled
        {
            get => _customGunColorEnabled;
            set
            {
                if (_customGunColorEnabled && !value) RestoreCustomColor();
                _customGunColorEnabled = value;
            }
        }
        public static bool RainbowEnabled = true;
        public static bool EmissionEnabled = true;
        public static bool ReflectionEnabled = true;
        public static bool TransparencyEnabled = false;
        public static float TransparencyAmount = 0.5f;
        public static float ColorR = 1f;
        public static float ColorG = 0f;
        public static float ColorB = 1f;
        public static float EmissionIntensity = 4f;
        public static float RainbowSpeed = 0.25f;

        // Gradient mode
        public static bool GradientEnabled = false;
        public static float GradientSpeed = 0.5f;
        public static float GradientSpread = 1f;
        public static float Color2R = 0f;
        public static float Color2G = 1f;
        public static float Color2B = 0f;

        // ── Shader Library ──
        public static bool ShaderLibraryEnabled = false;
        public static bool AutoApplyShader = false;
        public static int SelectedShaderIndex = 0;
        private static List<Shader> _availableShaders = new List<Shader>();
        private static List<string> _shaderNames = new List<string>();
        private static Dictionary<int, Shader> _origShaders = new Dictionary<int, Shader>();
        private static bool _shadersDirty = true;
        private static int _lastLeftGunId = 0;
        private static int _lastRightGunId = 0;

        // ── Shader Search ──
        public static string ShaderSearchQuery = "";
        private static List<int> _filteredIndices = new List<int>();
        private static int _filteredCursor = 0;
        private static string _lastSearchQuery = null;

        // ── Shader Favorites ──
        private static List<string> _favoriteShaderNames = new List<string>();
        public static bool ShowFavoritesOnly = false;
        public static int FavoriteCount => _favoriteShaderNames.Count;
        public static List<string> FavoriteShaderNames => _favoriteShaderNames;

        public static string SelectedShaderName => _shaderNames.Count > 0 && SelectedShaderIndex >= 0 && SelectedShaderIndex < _shaderNames.Count
            ? _shaderNames[SelectedShaderIndex] : "None";
        public static int ShaderCount => _shaderNames.Count;
        public static List<string> ShaderNames => _shaderNames;

        public static bool IsCurrentShaderFavorited()
        {
            if (SelectedShaderIndex < 0 || SelectedShaderIndex >= _shaderNames.Count) return false;
            return _favoriteShaderNames.Contains(_shaderNames[SelectedShaderIndex]);
        }

        public static void ToggleFavoriteCurrent()
        {
            if (SelectedShaderIndex < 0 || SelectedShaderIndex >= _shaderNames.Count) return;
            string name = _shaderNames[SelectedShaderIndex];
            if (_favoriteShaderNames.Contains(name))
                _favoriteShaderNames.Remove(name);
            else
                _favoriteShaderNames.Add(name);
            _lastSearchQuery = null; // force re-filter
        }

        public static void SetFavorites(List<string> favorites)
        {
            _favoriteShaderNames = favorites ?? new List<string>();
            _lastSearchQuery = null;
        }

        // ── Shader Pallet Metadata ──
        public struct ShaderMeta
        {
            public string PalletName;
            public string AuthorName;
        }
        private static Dictionary<string, ShaderMeta> _shaderMeta = new Dictionary<string, ShaderMeta>();
        public static bool IsScanningPallets = false;
        private static int _scanRemaining = 0;
        private static int _scanTotal = 0;
        public static string ScanProgress => IsScanningPallets ? $"Scanning... ({_scanTotal - _scanRemaining}/{_scanTotal})" : "";

        public static int FilteredCount => _filteredIndices.Count;
        public static int FilteredCursor => _filteredCursor;

        public static string FilteredShaderName
        {
            get
            {
                RebuildFilterIfNeeded();
                if (_filteredIndices.Count == 0) return "None";
                int realIdx = _filteredIndices[_filteredCursor];
                return _shaderNames[realIdx];
            }
        }

        public static string FilteredShaderPalletInfo
        {
            get
            {
                RebuildFilterIfNeeded();
                if (_filteredIndices.Count == 0) return "";
                int realIdx = _filteredIndices[_filteredCursor];
                return GetShaderPalletInfo(_shaderNames[realIdx]);
            }
        }

        public static string GetShaderPalletInfo(string shaderName)
        {
            if (_shaderMeta.TryGetValue(shaderName, out var meta))
            {
                if (!string.IsNullOrEmpty(meta.PalletName))
                {
                    string info = meta.PalletName;
                    if (!string.IsNullOrEmpty(meta.AuthorName))
                        info += " by " + meta.AuthorName;
                    return info;
                }
            }
            return "";
        }

        public static void RebuildFilterIfNeeded()
        {
            string q = (ShaderSearchQuery ?? "").Trim().ToLower();
            bool favOnly = ShowFavoritesOnly;
            // Rebuild if query or fav-mode changed
            if (q == (_lastSearchQuery ?? "\x01") && !_shadersDirty) return;
            _lastSearchQuery = q;

            _filteredIndices.Clear();
            for (int i = 0; i < _shaderNames.Count; i++)
            {
                if (favOnly && !_favoriteShaderNames.Contains(_shaderNames[i])) continue;
                if (q.Length > 0 && !_shaderNames[i].ToLower().Contains(q)) continue;
                _filteredIndices.Add(i);
            }
            if (_filteredCursor >= _filteredIndices.Count)
                _filteredCursor = _filteredIndices.Count > 0 ? 0 : 0;
        }

        public static void NextShader()
        {
            RebuildFilterIfNeeded();
            if (_filteredIndices.Count == 0) return;
            _filteredCursor = (_filteredCursor + 1) % _filteredIndices.Count;
            SelectedShaderIndex = _filteredIndices[_filteredCursor];
        }

        public static void PrevShader()
        {
            RebuildFilterIfNeeded();
            if (_filteredIndices.Count == 0) return;
            _filteredCursor = (_filteredCursor - 1 + _filteredIndices.Count) % _filteredIndices.Count;
            SelectedShaderIndex = _filteredIndices[_filteredCursor];
        }

        public static void RefreshShaderList()
        {
            _availableShaders.Clear();
            _shaderNames.Clear();
            try
            {
                var allShaders = Resources.FindObjectsOfTypeAll<Shader>();
                foreach (Shader s in allShaders)
                {
                    if (s == null) continue;
                    string n = s.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    // Filter out shaders unsuitable for mesh rendering
                    if (n.StartsWith("Hidden/") || n.StartsWith("GUI/") || n.StartsWith("UI/") ||
                        n.Contains("Internal") || n.Contains("Sprite") || n.Contains("TextMesh"))
                        continue;
                    _availableShaders.Add(s);
                    _shaderNames.Add(n);
                }
                // Sort alphabetically
                var sorted = _shaderNames.Select((name, idx) => (name, idx)).OrderBy(x => x.name).ToList();
                _shaderNames = sorted.Select(x => x.name).ToList();
                var sortedShaders = sorted.Select(x => _availableShaders[x.idx]).ToList();
                _availableShaders = sortedShaders;
            }
            catch { }
            _shadersDirty = false;
            _lastSearchQuery = null; // force re-filter
            if (SelectedShaderIndex >= _shaderNames.Count)
                SelectedShaderIndex = 0;
        }

        /// <summary>
        /// Scan ALL mod pallets and load their crate assets to discover every shader.
        /// Also builds pallet/author metadata for each shader found.
        /// </summary>
        public static void ScanPalletShaders()
        {
            if (IsScanningPallets) return;
            try
            {
                var warehouse = AssetWarehouse.Instance;
                if (warehouse == null) return;

                var pallets = warehouse.GetPallets();
                if (pallets == null) return;

                IsScanningPallets = true;
                _scanRemaining = 0;
                _scanTotal = 0;

                for (int p = 0; p < pallets.Count; p++)
                {
                    var pallet = pallets[p];
                    if (pallet == null) continue;

                    string palletName = "";
                    string palletAuthor = "";
                    try
                    {
                        palletName = pallet.name ?? "Unknown";
                        palletAuthor = pallet.Author ?? "";
                    }
                    catch { continue; }

                    Il2CppSystem.Collections.Generic.List<Crate> crates = null;
                    try { crates = pallet.Crates; } catch { continue; }
                    if (crates == null) continue;

                    for (int c = 0; c < crates.Count; c++)
                    {
                        var crate = crates[c];
                        if (crate == null) continue;

                        _scanTotal++;
                        _scanRemaining++;

                        string pn = palletName;
                        string pa = palletAuthor;

                        try
                        {
                            System.Action<UnityEngine.Object> managedCb = (UnityEngine.Object obj) =>
                            {
                                _scanRemaining--;
                                try { ProcessLoadedAssetForShaders(obj, pn, pa); } catch { }
                                if (_scanRemaining <= 0 && IsScanningPallets)
                                {
                                    IsScanningPallets = false;
                                    RefreshShaderList();
                                    Main.MelonLog.Msg($"[Shader Scan] Complete. {_shaderMeta.Count} shader sources, {_shaderNames.Count} total shaders.");
                                }
                            };
                            crate.LoadAsset((Il2CppSystem.Action<UnityEngine.Object>)managedCb);
                        }
                        catch
                        {
                            _scanRemaining--;
                            if (_scanRemaining <= 0 && IsScanningPallets)
                            {
                                IsScanningPallets = false;
                                RefreshShaderList();
                            }
                        }
                    }
                }

                if (_scanTotal == 0)
                    IsScanningPallets = false;
                else
                    Main.MelonLog.Msg($"[Shader Scan] Scanning {_scanTotal} crates from {pallets.Count} pallets...");
            }
            catch
            {
                IsScanningPallets = false;
            }
        }

        private static void ProcessLoadedAssetForShaders(UnityEngine.Object obj, string palletName, string authorName)
        {
            if (obj == null) return;
            var go = obj.TryCast<GameObject>();
            if (go == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                try
                {
                    foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null) continue;
                        string sn = mat.shader.name;
                        if (string.IsNullOrEmpty(sn)) continue;

                        if (!_shaderMeta.ContainsKey(sn))
                        {
                            _shaderMeta[sn] = new ShaderMeta { PalletName = palletName, AuthorName = authorName };
                        }
                    }
                }
                catch { }
            }
        }

        public static void ApplyShaderToGun()
        {
            if (_availableShaders.Count == 0 || SelectedShaderIndex < 0 || SelectedShaderIndex >= _availableShaders.Count) return;
            Shader targetShader = _availableShaders[SelectedShaderIndex];
            try
            {
                ApplyShaderToHand(Player.LeftHand, targetShader);
                ApplyShaderToHand(Player.RightHand, targetShader);
            }
            catch { }
        }

        private static void ApplyShaderToHand(Hand hand, Shader shader)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            ApplyShaderToRenderers(((Component)gun).GetComponentsInChildren<Renderer>(), shader);
        }

        private static void ApplyShaderToRenderers(Il2CppArrayBase<Renderer> renderers, Shader shader)
        {
            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                // Skip grip highlight gizmos (the faint white circles on grab points)
                string goName = r.gameObject.name.ToLower();
                if (goName.Contains("grip") || goName.Contains("gizmo") || goName.Contains("grab") ||
                    goName.Contains("highlight") || goName.Contains("forcepull")) continue;
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string sn = mat.shader != null ? mat.shader.name : "";
                    if (sn.Contains("Scope") || sn.Contains("Lens") || sn.Contains("Reticle") ||
                        sn.Contains("Holographic") || sn.Contains("Stencil")) continue;
                    // Skip grip/gizmo materials by name
                    string mn = mat.name.ToLower();
                    if (mn.Contains("lens") || mn.Contains("reticle") || mn.Contains("scope_glass")) continue;
                    if (mn.Contains("grip") || mn.Contains("gizmo") || mn.Contains("highlight") ||
                        mn.Contains("forcepull") || mn.Contains("grab_point")) continue;
                    int id = ((Object)mat).GetInstanceID();
                    if (!_origShaders.ContainsKey(id))
                        _origShaders[id] = mat.shader;
                    mat.shader = shader;
                }
            }
        }

        private static void AutoApplyShaderToHand(Hand hand, ref int lastGunId)
        {
            if (hand == null) { lastGunId = 0; return; }
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) { lastGunId = 0; return; }
            int gunId = ((Object)gun).GetInstanceID();
            if (gunId != lastGunId)
            {
                lastGunId = gunId;
                Shader targetShader = _availableShaders[SelectedShaderIndex];
                ApplyShaderToHand(hand, targetShader);
            }
        }

        public static void RevertShaders()
        {
            try
            {
                RevertShadersOnHand(Player.LeftHand);
                RevertShadersOnHand(Player.RightHand);
            }
            catch { }
            _origShaders.Clear();
        }

        private static void RevertShadersOnHand(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Renderer r in ((Component)gun).GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.sharedMaterials)
                {
                    if (mat == null) continue;
                    int id = ((Object)mat).GetInstanceID();
                    if (_origShaders.TryGetValue(id, out var orig))
                        mat.shader = orig;
                }
            }
        }

        // ── Texture Editor ──
        public static int TextureMode = 0; // 0=Original, 1=Solid, 2=Gradient, 3=Noise
        public static float TexGradR2 = 0f;
        public static float TexGradG2 = 1f;
        public static float TexGradB2 = 0f;
        public static float TexNoiseScale = 5f;
        public static float TexScrollSpeed = 0f;
        private static Texture2D _generatedTex = null;
        private static int _lastTexMode = -1;
        private static float _lastTexR = -1, _lastTexG = -1, _lastTexB = -1;
        private static float _lastTexR2 = -1, _lastTexG2 = -1, _lastTexB2 = -1;
        private static float _lastTexNoise = -1;
        public static readonly string[] TextureModeNames = { "Original", "Solid", "Gradient", "Noise" };

        public static string TextureModeName => TextureModeNames[Mathf.Clamp(TextureMode, 0, TextureModeNames.Length - 1)];

        public static void ApplyTextureToGun()
        {
            if (TextureMode == 0)
            {
                // Restore original textures
                RestoreTextures();
                return;
            }
            RegenerateTexture();
            if (_generatedTex == null) return;
            try
            {
                ApplyTexToHand(Player.LeftHand, _generatedTex);
                ApplyTexToHand(Player.RightHand, _generatedTex);
            }
            catch { }
        }

        public static void RegenerateTexture()
        {
            bool paramsChanged = TextureMode != _lastTexMode ||
                ColorR != _lastTexR || ColorG != _lastTexG || ColorB != _lastTexB ||
                TexGradR2 != _lastTexR2 || TexGradG2 != _lastTexG2 || TexGradB2 != _lastTexB2 ||
                TexNoiseScale != _lastTexNoise;
            if (!paramsChanged && _generatedTex != null) return;

            int size = 256;
            if (_generatedTex == null)
                _generatedTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _generatedTex.wrapMode = TextureWrapMode.Repeat;
            _generatedTex.filterMode = FilterMode.Bilinear;

            Color c1 = new Color(ColorR, ColorG, ColorB, 1f);
            Color c2 = new Color(TexGradR2, TexGradG2, TexGradB2, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = (float)x / (size - 1);
                    Color pixel;
                    switch (TextureMode)
                    {
                        case 1: // Solid
                            pixel = c1;
                            break;
                        case 2: // Gradient
                            pixel = Color.Lerp(c1, c2, t);
                            break;
                        case 3: // Noise pattern
                            float nx = x * TexNoiseScale / size;
                            float ny = y * TexNoiseScale / size;
                            float noise = Mathf.PerlinNoise(nx, ny);
                            pixel = Color.Lerp(c1, c2, noise);
                            break;
                        default:
                            pixel = c1;
                            break;
                    }
                    _generatedTex.SetPixel(x, y, pixel);
                }
            }
            _generatedTex.Apply();

            _lastTexMode = TextureMode;
            _lastTexR = ColorR; _lastTexG = ColorG; _lastTexB = ColorB;
            _lastTexR2 = TexGradR2; _lastTexG2 = TexGradG2; _lastTexB2 = TexGradB2;
            _lastTexNoise = TexNoiseScale;
        }

        private static void ApplyTexToHand(Hand hand, Texture2D tex)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Renderer r in ((Component)gun).GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.materials)
                {
                    string sn = mat.shader != null ? mat.shader.name : "";
                    if (sn.Contains("Scope") || sn.Contains("Lens") || sn.Contains("Reticle") ||
                        sn.Contains("Holographic") || sn.Contains("Stencil")) continue;
                    string mn = mat.name.ToLower();
                    if (mn.Contains("lens") || mn.Contains("reticle") || mn.Contains("scope_glass")) continue;
                    int id = ((Object)mat).GetInstanceID();
                    CacheMatOriginal(mat, id);
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", tex);
                }
            }
        }

        public static void RestoreTextures()
        {
            try
            {
                RestoreTexOnHand(Player.LeftHand);
                RestoreTexOnHand(Player.RightHand);
            }
            catch { }
        }

        private static void RestoreTexOnHand(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Renderer r in ((Component)gun).GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.materials)
                {
                    int id = ((Object)mat).GetInstanceID();
                    if (!_origMatData.TryGetValue(id, out var orig)) continue;
                    if (orig.hasBaseMap) { mat.SetTexture("_BaseMap", orig.baseMap); mat.SetTextureOffset("_BaseMap", orig.baseMapOffset); }
                    if (orig.hasMainTex) { mat.SetTexture("_MainTex", orig.mainTex); mat.SetTextureOffset("_MainTex", orig.mainTexOffset); }
                }
            }
        }

        public static void UpdateTextureScroll()
        {
            if (TextureMode == 0 || TexScrollSpeed <= 0f) return;
            try
            {
                ScrollTexOnHand(Player.LeftHand);
                ScrollTexOnHand(Player.RightHand);
            }
            catch { }
        }

        private static void ScrollTexOnHand(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            float offset = Time.time * TexScrollSpeed;
            Vector2 off = new Vector2(offset, 0f);
            foreach (Renderer r in ((Component)gun).GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)r.materials)
                {
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTextureOffset("_BaseMap", off);
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTextureOffset("_MainTex", off);
                }
            }
        }

        // Backward-compatible alias
        public static bool PurpleGuns
        {
            get => CustomGunColorEnabled;
            set => CustomGunColorEnabled = value;
        }

        // Original material state cache (keyed by material instance ID)
        private struct MatOriginal
        {
            public Color color;
            public Color baseColor;
            public float metallic;
            public float smoothness;
            public bool emissionKeyword;
            public Color emissionColor;
            public MaterialGlobalIlluminationFlags giFlags;
            public Texture emissionMap;
            public Texture baseMap;
            public Texture mainTex;
            public Texture metallicGlossMap;
            public Texture bumpMap;
            public Texture occlusionMap;
            public float surface;
            public float srcBlend;
            public float dstBlend;
            public float zWrite;
            public int renderQueue;
            public bool hasBaseColor;
            public bool hasBaseMap;
            public bool hasMainTex;
            public bool hasMetallicGlossMap;
            public bool hasBumpMap;
            public bool hasOcclusionMap;
            public bool hasSurface;
            public bool hasSrcBlend;
            public bool hasDstBlend;
            public bool hasZWrite;
            public Vector2 baseMapOffset;
            public Vector2 mainTexOffset;
        }
        private static Dictionary<int, MatOriginal> _origMatData = new Dictionary<int, MatOriginal>();

        // Sub-feature toggles (use properties so we can restore on disable)
        public static bool NoRecoil = false;
        public static bool NoReload = false;

        private static bool _insaneDamage = false;
        public static bool InsaneDamage
        {
            get => _insaneDamage;
            set
            {
                if (_insaneDamage && !value) RestoreDamage();
                _insaneDamage = value;
            }
        }

        private static bool _insaneFirerate = false;
        public static bool InsaneFirerate
        {
            get => _insaneFirerate;
            set
            {
                if (_insaneFirerate && !value) RestoreFirerate();
                _insaneFirerate = value;
            }
        }

        private static bool _noWeight = false;
        public static bool NoWeight
        {
            get => _noWeight;
            set
            {
                if (_noWeight && !value) RestoreWeight();
                _noWeight = value;
            }
        }

        private static bool _gunsBounce = false;
        public static bool GunsBounce
        {
            get => _gunsBounce;
            set
            {
                if (_gunsBounce && !value) RestoreBounce();
                _gunsBounce = value;
            }
        }

        // Original value caches (keyed by instance ID)
        private static Dictionary<int, (CartridgeData data, float orig)> _origDamageMultiplier = new Dictionary<int, (CartridgeData, float)>();
        private static Dictionary<int, (Gun.FireMode mode, float rps)> _origFirerate = new Dictionary<int, (Gun.FireMode, float)>();
        private static Dictionary<int, (float mass, float drag, float angDrag)> _origRbData = new Dictionary<int, (float, float, float)>();
        private static Dictionary<int, (float bounciness, PhysicMaterialCombine bounceCombine, float staticFriction, PhysicMaterialCombine frictionCombine)> _origColliderData =
            new Dictionary<int, (float, PhysicMaterialCombine, float, PhysicMaterialCombine)>();

        // Track which guns have already had glow applied (avoid re-applying every frame)
        private static HashSet<int> _glowedGuns = new HashSet<int>();

        // Cache renderers per gun instance to avoid per-frame GetComponentsInChildren GC alloc
        private static Dictionary<int, Il2CppArrayBase<Renderer>> _cachedRenderers = new Dictionary<int, Il2CppArrayBase<Renderer>>();

        // Rainbow hue state for glow guns (cycles 0→1 over time)
        private static float _glowHue = 0f;

        /// <summary>
        /// Per-frame fallback that applies effects to held guns.
        /// Harmony patches on IL2CPP types can fail silently, so this ensures effects work.
        /// </summary>
        public static void Update()
        {
            bool anyFeature = CustomGunColorEnabled || InsaneDamage || NoRecoil || InsaneFirerate || NoWeight || GunsBounce || NoReload;
            bool texScroll = TextureMode > 0 && TexScrollSpeed > 0f;

            // Auto-apply shader: detect new gun in hand and apply selected shader
            if (AutoApplyShader && ShaderLibraryEnabled && _availableShaders.Count > 0 && SelectedShaderIndex >= 0 && SelectedShaderIndex < _availableShaders.Count)
            {
                try { AutoApplyShaderToHand(Player.LeftHand, ref _lastLeftGunId); } catch { }
                try { AutoApplyShaderToHand(Player.RightHand, ref _lastRightGunId); } catch { }
            }

            if (!anyFeature && !texScroll) return;

            // Advance rainbow hue each frame
            if (CustomGunColorEnabled && RainbowEnabled)
                _glowHue = (_glowHue + RainbowSpeed * Time.deltaTime) % 1f;

            // Texture UV scrolling
            if (texScroll) UpdateTextureScroll();

            if (!anyFeature) return;
            try
            {
                ProcessHeldGun(Player.LeftHand);
                ProcessHeldGun(Player.RightHand);
            }
            catch { }
        }

        private static void ProcessHeldGun(Hand hand)
        {
            if (hand == null) return;
            try
            {
                Gun gun = Player.GetComponentInHand<Gun>(hand);
                if (gun == null) return;

                if (InsaneDamage) ApplyDamage(gun);
                if (NoReload) ApplyNoReload(gun);
                if (InsaneFirerate) ApplyFirerate(gun);
                if (NoWeight) ApplyNoWeight(gun);
                if (GunsBounce) ApplyBounce(gun);

                if (CustomGunColorEnabled)
                {
                    // Don't cache renderers — magazine insertions change the child hierarchy
                    var renderers = ((Component)gun).GetComponentsInChildren<Renderer>();
                    ApplyCustomColor(renderers, ((Component)gun).transform);
                }
            }
            catch { }
        }

        /// <summary>
        /// Clear tracked state on level unload.
        /// </summary>
        public static void OnLevelUnloaded()
        {
            _glowedGuns.Clear();
            _cachedRenderers.Clear();
            _origDamageMultiplier.Clear();
            _origFirerate.Clear();
            _origRbData.Clear();
            _origColliderData.Clear();
            _origMatData.Clear();
            _origShaders.Clear();
            _shadersDirty = true;
            _lastLeftGunId = 0;
            _lastRightGunId = 0;
        }

        // ── Harmony Patches (supplementary — may not fire on all IL2CPP builds) ──

        [HarmonyPatch(typeof(Gun), "Fire")]
        [HarmonyPostfix]
        public static void OnFire(Gun __instance)
        {
            ApplyDamage(__instance);
            ApplyNoReload(__instance);
            RecoilRagdollController.OnGunFired(__instance);
        }

        [HarmonyPatch(typeof(Gun), "OnTriggerGripAttached")]
        [HarmonyPostfix]
        public static void OnGrip(Gun __instance)
        {
            ApplyBounce(__instance);
            ApplyNoWeight(__instance);
            ApplyFirerate(__instance);
            ApplyPurple(__instance);
        }

        [HarmonyPatch(typeof(Rigidbody), "AddForceAtPosition", new System.Type[]
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(ForceMode)
        })]
        [HarmonyPrefix]
        public static bool AddForceAtPositionPrefix(Rigidbody __instance, ref Vector3 force)
        {
            if (!NoRecoil)
                return true;
            if ((Object)(object)((Component)__instance).GetComponentInParent<Gun>() != (Object)null)
                force = Vector3.zero;
            return true;
        }

        [HarmonyPatch(typeof(Magazine), "OnGrab")]
        [HarmonyPostfix]
        public static void OnMagGrab(Magazine __instance)
        {
            ApplyBounceMag(__instance);
            ApplyPurpleMag(__instance);
            ApplyNoWeightMag(__instance);
            ApplyShaderToMag(__instance);
        }

        [HarmonyPatch(typeof(Gun), "OnMagazineInserted")]
        [HarmonyPostfix]
        public static void OnMagInserted(Gun __instance)
        {
            if (CustomGunColorEnabled)
            {
                try
                {
                    ApplyCustomColor(((Component)__instance).GetComponentsInChildren<Renderer>(), ((Component)__instance).transform);
                }
                catch { }
            }
            // Re-trigger shader on magazine insertion (whenever shader library is active)
            if (ShaderLibraryEnabled && _availableShaders.Count > 0 && SelectedShaderIndex >= 0 && SelectedShaderIndex < _availableShaders.Count)
            {
                try
                {
                    Shader targetShader = _availableShaders[SelectedShaderIndex];
                    ApplyShaderToRenderers(((Component)__instance).GetComponentsInChildren<Renderer>(), targetShader);
                }
                catch { }
            }
        }

        // ── Feature Implementations ──

        private static void ApplyDamage(Gun gun)
        {
            if (!InsaneDamage) return;
            MagazineState magState = gun._magState;
            if (magState == null) return;
            CartridgeData cartridgeData = magState.cartridgeData;
            if ((Object)(object)cartridgeData == (Object)null) return;
            if (cartridgeData.projectile == null) return;
            int id = ((Object)cartridgeData).GetInstanceID();
            if (!_origDamageMultiplier.ContainsKey(id))
                _origDamageMultiplier[id] = (cartridgeData, cartridgeData.projectile.damageMultiplier);
            cartridgeData.projectile.damageMultiplier = float.MaxValue;
        }

        private static void RestoreDamage()
        {
            foreach (var kv in _origDamageMultiplier)
            {
                try
                {
                    var cd = kv.Value.data;
                    if (cd != null && cd.projectile != null)
                        cd.projectile.damageMultiplier = kv.Value.orig;
                }
                catch { }
            }
            _origDamageMultiplier.Clear();
        }

        private static void RestoreDamageOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            MagazineState magState = gun._magState;
            if (magState?.cartridgeData?.projectile == null) return;
            int id = ((Object)magState.cartridgeData).GetInstanceID();
            if (_origDamageMultiplier.TryGetValue(id, out var entry))
                magState.cartridgeData.projectile.damageMultiplier = entry.orig;
        }

        private static void ApplyNoReload(Gun gun)
        {
            if (!NoReload) return;
            MagazineState magState = gun._magState;
            if (magState != null)
                magState.Refill();
        }

        private static void ApplyFirerate(Gun gun)
        {
            if (!InsaneFirerate) return;
            int id = ((Object)gun).GetInstanceID();
            if (!_origFirerate.ContainsKey(id))
                _origFirerate[id] = (gun.fireMode, gun.roundsPerSecond);
            gun.fireMode = (Gun.FireMode)2;
            gun.roundsPerSecond = float.MaxValue;
        }

        private static void RestoreFirerate()
        {
            try
            {
                RestoreFirerateOnHeld(Player.LeftHand);
                RestoreFirerateOnHeld(Player.RightHand);
            }
            catch { }
            _origFirerate.Clear();
        }

        private static void RestoreFirerateOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            int id = ((Object)gun).GetInstanceID();
            if (_origFirerate.TryGetValue(id, out var orig))
            {
                gun.fireMode = orig.mode;
                gun.roundsPerSecond = orig.rps;
            }
        }

        private static void ApplyNoWeight(Gun gun)
        {
            if (!NoWeight) return;
            foreach (Rigidbody rb in ((Component)gun).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (!_origRbData.ContainsKey(id))
                    _origRbData[id] = (rb.mass, rb.drag, rb.angularDrag);
                rb.mass = 0.1f;
                rb.drag = 0f;
                rb.angularDrag = 0f;
            }
        }

        private static void RestoreWeight()
        {
            try
            {
                RestoreWeightOnHeld(Player.LeftHand);
                RestoreWeightOnHeld(Player.RightHand);
            }
            catch { }
            _origRbData.Clear();
        }

        private static void RestoreWeightOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Rigidbody rb in ((Component)gun).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (_origRbData.TryGetValue(id, out var orig))
                {
                    rb.mass = orig.mass;
                    rb.drag = orig.drag;
                    rb.angularDrag = orig.angDrag;
                }
            }
        }

        private static void ApplyBounce(Gun gun)
        {
            if (!GunsBounce) return;
            foreach (Collider col in ((Component)gun).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (!_origColliderData.ContainsKey(id))
                    _origColliderData[id] = (col.material.bounciness, col.material.bounceCombine, col.material.staticFriction, col.material.frictionCombine);
                col.material.bounciness = 1f;
                col.material.bounceCombine = (PhysicMaterialCombine)3;
                col.material.staticFriction = 0f;
                col.material.frictionCombine = (PhysicMaterialCombine)2;
            }
        }

        private static void RestoreBounce()
        {
            try
            {
                RestoreBounceOnHeld(Player.LeftHand);
                RestoreBounceOnHeld(Player.RightHand);
            }
            catch { }
            _origColliderData.Clear();
        }

        private static void RestoreBounceOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Collider col in ((Component)gun).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (_origColliderData.TryGetValue(id, out var orig))
                {
                    col.material.bounciness = orig.bounciness;
                    col.material.bounceCombine = orig.bounceCombine;
                    col.material.staticFriction = orig.staticFriction;
                    col.material.frictionCombine = orig.frictionCombine;
                }
            }
        }

        private static void ApplyPurple(Gun gun)
        {
            if (!CustomGunColorEnabled) return;
            ApplyCustomColor(((Component)gun).GetComponentsInChildren<Renderer>(), ((Component)gun).transform);
        }

        private static void CacheMatOriginal(Material mat, int id)
        {
            if (_origMatData.ContainsKey(id)) return;
            var orig = new MatOriginal
            {
                color = mat.color,
                metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f,
                smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f,
                emissionKeyword = mat.IsKeywordEnabled("_EMISSION"),
                emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
                giFlags = mat.globalIlluminationFlags,
                emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null,
                renderQueue = mat.renderQueue,
                hasBaseColor = mat.HasProperty("_BaseColor"),
                hasSurface = mat.HasProperty("_Surface"),
                hasSrcBlend = mat.HasProperty("_SrcBlend"),
                hasDstBlend = mat.HasProperty("_DstBlend"),
                hasZWrite = mat.HasProperty("_ZWrite")
            };
            orig.hasBaseMap = mat.HasProperty("_BaseMap");
            orig.hasMainTex = mat.HasProperty("_MainTex");
            orig.hasMetallicGlossMap = mat.HasProperty("_MetallicGlossMap");
            orig.hasBumpMap = mat.HasProperty("_BumpMap");
            orig.hasOcclusionMap = mat.HasProperty("_OcclusionMap");
            if (orig.hasBaseColor) orig.baseColor = mat.GetColor("_BaseColor");
            if (orig.hasBaseMap) orig.baseMap = mat.GetTexture("_BaseMap");
            if (orig.hasMainTex) orig.mainTex = mat.GetTexture("_MainTex");
            if (orig.hasMetallicGlossMap) orig.metallicGlossMap = mat.GetTexture("_MetallicGlossMap");
            if (orig.hasBumpMap) orig.bumpMap = mat.GetTexture("_BumpMap");
            if (orig.hasOcclusionMap) orig.occlusionMap = mat.GetTexture("_OcclusionMap");
            if (orig.hasBaseMap) orig.baseMapOffset = mat.GetTextureOffset("_BaseMap");
            if (orig.hasMainTex) orig.mainTexOffset = mat.GetTextureOffset("_MainTex");
            if (orig.hasSurface) orig.surface = mat.GetFloat("_Surface");
            if (orig.hasSrcBlend) orig.srcBlend = mat.GetFloat("_SrcBlend");
            if (orig.hasDstBlend) orig.dstBlend = mat.GetFloat("_DstBlend");
            if (orig.hasZWrite) orig.zWrite = mat.GetFloat("_ZWrite");
            _origMatData[id] = orig;
        }

        private static void RestoreCustomColor()
        {
            try
            {
                RestoreCustomColorOnHeld(Player.LeftHand);
                RestoreCustomColorOnHeld(Player.RightHand);
            }
            catch { }
            _origMatData.Clear();
        }

        private static void RestoreCustomColorOnHeld(Hand hand)
        {
            if (hand == null) return;
            Gun gun = Player.GetComponentInHand<Gun>(hand);
            if (gun == null) return;
            foreach (Renderer renderer in ((Component)gun).GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in (Il2CppArrayBase<Material>)(object)renderer.materials)
                {
                    int id = ((Object)mat).GetInstanceID();
                    if (!_origMatData.TryGetValue(id, out var orig)) continue;

                    mat.color = orig.color;
                    if (orig.hasBaseColor) mat.SetColor("_BaseColor", orig.baseColor);
                    if (orig.hasBaseMap) mat.SetTexture("_BaseMap", orig.baseMap);
                    if (orig.hasMainTex) mat.SetTexture("_MainTex", orig.mainTex);
                    if (orig.hasMetallicGlossMap) mat.SetTexture("_MetallicGlossMap", orig.metallicGlossMap);
                    if (orig.hasBumpMap) mat.SetTexture("_BumpMap", orig.bumpMap);
                    if (orig.hasOcclusionMap) mat.SetTexture("_OcclusionMap", orig.occlusionMap);
                    mat.SetFloat("_Metallic", orig.metallic);
                    mat.SetFloat("_Smoothness", orig.smoothness);

                    // Restore emission state
                    if (orig.emissionKeyword)
                        mat.EnableKeyword("_EMISSION");
                    else
                        mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", orig.emissionColor);
                    mat.globalIlluminationFlags = orig.giFlags;
                    if (mat.HasProperty("_EmissionMap"))
                        mat.SetTexture("_EmissionMap", orig.emissionMap);

                    // Restore transparency state
                    if (orig.hasSurface) mat.SetFloat("_Surface", orig.surface);
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    if (orig.hasSrcBlend) mat.SetFloat("_SrcBlend", orig.srcBlend);
                    if (orig.hasDstBlend) mat.SetFloat("_DstBlend", orig.dstBlend);
                    if (orig.hasZWrite) mat.SetFloat("_ZWrite", orig.zWrite);
                    mat.renderQueue = orig.renderQueue;
                }
            }
        }

        private static void ApplyCustomColor(Il2CppArrayBase<Renderer> renderers, Transform root)
        {
            // ── Pre-compute gradient axis if enabled ──
            float minProj = float.MaxValue, maxProj = float.MinValue;
            Vector3 rootPos = Vector3.zero, rootFwd = Vector3.forward;
            if (GradientEnabled && root != null)
            {
                rootPos = root.position;
                rootFwd = root.forward;
                foreach (Renderer r in renderers)
                {
                    float p = Vector3.Dot(r.transform.position - rootPos, rootFwd);
                    if (p < minProj) minProj = p;
                    if (p > maxProj) maxProj = p;
                }
            }
            float projRange = maxProj - minProj;
            float gradAnimOffset = Time.time * GradientSpeed;

            // Determine base color: rainbow cycle or static RGB
            Color baseColor;
            if (RainbowEnabled)
                baseColor = Color.HSVToRGB(_glowHue, 1f, 1f);
            else
                baseColor = new Color(ColorR, ColorG, ColorB, 1f);

            // Apply transparency alpha
            if (TransparencyEnabled)
                baseColor.a = 1f - TransparencyAmount;

            foreach (Renderer renderer in renderers)
            {
                // ── Per-renderer gradient color ──
                Color rendColor = baseColor;
                if (GradientEnabled && root != null && projRange > 0.001f)
                {
                    float proj = Vector3.Dot(renderer.transform.position - rootPos, rootFwd);
                    float t = (proj - minProj) / projRange;
                    if (RainbowEnabled)
                    {
                        // Rainbow wave: offset hue by position
                        rendColor = Color.HSVToRGB((_glowHue + t * GradientSpread) % 1f, 1f, 1f);
                    }
                    else
                    {
                        // Custom two-color gradient with flow animation
                        float animT = (t * GradientSpread + gradAnimOffset) % 1f;
                        Color colorA = new Color(ColorR, ColorG, ColorB, 1f);
                        Color colorB = new Color(Color2R, Color2G, Color2B, 1f);
                        rendColor = Color.Lerp(colorA, colorB, Mathf.PingPong(animT * 2f, 1f));
                    }
                    if (TransparencyEnabled)
                        rendColor.a = 1f - TransparencyAmount;
                }

                // Particle/trail renderers: only tint color, don't strip textures or change material props
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                {
                    foreach (Material mat in (Il2CppArrayBase<Material>)(object)renderer.materials)
                    {
                        mat.color = rendColor;
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", rendColor);
                        if (mat.HasProperty("_TintColor"))
                            mat.SetColor("_TintColor", rendColor);
                    }
                    continue;
                }

                foreach (Material mat in (Il2CppArrayBase<Material>)(object)renderer.materials)
                {
                    // Skip materials with particle/sprite/additive shaders (VFX overlay)
                    string shaderName = mat.shader != null ? mat.shader.name : "";
                    if (shaderName.Contains("Particle") || shaderName.Contains("Sprite") ||
                        shaderName.Contains("Additive") || shaderName.Contains("Distortion"))
                    {
                        // Still tint the color
                        mat.color = rendColor;
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", rendColor);
                        if (mat.HasProperty("_TintColor"))
                            mat.SetColor("_TintColor", rendColor);
                        continue;
                    }
                    // Skip scope/lens/stencil shaders and pre-existing transparent materials
                    if (shaderName.Contains("Scope") || shaderName.Contains("Lens") ||
                        shaderName.Contains("Reticle") || shaderName.Contains("Holographic") ||
                        shaderName.Contains("Refract") || shaderName.Contains("Stencil"))
                        continue;
                    // Skip lens/reticle materials by name (modded guns use material names)
                    string matName = mat.name.ToLower();
                    if (matName.Contains("lens") || matName.Contains("reticle") ||
                        matName.Contains("sight_glass") || matName.Contains("optic_glass") ||
                        matName.Contains("scope_glass") || matName.Contains("holographic"))
                        continue;
                    // Skip pre-existing transparent materials UNLESS we previously modified them
                    if (!TransparencyEnabled && mat.renderQueue >= 2450)
                    {
                        int checkId = ((Object)mat).GetInstanceID();
                        if (!_origMatData.ContainsKey(checkId) || _origMatData[checkId].renderQueue >= 2450)
                            continue;
                        // Fall through — we set this renderQueue, need to restore it
                    }

                    int matId = ((Object)mat).GetInstanceID();
                    CacheMatOriginal(mat, matId);

                    // Check if shader was swapped by Shader Editor
                    bool shaderSwapped = _origShaders.ContainsKey(matId);

                    mat.color = rendColor;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", rendColor);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", rendColor);
                    // Strip albedo textures so flat color shows through — but ONLY if shader
                    // was NOT swapped (custom shaders rely on their textures for their visual effect)
                    if (!shaderSwapped)
                    {
                        if (mat.HasProperty("_BaseMap"))
                            mat.SetTexture("_BaseMap", null);
                        if (mat.HasProperty("_MainTex"))
                            mat.SetTexture("_MainTex", null);
                    }

                    // ── Reflection ──
                    if (ReflectionEnabled)
                    {
                        if (mat.HasProperty("_MetallicGlossMap"))
                            mat.SetTexture("_MetallicGlossMap", null);
                        if (mat.HasProperty("_BumpMap"))
                            mat.SetTexture("_BumpMap", null);
                        if (mat.HasProperty("_OcclusionMap"))
                            mat.SetTexture("_OcclusionMap", null);
                        mat.SetFloat("_Metallic", 1f);
                        mat.SetFloat("_Smoothness", 1f);
                    }
                    else if (_origMatData.TryGetValue(matId, out var origRef))
                    {
                        if (origRef.hasMetallicGlossMap) mat.SetTexture("_MetallicGlossMap", origRef.metallicGlossMap);
                        if (origRef.hasBumpMap) mat.SetTexture("_BumpMap", origRef.bumpMap);
                        if (origRef.hasOcclusionMap) mat.SetTexture("_OcclusionMap", origRef.occlusionMap);
                        mat.SetFloat("_Metallic", origRef.metallic);
                        mat.SetFloat("_Smoothness", origRef.smoothness);
                    }

                    // ── Emission ──
                    if (EmissionEnabled)
                    {
                        Color emission = rendColor * EmissionIntensity;
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", emission);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        if (mat.HasProperty("_EmissionMap"))
                            mat.SetTexture("_EmissionMap", null);
                    }
                    else if (_origMatData.TryGetValue(matId, out var origEm))
                    {
                        if (origEm.emissionKeyword)
                            mat.EnableKeyword("_EMISSION");
                        else
                            mat.DisableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", origEm.emissionColor);
                        mat.globalIlluminationFlags = origEm.giFlags;
                        if (mat.HasProperty("_EmissionMap"))
                            mat.SetTexture("_EmissionMap", origEm.emissionMap);
                    }

                    // ── Transparency ──
                    if (TransparencyEnabled)
                    {
                        if (mat.HasProperty("_Surface"))
                            mat.SetFloat("_Surface", 1f);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        if (mat.HasProperty("_SrcBlend"))
                            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend"))
                            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_ZWrite"))
                            mat.SetFloat("_ZWrite", 0f);
                        mat.renderQueue = 3000;
                    }
                    else if (_origMatData.TryGetValue(matId, out var origTr))
                    {
                        if (origTr.hasSurface) mat.SetFloat("_Surface", origTr.surface);
                        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        if (origTr.hasSrcBlend) mat.SetFloat("_SrcBlend", origTr.srcBlend);
                        if (origTr.hasDstBlend) mat.SetFloat("_DstBlend", origTr.dstBlend);
                        if (origTr.hasZWrite) mat.SetFloat("_ZWrite", origTr.zWrite);
                        mat.renderQueue = origTr.renderQueue;
                    }
                }
            }
        }

        // ── Magazine variants ──

        private static void ApplyBounceMag(Magazine mag)
        {
            if (!GunsBounce) return;
            foreach (Collider col in ((Component)mag).GetComponentsInChildren<Collider>())
            {
                if (col.material == null) continue;
                int id = ((Object)col).GetInstanceID();
                if (!_origColliderData.ContainsKey(id))
                    _origColliderData[id] = (col.material.bounciness, col.material.bounceCombine, col.material.staticFriction, col.material.frictionCombine);
                col.material.bounciness = 1f;
                col.material.bounceCombine = (PhysicMaterialCombine)3;
                col.material.staticFriction = 0f;
                col.material.frictionCombine = (PhysicMaterialCombine)2;
            }
        }

        private static void ApplyPurpleMag(Magazine mag)
        {
            if (!CustomGunColorEnabled) return;
            ApplyCustomColor(((Component)mag).GetComponentsInChildren<Renderer>(), ((Component)mag).transform);
        }

        private static void ApplyNoWeightMag(Magazine mag)
        {
            if (!NoWeight) return;
            foreach (Rigidbody rb in ((Component)mag).GetComponentsInChildren<Rigidbody>())
            {
                int id = ((Object)rb).GetInstanceID();
                if (!_origRbData.ContainsKey(id))
                    _origRbData[id] = (rb.mass, rb.drag, rb.angularDrag);
                rb.mass = 0.1f;
                rb.drag = 0f;
                rb.angularDrag = 0f;
            }
        }

        private static void ApplyShaderToMag(Magazine mag)
        {
            if (!ShaderLibraryEnabled || _availableShaders.Count == 0 || SelectedShaderIndex < 0 || SelectedShaderIndex >= _availableShaders.Count) return;
            Shader targetShader = _availableShaders[SelectedShaderIndex];
            try
            {
                ApplyShaderToRenderers(((Component)mag).GetComponentsInChildren<Renderer>(), targetShader);
            }
            catch { }
        }
    }
}
