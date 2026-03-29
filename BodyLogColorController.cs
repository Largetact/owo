using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using System;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Customize Body Log and Radial Menu colors.
    /// Uses reflection for Il2Cpp types not directly referenced in the project.
    /// </summary>
    public static class BodyLogColorController
    {
        // ═══════════════════════════════════════════════════
        // SETTINGS — 4 color groups (0-255 range for BoneMenu)
        // ═══════════════════════════════════════════════════
        private static bool _enabled = false;

        // Body Log hologram tint
        private static float _bodyLogR = 255f, _bodyLogG = 255f, _bodyLogB = 255f, _bodyLogA = 255f;
        // Ball (sphere grip material)
        private static float _ballR = 255f, _ballG = 255f, _ballB = 255f, _ballA = 255f;
        // Line renderer
        private static float _lineR = 255f, _lineG = 255f, _lineB = 255f, _lineA = 255f;
        // Radial menu buttons
        private static float _radialR = 255f, _radialG = 255f, _radialB = 255f, _radialA = 255f;

        // ═══════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════
        private static Color ToUnityColor(float r, float g, float b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static Color BodyLogColor => ToUnityColor(_bodyLogR, _bodyLogG, _bodyLogB, _bodyLogA);
        private static Color BallColor => ToUnityColor(_ballR, _ballG, _ballB, _ballA);
        private static Color LineColor => ToUnityColor(_lineR, _lineG, _lineB, _lineA);
        private static Color RadialColor => ToUnityColor(_radialR, _radialG, _radialB, _radialA);

        // ═══════════════════════════════════════════════════
        // APPLY BODY LOG COLORS
        // ═══════════════════════════════════════════════════
        public static void ApplyBodyLogColors()
        {
            try
            {
                var physRig = Player.PhysicsRig;
                if (physRig == null) return;

                // Access m_elbowRt via Rig base class (same pattern as FusionProtector)
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

                Main.MelonLog.Msg("[BodyLogColor] Body log colors applied");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[BodyLogColor] Apply body log error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        // APPLY RADIAL MENU COLORS
        // ═══════════════════════════════════════════════════
        public static void ApplyRadialMenuColors()
        {
            try
            {
                var uiRig = Player.UIRig;
                if (uiRig == null) return;

                // Access popUpMenu via reflection
                var popUpMenuProp = uiRig.GetType().GetProperty("popUpMenu", BindingFlags.Public | BindingFlags.Instance);
                object popUpMenu = popUpMenuProp?.GetValue(uiRig);
                if (popUpMenu == null) return;

                // Access radialPageView
                var radialViewProp = popUpMenu.GetType().GetProperty("radialPageView", BindingFlags.Public | BindingFlags.Instance);
                object radialPageView = radialViewProp?.GetValue(popUpMenu);
                if (radialPageView == null) return;

                // Access buttons array
                var buttonsProp = radialPageView.GetType().GetProperty("buttons", BindingFlags.Public | BindingFlags.Instance);
                var buttonsObj = buttonsProp?.GetValue(radialPageView);
                if (buttonsObj == null) return;

                // Enumerate buttons
                var buttonsArray = buttonsObj as System.Collections.IEnumerable;
                if (buttonsArray == null) return;

                Color color = RadialColor;
                foreach (object item in buttonsArray)
                {
                    if (item == null) continue;
                    var itemType = item.GetType();

                    // Set color2
                    var color2Prop = itemType.GetProperty("color2", BindingFlags.Public | BindingFlags.Instance);
                    color2Prop?.SetValue(item, color);

                    // Set textMesh.color
                    var textMeshProp = itemType.GetProperty("textMesh", BindingFlags.Public | BindingFlags.Instance);
                    var textMesh = textMeshProp?.GetValue(item);
                    if (textMesh != null)
                    {
                        var colorProp = textMesh.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                        colorProp?.SetValue(textMesh, color);
                    }

                    // Set icon CanvasRenderer color
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
                Main.MelonLog.Warning($"[BodyLogColor] Apply radial menu error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        // APPLY ALL
        // ═══════════════════════════════════════════════════
        public static void ApplyAll()
        {
            ApplyBodyLogColors();
            ApplyRadialMenuColors();
        }

        /// <summary>
        /// Call from main Update loop — reapplies radial menu colors when the menu is visible.
        /// </summary>
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

                // Check if menu is active
                var goType = popUpMenu.GetType();
                var gameObjectProp = goType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                if (gameObjectProp == null)
                {
                    // Try Component.gameObject
                    gameObjectProp = typeof(Component).GetProperty("gameObject");
                }
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
