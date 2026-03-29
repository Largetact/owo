using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using System;
using System.Reflection;

[assembly: MelonInfo(typeof(StandaloneBodyLogColor.BodyLogColorMod), "BodyLog & Radial Color", "1.0.0", "DOOBER")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace StandaloneBodyLogColor
{
    public class BodyLogColorMod : MelonMod
    {
        private static BoneLib.BoneMenu.Page _mainPage;

        private static MelonPreferences_Category _prefCategory;
        private static MelonPreferences_Entry<bool> _prefEnabled;
        private static MelonPreferences_Entry<float> _prefBodyLogR, _prefBodyLogG, _prefBodyLogB, _prefBodyLogA;
        private static MelonPreferences_Entry<float> _prefBallR, _prefBallG, _prefBallB, _prefBallA;
        private static MelonPreferences_Entry<float> _prefLineR, _prefLineG, _prefLineB, _prefLineA;
        private static MelonPreferences_Entry<float> _prefRadialR, _prefRadialG, _prefRadialB, _prefRadialA;

        public override void OnInitializeMelon()
        {
            _prefCategory = MelonPreferences.CreateCategory("BodyLogColor");
            _prefEnabled = _prefCategory.CreateEntry("Enabled", false);
            _prefBodyLogR = _prefCategory.CreateEntry("BodyLogR", 255f);
            _prefBodyLogG = _prefCategory.CreateEntry("BodyLogG", 255f);
            _prefBodyLogB = _prefCategory.CreateEntry("BodyLogB", 255f);
            _prefBodyLogA = _prefCategory.CreateEntry("BodyLogA", 255f);
            _prefBallR = _prefCategory.CreateEntry("BallR", 255f);
            _prefBallG = _prefCategory.CreateEntry("BallG", 255f);
            _prefBallB = _prefCategory.CreateEntry("BallB", 255f);
            _prefBallA = _prefCategory.CreateEntry("BallA", 255f);
            _prefLineR = _prefCategory.CreateEntry("LineR", 255f);
            _prefLineG = _prefCategory.CreateEntry("LineG", 255f);
            _prefLineB = _prefCategory.CreateEntry("LineB", 255f);
            _prefLineA = _prefCategory.CreateEntry("LineA", 255f);
            _prefRadialR = _prefCategory.CreateEntry("RadialR", 255f);
            _prefRadialG = _prefCategory.CreateEntry("RadialG", 255f);
            _prefRadialB = _prefCategory.CreateEntry("RadialB", 255f);
            _prefRadialA = _prefCategory.CreateEntry("RadialA", 255f);

            BodyLogColorController.Enabled = _prefEnabled.Value;
            BodyLogColorController.BodyLogR = _prefBodyLogR.Value;
            BodyLogColorController.BodyLogG = _prefBodyLogG.Value;
            BodyLogColorController.BodyLogB = _prefBodyLogB.Value;
            BodyLogColorController.BodyLogA = _prefBodyLogA.Value;
            BodyLogColorController.BallR = _prefBallR.Value;
            BodyLogColorController.BallG = _prefBallG.Value;
            BodyLogColorController.BallB = _prefBallB.Value;
            BodyLogColorController.BallA = _prefBallA.Value;
            BodyLogColorController.LineR = _prefLineR.Value;
            BodyLogColorController.LineG = _prefLineG.Value;
            BodyLogColorController.LineB = _prefLineB.Value;
            BodyLogColorController.LineA = _prefLineA.Value;
            BodyLogColorController.RadialR = _prefRadialR.Value;
            BodyLogColorController.RadialG = _prefRadialG.Value;
            BodyLogColorController.RadialB = _prefRadialB.Value;
            BodyLogColorController.RadialA = _prefRadialA.Value;

            LoggerInstance.Msg("BodyLog & Radial Menu Color mod loaded");
        }

        public override void OnLateInitializeMelon()
        {
            _mainPage = BoneLib.BoneMenu.Page.Root.CreatePage("BodyLog Color", Color.magenta);
            BuildMenu();
        }

        public override void OnUpdate()
        {
            BodyLogColorController.Update();
        }

        public override void OnApplicationQuit()
        {
            SavePreferences();
        }

        public static void SavePreferences()
        {
            if (_prefCategory == null) return;
            _prefEnabled.Value = BodyLogColorController.Enabled;
            _prefBodyLogR.Value = BodyLogColorController.BodyLogR;
            _prefBodyLogG.Value = BodyLogColorController.BodyLogG;
            _prefBodyLogB.Value = BodyLogColorController.BodyLogB;
            _prefBodyLogA.Value = BodyLogColorController.BodyLogA;
            _prefBallR.Value = BodyLogColorController.BallR;
            _prefBallG.Value = BodyLogColorController.BallG;
            _prefBallB.Value = BodyLogColorController.BallB;
            _prefBallA.Value = BodyLogColorController.BallA;
            _prefLineR.Value = BodyLogColorController.LineR;
            _prefLineG.Value = BodyLogColorController.LineG;
            _prefLineB.Value = BodyLogColorController.LineB;
            _prefLineA.Value = BodyLogColorController.LineA;
            _prefRadialR.Value = BodyLogColorController.RadialR;
            _prefRadialG.Value = BodyLogColorController.RadialG;
            _prefRadialB.Value = BodyLogColorController.RadialB;
            _prefRadialA.Value = BodyLogColorController.RadialA;
            _prefCategory.SaveToFile(false);
        }

        private void BuildMenu()
        {
            _mainPage.CreateBool("Enabled", Color.white, BodyLogColorController.Enabled,
                (value) => { BodyLogColorController.Enabled = value; SavePreferences(); });
            _mainPage.CreateFunction("Apply Colors Now", Color.green, () => BodyLogColorController.ApplyAll());

            // Body Log hologram tint
            var bodyLogColorPage = _mainPage.CreatePage("Body Log Tint", Color.cyan);
            bodyLogColorPage.CreateFloat("R", Color.red, BodyLogColorController.BodyLogR, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BodyLogR = value; SavePreferences(); });
            bodyLogColorPage.CreateFloat("G", Color.green, BodyLogColorController.BodyLogG, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BodyLogG = value; SavePreferences(); });
            bodyLogColorPage.CreateFloat("B", Color.blue, BodyLogColorController.BodyLogB, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BodyLogB = value; SavePreferences(); });
            bodyLogColorPage.CreateFloat("A", Color.white, BodyLogColorController.BodyLogA, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BodyLogA = value; SavePreferences(); });

            // Ball (sphere grip)
            var ballColorPage = _mainPage.CreatePage("Ball Color", Color.yellow);
            ballColorPage.CreateFloat("R", Color.red, BodyLogColorController.BallR, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BallR = value; SavePreferences(); });
            ballColorPage.CreateFloat("G", Color.green, BodyLogColorController.BallG, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BallG = value; SavePreferences(); });
            ballColorPage.CreateFloat("B", Color.blue, BodyLogColorController.BallB, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BallB = value; SavePreferences(); });
            ballColorPage.CreateFloat("A", Color.white, BodyLogColorController.BallA, 5f, 0f, 255f,
                (value) => { BodyLogColorController.BallA = value; SavePreferences(); });

            // Line renderer
            var lineColorPage = _mainPage.CreatePage("Line Color", Color.green);
            lineColorPage.CreateFloat("R", Color.red, BodyLogColorController.LineR, 5f, 0f, 255f,
                (value) => { BodyLogColorController.LineR = value; SavePreferences(); });
            lineColorPage.CreateFloat("G", Color.green, BodyLogColorController.LineG, 5f, 0f, 255f,
                (value) => { BodyLogColorController.LineG = value; SavePreferences(); });
            lineColorPage.CreateFloat("B", Color.blue, BodyLogColorController.LineB, 5f, 0f, 255f,
                (value) => { BodyLogColorController.LineB = value; SavePreferences(); });
            lineColorPage.CreateFloat("A", Color.white, BodyLogColorController.LineA, 5f, 0f, 255f,
                (value) => { BodyLogColorController.LineA = value; SavePreferences(); });

            // Radial menu
            var radialColorPage = _mainPage.CreatePage("Radial Menu Color", Color.red);
            radialColorPage.CreateFloat("R", Color.red, BodyLogColorController.RadialR, 5f, 0f, 255f,
                (value) => { BodyLogColorController.RadialR = value; SavePreferences(); });
            radialColorPage.CreateFloat("G", Color.green, BodyLogColorController.RadialG, 5f, 0f, 255f,
                (value) => { BodyLogColorController.RadialG = value; SavePreferences(); });
            radialColorPage.CreateFloat("B", Color.blue, BodyLogColorController.RadialB, 5f, 0f, 255f,
                (value) => { BodyLogColorController.RadialB = value; SavePreferences(); });
            radialColorPage.CreateFloat("A", Color.white, BodyLogColorController.RadialA, 5f, 0f, 255f,
                (value) => { BodyLogColorController.RadialA = value; SavePreferences(); });
        }
    }

    public static class BodyLogColorController
    {
        private static bool _enabled = false;

        // Body Log hologram tint
        private static float _bodyLogR = 255f, _bodyLogG = 255f, _bodyLogB = 255f, _bodyLogA = 255f;
        // Ball (sphere grip material)
        private static float _ballR = 255f, _ballG = 255f, _ballB = 255f, _ballA = 255f;
        // Line renderer
        private static float _lineR = 255f, _lineG = 255f, _lineB = 255f, _lineA = 255f;
        // Radial menu buttons
        private static float _radialR = 255f, _radialG = 255f, _radialB = 255f, _radialA = 255f;

        public static bool Enabled { get => _enabled; set => _enabled = value; }

        public static float BodyLogR { get => _bodyLogR; set => _bodyLogR = value; }
        public static float BodyLogG { get => _bodyLogG; set => _bodyLogG = value; }
        public static float BodyLogB { get => _bodyLogB; set => _bodyLogB = value; }
        public static float BodyLogA { get => _bodyLogA; set => _bodyLogA = value; }

        public static float BallR { get => _ballR; set => _ballR = value; }
        public static float BallG { get => _ballG; set => _ballG = value; }
        public static float BallB { get => _ballB; set => _ballB = value; }
        public static float BallA { get => _ballA; set => _ballA = value; }

        public static float LineR { get => _lineR; set => _lineR = value; }
        public static float LineG { get => _lineG; set => _lineG = value; }
        public static float LineB { get => _lineB; set => _lineB = value; }
        public static float LineA { get => _lineA; set => _lineA = value; }

        public static float RadialR { get => _radialR; set => _radialR = value; }
        public static float RadialG { get => _radialG; set => _radialG = value; }
        public static float RadialB { get => _radialB; set => _radialB = value; }
        public static float RadialA { get => _radialA; set => _radialA = value; }

        private static Color ToUnityColor(float r, float g, float b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static Color BodyLogColor => ToUnityColor(_bodyLogR, _bodyLogG, _bodyLogB, _bodyLogA);
        private static Color BallColor => ToUnityColor(_ballR, _ballG, _ballB, _ballA);
        private static Color LineColor => ToUnityColor(_lineR, _lineG, _lineB, _lineA);
        private static Color RadialColor => ToUnityColor(_radialR, _radialG, _radialB, _radialA);

        public static void ApplyBodyLogColors()
        {
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                // Access m_elbowRt via Rig base class
                Transform bodyLogTransform = null;
                var elbowRt = ((Rig)physRig).m_elbowRt;
                if (elbowRt != null)
                    bodyLogTransform = elbowRt.Find("BodyLogSlot/BodyLog");

                if (bodyLogTransform == null)
                {
                    var elbowLf = ((Rig)physRig).m_elbowLf;
                    if (elbowLf != null)
                        bodyLogTransform = elbowLf.Find("BodyLogSlot/BodyLog");
                }

                if (bodyLogTransform == null) return;

                // Get PullCordDevice directly (Il2CppSLZ.Bonelab type)
                var pullCord = bodyLogTransform.GetComponent<PullCordDevice>();
                if (pullCord == null) return;

                // Set hologram tint
                pullCord.hologramTint = BodyLogColor;

                // Set line renderer color
                LineRenderer lineRend = pullCord.lineRenderer;
                if (lineRend != null)
                {
                    lineRend.startColor = LineColor;
                    lineRend.endColor = LineColor;
                }

                // Set ball/sphere color
                Transform sphereTransform = bodyLogTransform.Find("spheregrip/Sphere/Art/GrabGizmo");
                if (sphereTransform != null)
                {
                    var meshRenderer = sphereTransform.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        ((Renderer)meshRenderer).material.color = BallColor;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BodyLogColor] Apply body log error: {ex.Message}");
            }
        }

        public static void ApplyRadialMenuColors()
        {
            try
            {
                var uiRig = Player.UIRig;
                if (uiRig == null) return;

                var popUpMenuProp = uiRig.GetType().GetProperty("popUpMenu", BindingFlags.Public | BindingFlags.Instance);
                object popUpMenu = popUpMenuProp?.GetValue(uiRig);
                if (popUpMenu == null) return;

                var radialViewProp = popUpMenu.GetType().GetProperty("radialPageView", BindingFlags.Public | BindingFlags.Instance);
                object radialPageView = radialViewProp?.GetValue(popUpMenu);
                if (radialPageView == null) return;

                var buttonsProp = radialPageView.GetType().GetProperty("buttons", BindingFlags.Public | BindingFlags.Instance);
                var buttonsObj = buttonsProp?.GetValue(radialPageView);
                if (buttonsObj == null) return;

                var buttonsArray = buttonsObj as System.Collections.IEnumerable;
                if (buttonsArray == null) return;

                Color color = RadialColor;
                foreach (object item in buttonsArray)
                {
                    if (item == null) continue;
                    var itemType = item.GetType();

                    var color2Prop = itemType.GetProperty("color2", BindingFlags.Public | BindingFlags.Instance);
                    color2Prop?.SetValue(item, color);

                    var textMeshProp = itemType.GetProperty("textMesh", BindingFlags.Public | BindingFlags.Instance);
                    var textMesh = textMeshProp?.GetValue(item);
                    if (textMesh != null)
                    {
                        var colorProp = textMesh.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                        colorProp?.SetValue(textMesh, color);
                    }

                    var iconProp = itemType.GetProperty("icon", BindingFlags.Public | BindingFlags.Instance);
                    var icon = iconProp?.GetValue(item);
                    if (icon != null)
                    {
                        var getCompMethod = icon.GetType().GetMethod("GetComponent", new[] { typeof(string) });
                        if (getCompMethod != null)
                        {
                            var canvasRenderer = getCompMethod.Invoke(icon, new object[] { "CanvasRenderer" });
                            if (canvasRenderer != null)
                            {
                                var setColorMethod = canvasRenderer.GetType().GetMethod("SetColor", new[] { typeof(Color) });
                                setColorMethod?.Invoke(canvasRenderer, new object[] { color });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BodyLogColor] Apply radial menu error: {ex.Message}");
            }
        }

        public static void ApplyAll()
        {
            ApplyBodyLogColors();
            ApplyRadialMenuColors();
        }

        public static void Update()
        {
            if (!_enabled) return;

            try
            {
                var uiRig = Player.UIRig;
                if (uiRig == null) return;

                var popUpMenuProp = uiRig.GetType().GetProperty("popUpMenu", BindingFlags.Public | BindingFlags.Instance);
                object popUpMenu = popUpMenuProp?.GetValue(uiRig);
                if (popUpMenu == null) return;

                var gameObjectProp = popUpMenu.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                if (gameObjectProp == null)
                    gameObjectProp = typeof(Component).GetProperty("gameObject");
                var go = gameObjectProp?.GetValue(popUpMenu) as GameObject;
                if (go != null && go.activeInHierarchy)
                {
                    ApplyRadialMenuColors();
                }
            }
            catch { }
        }

    }
}
