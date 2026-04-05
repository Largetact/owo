using System;
using System.Collections.Generic;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.AI;
using Il2CppSLZ.Marrow.Pool;

namespace BonelabUtilityMod
{
    public enum ESPMode
    {
        OFF,
        TRACERS,
        BOX,
        SKELETON,
        ALL
    }

    public enum ESPColorMode
    {
        Distance,
        Rainbow,
        CustomRGB,
        Gradient
    }

    public enum ItemESPFilter
    {
        All,
        Guns,
        Melee,
        NPCs,
        Props
    }

    public static class ESPController
    {
        // ── Settings ──
        public static bool Enabled = false;
        public static ESPMode Mode = ESPMode.TRACERS;
        public static ESPColorMode ColorMode = ESPColorMode.Distance;
        public static float NearColor = 10f;     // distance for green
        public static float FarColor = 100f;     // distance for red
        public static float TracerWidth = 0.005f;
        public static float SkeletonWidth = 0.003f;
        public static float BoxPadding = 0.3f;   // world-space padding for box corners
        public static float CustomR = 1f, CustomG = 0f, CustomB = 0f;
        public static float GradientR2 = 0f, GradientG2 = 0f, GradientB2 = 1f;
        public static float RainbowSpeed = 1f;

        // ── Item ESP Settings ──
        public static bool ItemESPEnabled = false;
        public static ItemESPFilter ItemFilter = ItemESPFilter.All;
        public static float ItemMaxDistance = 200f;
        public static float ItemScanInterval = 0.5f;
        public static bool ItemShowLabels = true;
        public static float ItemColorR = 1f, ItemColorG = 0.6f, ItemColorB = 0f; // Orange default
        public static float ItemNpcR = 1f, ItemNpcG = 0.2f, ItemNpcB = 0.2f;     // Red for NPCs
        public static float ItemGunR = 0.2f, ItemGunG = 0.8f, ItemGunB = 1f;     // Cyan for guns
        public static float ItemMeleeR = 1f, ItemMeleeG = 1f, ItemMeleeB = 0.2f; // Yellow for melee
        public static float ItemBeamHeight = 50f;
        public static float ItemBeamWidth = 0.08f;

        // ── Internal ──
        private static float _rainbowHue = 0f;

        // Item ESP internal state
        private static float _lastItemScan = 0f;
        private static readonly List<ItemTarget> _itemTargets = new List<ItemTarget>();

        private enum ItemCategory { Gun, Melee, NPC, Prop }

        private struct ItemTarget
        {
            public GameObject Go;
            public Vector3 Position;
            public float Dist;
            public string Label;
            public ItemCategory Category;
        }
        private static readonly List<TracerData> _tracers = new List<TracerData>();
        private static readonly List<SkeletonData> _skeletons = new List<SkeletonData>();
        private static Material _lineMat;
        private static Shader _lineShader;

        private struct TracerData
        {
            public RigManager Rig;
            public LineRenderer Line;
            public GameObject Go;
        }

        private struct SkeletonData
        {
            public RigManager Rig;
            public LineRenderer[] Lines; // 12 bones
            public GameObject Root;
        }

        // Box ESP - 2D data for OnGUI labels
        private static readonly List<BoxTarget> _boxTargets = new List<BoxTarget>();
        private struct BoxTarget
        {
            public Vector3 Head;
            public Vector3 Feet;
            public float Dist;
        }

        // Box ESP - 3D world-space LineRenderers (VR visible)
        private static readonly List<BoxESPObj> _boxes = new List<BoxESPObj>();
        private struct BoxESPObj
        {
            public RigManager Rig;
            public LineRenderer Line;
            public GameObject Go;
        }

        // Item ESP - world-space beacon beams (Meteor Client style)
        private static readonly List<ItemBeamObj> _itemBeams = new List<ItemBeamObj>();
        private struct ItemBeamObj
        {
            public GameObject Go;
            public LineRenderer Line;
        }

        private static GUIStyle _nameStyle;
        private static Texture2D _whiteTex;

        public static void Initialize()
        {
            Cleanup();
        }

        public static void Update()
        {
            bool playerEsp = Enabled && Mode != ESPMode.OFF;
            bool itemEsp = ItemESPEnabled;

            if (!playerEsp && !itemEsp)
            {
                Cleanup();
                _itemTargets.Clear();
                return;
            }

            // Advance rainbow hue
            if (ColorMode == ESPColorMode.Rainbow)
                _rainbowHue = (_rainbowHue + RainbowSpeed * Time.deltaTime) % 1f;

            EnsureMaterial();

            Vector3 localPos = Vector3.zero;
            try
            {
                var head = Player.Head;
                if (head != null) localPos = head.position;
            }
            catch { }

            // ═══ Player ESP ═══
            if (playerEsp)
            {
                var players = PlayerTargeting.GetCachedPlayers();
                var localRig = Player.RigManager;

                var targets = new List<(RigManager rig, float dist)>();
                foreach (var p in players)
                {
                    if (p.Rig == null || p.Rig == localRig) continue;
                    var pos = PlayerTargeting.GetTargetPosition(p.Rig);
                    if (!pos.HasValue) continue;
                    targets.Add((p.Rig, Vector3.Distance(localPos, pos.Value)));
                }

                bool doTracers = Mode == ESPMode.TRACERS || Mode == ESPMode.ALL;
                bool doSkeleton = Mode == ESPMode.SKELETON || Mode == ESPMode.ALL;
                bool doBox = Mode == ESPMode.BOX || Mode == ESPMode.ALL;

                if (doTracers) UpdateTracers(targets, localPos); else CleanupTracers();
                if (doSkeleton) UpdateSkeletons(targets); else CleanupSkeletons();

                _boxTargets.Clear();
                if (doBox)
                {
                    UpdateBoxes(targets);
                    foreach (var (rig, dist) in targets)
                    {
                        try
                        {
                            var pr = rig.physicsRig;
                            if (pr?.torso?.rbHead == null || pr?.torso?.rbPelvis == null) continue;
                            _boxTargets.Add(new BoxTarget
                            {
                                Head = pr.torso.rbHead.position + Vector3.up * 0.15f,
                                Feet = pr.torso.rbPelvis.position - Vector3.up * 0.8f,
                                Dist = dist
                            });
                        }
                        catch { }
                    }
                }
                else { CleanupBoxes(); }
            }
            else
            {
                CleanupPlayerESP();
            }

            // ═══ Item ESP ═══
            if (itemEsp)
            {
                float t = Time.unscaledTime;
                if (t - _lastItemScan >= ItemScanInterval)
                {
                    _lastItemScan = t;
                    ScanItems(localPos);
                    SyncItemBeams();
                }
                UpdateItemBeamPositions();
            }
            else
            {
                _itemTargets.Clear();
                CleanupItemBeams();
            }
        }

        /// <summary>
        /// Call from OnGUI for desktop-mirror distance labels.
        /// </summary>
        public static void OnGUI()
        {
            // ── Player Box ESP distance labels (desktop mirror) ──
            if (Enabled && Mode != ESPMode.OFF)
            {
                bool doBox = Mode == ESPMode.BOX || Mode == ESPMode.ALL;
                if (doBox && _boxTargets.Count > 0)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        EnsureGUIResources();

                        foreach (var bt in _boxTargets)
                        {
                            Vector3 screenHead = cam.WorldToScreenPoint(bt.Head);
                            if (screenHead.z < 0) continue;
                            screenHead.y = Screen.height - screenHead.y;

                            Color c = DistColor(bt.Dist);
                            GUI.color = c;
                            string label = $"{bt.Dist:F0}m";
                            GUI.Label(new Rect(screenHead.x - 30f, screenHead.y - 22f, 60f, 20f), label, _nameStyle);
                        }
                        GUI.color = Color.white;
                    }
                }
            }

            // ── Item ESP labels (desktop mirror) ──
            if (ItemESPEnabled && ItemShowLabels && _itemTargets.Count > 0)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    EnsureGUIResources();
                    DrawItemLabels(cam);
                }
            }
        }

        // ═══════════════════════════════════════
        // TRACERS
        // ═══════════════════════════════════════

        private static void UpdateTracers(List<(RigManager rig, float dist)> targets, Vector3 localPos)
        {
            // Remove stale
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var t = _tracers[i];
                bool found = false;
                foreach (var (rig, _) in targets)
                    if (rig == t.Rig) { found = true; break; }
                if (!found || t.Rig == null)
                {
                    if (t.Go != null) UnityEngine.Object.Destroy(t.Go);
                    _tracers.RemoveAt(i);
                }
            }

            foreach (var (rig, dist) in targets)
            {
                // Find or create
                int idx = -1;
                for (int i = 0; i < _tracers.Count; i++)
                    if (_tracers[i].Rig == rig) { idx = i; break; }

                if (idx < 0)
                {
                    var go = new GameObject("ESP_Tracer");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    var lr = go.AddComponent<LineRenderer>();
                    ConfigureLine(lr, TracerWidth);
                    _tracers.Add(new TracerData { Rig = rig, Line = lr, Go = go });
                    idx = _tracers.Count - 1;
                }

                var td = _tracers[idx];
                if (td.Line == null) continue;

                var targetPos = PlayerTargeting.GetTargetPosition(rig);
                if (!targetPos.HasValue) continue;

                td.Line.startWidth = TracerWidth;
                td.Line.endWidth = TracerWidth;
                td.Line.positionCount = 2;
                td.Line.SetPosition(0, localPos + Vector3.down * 0.3f);
                td.Line.SetPosition(1, targetPos.Value);

                Color c = DistColor(dist);
                td.Line.startColor = c;
                td.Line.endColor = c;
            }
        }

        // ═══════════════════════════════════════
        // SKELETON
        // ═══════════════════════════════════════

        // Bone connections: pairs of indices into the bone position array
        // Bones: 0=Head, 1=Neck, 2=Chest, 3=Spine, 4=Pelvis,
        //         5=LeftUpperLeg, 6=LeftLowerLeg, 7=LeftFoot,
        //         8=RightUpperLeg, 9=RightLowerLeg, 10=RightFoot,
        //         11=LeftHand, 12=RightHand
        private static readonly int[,] _boneConnections = {
            {0, 1}, {1, 2}, {2, 3}, {3, 4},     // spine
            {4, 5}, {5, 6}, {6, 7},              // left leg
            {4, 8}, {8, 9}, {9, 10},             // right leg
            {2, 11}, {2, 12}                      // arms (chest to hands)
        };

        private static Vector3[] GetBonePositions(RigManager rig)
        {
            var positions = new Vector3[13];
            try
            {
                var pr = rig.physicsRig;
                if (pr == null) return null;

                var torso = pr.torso;
                if (torso == null) return null;

                positions[0] = torso.rbHead != null ? torso.rbHead.position : Vector3.zero;
                positions[1] = torso.rbNeck != null ? torso.rbNeck.position : positions[0];
                positions[2] = torso.rbChest != null ? torso.rbChest.position : positions[1];
                positions[3] = torso.rbSpine != null ? torso.rbSpine.position : positions[2];
                positions[4] = torso.rbPelvis != null ? torso.rbPelvis.position : positions[3];

                // Legs
                try
                {
                    var ll = pr.legLf;
                    positions[5] = ll?.rbUpper != null ? ll.rbUpper.position : positions[4];
                    positions[6] = ll?.rbLower != null ? ll.rbLower.position : positions[5];
                    positions[7] = ll?.rbEnd != null ? ll.rbEnd.position : positions[6];
                }
                catch
                {
                    positions[5] = positions[6] = positions[7] = positions[4];
                }

                try
                {
                    var rl = pr.legRt;
                    positions[8] = rl?.rbUpper != null ? rl.rbUpper.position : positions[4];
                    positions[9] = rl?.rbLower != null ? rl.rbLower.position : positions[8];
                    positions[10] = rl?.rbEnd != null ? rl.rbEnd.position : positions[9];
                }
                catch
                {
                    positions[8] = positions[9] = positions[10] = positions[4];
                }

                // Hands
                try
                {
                    positions[11] = pr.leftHand != null
                        ? ((UnityEngine.Component)pr.leftHand).transform.position
                        : positions[2];
                }
                catch { positions[11] = positions[2]; }

                try
                {
                    positions[12] = pr.rightHand != null
                        ? ((UnityEngine.Component)pr.rightHand).transform.position
                        : positions[2];
                }
                catch { positions[12] = positions[2]; }

                // Validate — if head is zero, bail
                if (positions[0] == Vector3.zero) return null;
                return positions;
            }
            catch { return null; }
        }

        private static void UpdateSkeletons(List<(RigManager rig, float dist)> targets)
        {
            // Remove stale
            for (int i = _skeletons.Count - 1; i >= 0; i--)
            {
                var s = _skeletons[i];
                bool found = false;
                foreach (var (rig, _) in targets)
                    if (rig == s.Rig) { found = true; break; }
                if (!found || s.Rig == null)
                {
                    if (s.Root != null) UnityEngine.Object.Destroy(s.Root);
                    _skeletons.RemoveAt(i);
                }
            }

            int numBones = _boneConnections.GetLength(0);

            foreach (var (rig, dist) in targets)
            {
                int idx = -1;
                for (int i = 0; i < _skeletons.Count; i++)
                    if (_skeletons[i].Rig == rig) { idx = i; break; }

                if (idx < 0)
                {
                    var root = new GameObject("ESP_Skeleton");
                    UnityEngine.Object.DontDestroyOnLoad(root);
                    var lines = new LineRenderer[numBones];
                    for (int b = 0; b < numBones; b++)
                    {
                        var boneGo = new GameObject($"Bone_{b}");
                        boneGo.transform.SetParent(root.transform);
                        var lr = boneGo.AddComponent<LineRenderer>();
                        ConfigureLine(lr, SkeletonWidth);
                        lines[b] = lr;
                    }
                    _skeletons.Add(new SkeletonData { Rig = rig, Lines = lines, Root = root });
                    idx = _skeletons.Count - 1;
                }

                var sd = _skeletons[idx];
                var bonePos = GetBonePositions(rig);
                if (bonePos == null)
                {
                    // Hide lines if bones unavailable
                    foreach (var lr in sd.Lines)
                        if (lr != null) lr.positionCount = 0;
                    continue;
                }

                Color c = DistColor(dist);
                for (int b = 0; b < numBones; b++)
                {
                    var lr = sd.Lines[b];
                    if (lr == null) continue;

                    lr.startWidth = SkeletonWidth;
                    lr.endWidth = SkeletonWidth;
                    lr.positionCount = 2;
                    lr.SetPosition(0, bonePos[_boneConnections[b, 0]]);
                    lr.SetPosition(1, bonePos[_boneConnections[b, 1]]);
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }
        }

        // ═══════════════════════════════════════
        // ITEM ESP
        // ═══════════════════════════════════════

        private static void ScanItems(Vector3 localPos)
        {
            _itemTargets.Clear();
            try
            {
                var poolees = UnityEngine.Object.FindObjectsOfType<Poolee>();
                if (poolees == null) return;

                foreach (Poolee poolee in poolees)
                {
                    try
                    {
                        if (poolee == null) continue;
                        var go = ((Component)poolee).gameObject;
                        if (go == null || !go.activeInHierarchy) continue;

                        // Skip player rigs
                        if (go.GetComponentInParent<RigManager>() != null) continue;

                        // Skip objects currently held by the local player
                        try
                        {
                            var physRig = Player.RigManager?.physicsRig;
                            if (physRig != null)
                            {
                                var goRoot = go.transform.root;
                                if (physRig.leftHand != null && physRig.leftHand.m_CurrentAttachedGO != null &&
                                    physRig.leftHand.m_CurrentAttachedGO.transform.root == goRoot)
                                    continue;
                                if (physRig.rightHand != null && physRig.rightHand.m_CurrentAttachedGO != null &&
                                    physRig.rightHand.m_CurrentAttachedGO.transform.root == goRoot)
                                    continue;
                            }
                        }
                        catch { }

                        Vector3 pos = go.transform.position;
                        float dist = Vector3.Distance(localPos, pos);
                        if (dist > ItemMaxDistance) continue;

                        // Categorize
                        ItemCategory cat;
                        if (go.GetComponent<AIBrain>() != null || go.GetComponentInChildren<AIBrain>() != null)
                            cat = ItemCategory.NPC;
                        else if (go.GetComponent<Gun>() != null || go.GetComponentInChildren<Gun>() != null)
                            cat = ItemCategory.Gun;
                        else if (go.GetComponent<StabSlash>() != null || go.GetComponentInChildren<StabSlash>() != null)
                            cat = ItemCategory.Melee;
                        else
                            cat = ItemCategory.Prop;

                        // Apply filter
                        if (ItemFilter != ItemESPFilter.All)
                        {
                            if (ItemFilter == ItemESPFilter.Guns && cat != ItemCategory.Gun) continue;
                            if (ItemFilter == ItemESPFilter.Melee && cat != ItemCategory.Melee) continue;
                            if (ItemFilter == ItemESPFilter.NPCs && cat != ItemCategory.NPC) continue;
                            if (ItemFilter == ItemESPFilter.Props && cat != ItemCategory.Prop) continue;
                        }

                        string label = go.name;
                        // Try to get a cleaner name from the spawnable crate
                        try
                        {
                            var crate = poolee.SpawnableCrate;
                            if (crate != null)
                            {
                                string title = crate.Title;
                                if (!string.IsNullOrEmpty(title))
                                    label = title;
                            }
                        }
                        catch { }

                        _itemTargets.Add(new ItemTarget
                        {
                            Go = go,
                            Position = pos,
                            Dist = dist,
                            Label = label,
                            Category = cat
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static Color GetItemColor(ItemCategory cat)
        {
            switch (cat)
            {
                case ItemCategory.NPC: return new Color(ItemNpcR, ItemNpcG, ItemNpcB, 1f);
                case ItemCategory.Gun: return new Color(ItemGunR, ItemGunG, ItemGunB, 1f);
                case ItemCategory.Melee: return new Color(ItemMeleeR, ItemMeleeG, ItemMeleeB, 1f);
                default: return new Color(ItemColorR, ItemColorG, ItemColorB, 1f);
            }
        }

        private static void DrawItemLabels(Camera cam)
        {
            foreach (var item in _itemTargets)
            {
                try
                {
                    if (item.Go == null) continue;
                    Vector3 screenPos = cam.WorldToScreenPoint(item.Go.transform.position);
                    if (screenPos.z < 0) continue;

                    float sx = screenPos.x;
                    float sy = Screen.height - screenPos.y;

                    Color c = GetItemColor(item.Category);
                    GUI.color = c;
                    string text = $"{item.Label} [{item.Dist:F0}m]";
                    GUI.Label(new Rect(sx - 80f, sy - 18f, 160f, 20f), text, _nameStyle);
                }
                catch { }
            }
            GUI.color = Color.white;
        }

        // ═══════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════

        private static Color DistColor(float dist)
        {
            switch (ColorMode)
            {
                case ESPColorMode.Rainbow:
                    return Color.HSVToRGB(_rainbowHue, 1f, 1f);

                case ESPColorMode.CustomRGB:
                    return new Color(CustomR, CustomG, CustomB, 1f);

                case ESPColorMode.Gradient:
                    {
                        float t = Mathf.InverseLerp(NearColor, FarColor, dist);
                        Color a = new Color(CustomR, CustomG, CustomB, 1f);
                        Color b = new Color(GradientR2, GradientG2, GradientB2, 1f);
                        return Color.Lerp(a, b, t);
                    }

                default: // Distance
                    {
                        float t = Mathf.InverseLerp(NearColor, FarColor, dist);
                        if (t < 0.5f)
                            return Color.Lerp(Color.green, Color.yellow, t * 2f);
                        else
                            return Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
                    }
            }
        }

        private static void EnsureMaterial()
        {
            if (_lineMat != null) return;
            try
            {
                // VR-compatible shaders that support vertex colors and stereo rendering
                _lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (_lineShader == null) _lineShader = Shader.Find("Particles/Standard Unlit");
                if (_lineShader == null) _lineShader = Shader.Find("Sprites/Default");
                if (_lineShader == null) _lineShader = Shader.Find("Hidden/Internal-Colored");
                if (_lineShader != null)
                {
                    _lineMat = new Material(_lineShader);
                    _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    _lineMat.SetInt("_ZWrite", 0);
                    _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    _lineMat.renderQueue = 5000;
                    // Ensure vertex colors work
                    if (_lineMat.HasProperty("_BaseColor"))
                        _lineMat.SetColor("_BaseColor", Color.white);
                }
            }
            catch { }
        }

        private static void ConfigureLine(LineRenderer lr, float width)
        {
            if (_lineMat != null) lr.material = _lineMat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 0;
            lr.useWorldSpace = true;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void EnsureGUIResources()
        {
            if (_nameStyle == null)
            {
                _nameStyle = new GUIStyle(GUI.skin.label);
                _nameStyle.fontSize = 12;
                _nameStyle.fontStyle = FontStyle.Bold;
                _nameStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
        }

        private static void DrawBoxOutline(Rect rect, Color color, float thickness)
        {
            GUI.color = color;
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTex);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
            // Right
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTex);
        }

        private static void CleanupPlayerESP()
        {
            CleanupTracers();
            CleanupSkeletons();
            CleanupBoxes();
            _boxTargets.Clear();
        }

        private static void Cleanup()
        {
            CleanupPlayerESP();
            CleanupItemBeams();
            _itemTargets.Clear();
        }

        private static void CleanupTracers()
        {
            foreach (var t in _tracers)
                if (t.Go != null) UnityEngine.Object.Destroy(t.Go);
            _tracers.Clear();
        }

        private static void CleanupSkeletons()
        {
            foreach (var s in _skeletons)
                if (s.Root != null) UnityEngine.Object.Destroy(s.Root);
            _skeletons.Clear();
        }

        // ═══════════════════════════════════════
        // BOX ESP (3D world-space)
        // ═══════════════════════════════════════

        private static void UpdateBoxes(List<(RigManager rig, float dist)> targets)
        {
            // Remove stale
            for (int i = _boxes.Count - 1; i >= 0; i--)
            {
                var b = _boxes[i];
                bool found = false;
                foreach (var (rig, _) in targets)
                    if (rig == b.Rig) { found = true; break; }
                if (!found || b.Rig == null)
                {
                    if (b.Go != null) UnityEngine.Object.Destroy(b.Go);
                    _boxes.RemoveAt(i);
                }
            }

            foreach (var (rig, dist) in targets)
            {
                int idx = -1;
                for (int i = 0; i < _boxes.Count; i++)
                    if (_boxes[i].Rig == rig) { idx = i; break; }

                if (idx < 0)
                {
                    var go = new GameObject("ESP_Box");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    var lr = go.AddComponent<LineRenderer>();
                    ConfigureLine(lr, SkeletonWidth * 1.5f);
                    lr.positionCount = 16;
                    _boxes.Add(new BoxESPObj { Rig = rig, Line = lr, Go = go });
                    idx = _boxes.Count - 1;
                }

                var bd = _boxes[idx];
                if (bd.Line == null) continue;

                Vector3 head, feet;
                try
                {
                    var pr = rig.physicsRig;
                    if (pr?.torso?.rbHead == null || pr?.torso?.rbPelvis == null) continue;
                    head = pr.torso.rbHead.position + Vector3.up * 0.15f;
                    feet = pr.torso.rbPelvis.position - Vector3.up * 0.8f;
                }
                catch { continue; }

                float pad = BoxPadding;
                float cx = (head.x + feet.x) / 2f;
                float cz = (head.z + feet.z) / 2f;

                // 8 corners
                Vector3 A = new Vector3(cx - pad, feet.y, cz - pad);
                Vector3 B = new Vector3(cx + pad, feet.y, cz - pad);
                Vector3 C = new Vector3(cx + pad, feet.y, cz + pad);
                Vector3 D = new Vector3(cx - pad, feet.y, cz + pad);
                Vector3 E = new Vector3(cx - pad, head.y, cz - pad);
                Vector3 F = new Vector3(cx + pad, head.y, cz - pad);
                Vector3 G = new Vector3(cx + pad, head.y, cz + pad);
                Vector3 H = new Vector3(cx - pad, head.y, cz + pad);

                // Path tracing all 12 edges: A-B-F-E-A-D-C-B-C-G-F-G-H-E-H-D
                bd.Line.positionCount = 16;
                bd.Line.SetPosition(0, A); bd.Line.SetPosition(1, B);
                bd.Line.SetPosition(2, F); bd.Line.SetPosition(3, E);
                bd.Line.SetPosition(4, A); bd.Line.SetPosition(5, D);
                bd.Line.SetPosition(6, C); bd.Line.SetPosition(7, B);
                bd.Line.SetPosition(8, C); bd.Line.SetPosition(9, G);
                bd.Line.SetPosition(10, F); bd.Line.SetPosition(11, G);
                bd.Line.SetPosition(12, H); bd.Line.SetPosition(13, E);
                bd.Line.SetPosition(14, H); bd.Line.SetPosition(15, D);

                bd.Line.startWidth = SkeletonWidth * 1.5f;
                bd.Line.endWidth = SkeletonWidth * 1.5f;

                Color c = DistColor(dist);
                bd.Line.startColor = c;
                bd.Line.endColor = c;
            }
        }

        private static void CleanupBoxes()
        {
            foreach (var b in _boxes)
                if (b.Go != null) UnityEngine.Object.Destroy(b.Go);
            _boxes.Clear();
        }

        // ═══════════════════════════════════════
        // ITEM ESP BEAMS (Meteor Client style)
        // ═══════════════════════════════════════

        private static void SyncItemBeams()
        {
            int needed = _itemTargets.Count;

            // Grow pool if needed
            while (_itemBeams.Count < needed)
            {
                var go = new GameObject("ESP_ItemBeam");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var lr = go.AddComponent<LineRenderer>();
                ConfigureLine(lr, ItemBeamWidth);
                lr.positionCount = 2;
                _itemBeams.Add(new ItemBeamObj { Go = go, Line = lr });
            }

            // Update active beams
            for (int i = 0; i < needed; i++)
            {
                var beam = _itemBeams[i];
                if (!beam.Go.activeSelf) beam.Go.SetActive(true);
                try
                {
                    var item = _itemTargets[i];
                    Vector3 pos = item.Go != null ? item.Go.transform.position : item.Position;
                    beam.Line.SetPosition(0, pos);
                    beam.Line.SetPosition(1, pos + Vector3.up * ItemBeamHeight);

                    Color c = GetItemColor(item.Category);
                    beam.Line.startColor = c;
                    beam.Line.endColor = new Color(c.r, c.g, c.b, 0.05f);
                    beam.Line.startWidth = ItemBeamWidth;
                    beam.Line.endWidth = ItemBeamWidth * 0.15f;
                }
                catch { }
            }

            // Deactivate excess beams
            for (int i = needed; i < _itemBeams.Count; i++)
            {
                if (_itemBeams[i].Go.activeSelf)
                    _itemBeams[i].Go.SetActive(false);
            }
        }

        private static void UpdateItemBeamPositions()
        {
            int count = Math.Min(_itemTargets.Count, _itemBeams.Count);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var item = _itemTargets[i];
                    if (item.Go == null || !_itemBeams[i].Go.activeSelf) continue;
                    Vector3 pos = item.Go.transform.position;
                    _itemBeams[i].Line.SetPosition(0, pos);
                    _itemBeams[i].Line.SetPosition(1, pos + Vector3.up * ItemBeamHeight);
                }
                catch { }
            }
        }

        private static void CleanupItemBeams()
        {
            foreach (var b in _itemBeams)
                if (b.Go != null) UnityEngine.Object.Destroy(b.Go);
            _itemBeams.Clear();
        }
    }
}
