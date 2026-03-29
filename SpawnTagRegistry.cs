using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Shared tag registry for spawn→force association across all spawn systems.
    /// When an object is spawned, the SpawnCallback registers it with a unique tag.
    /// Force resolution then looks up the tag instead of proximity-searching.
    /// </summary>
    public static class SpawnTagRegistry
    {
        private static Dictionary<string, GameObject> _tags = new Dictionary<string, GameObject>();
        private static int _counter = 0;

        /// <summary>
        /// Generate a unique tag string with the given prefix.
        /// </summary>
        public static string GenerateTag(string prefix)
        {
            return $"{prefix}_{++_counter}";
        }

        /// <summary>
        /// Register a spawned object with a tag.
        /// </summary>
        public static void Register(string tag, GameObject go)
        {
            if (!string.IsNullOrEmpty(tag) && go != null)
                _tags[tag] = go;
        }

        /// <summary>
        /// Resolve a tag to its registered GameObject. Returns null if not found or destroyed.
        /// </summary>
        public static GameObject Resolve(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            if (_tags.TryGetValue(tag, out var go))
            {
                if (go != null) return go;
                _tags.Remove(tag);
            }
            return null;
        }

        /// <summary>
        /// Remove a tag entry.
        /// </summary>
        public static void Remove(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
                _tags.Remove(tag);
        }

        /// <summary>
        /// Remove stale (null) entries and reset counter if it gets large.
        /// </summary>
        public static void Cleanup()
        {
            var dead = _tags.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
            foreach (var key in dead) _tags.Remove(key);
            if (_counter > 100000) _counter = 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // SpawnCallback helper — called from expression-tree delegates
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called from the NetworkAssetSpawner SpawnCallback.
        /// Registers the spawned object by tag and optionally applies scale.
        /// The callbackInfo parameter is a boxed SpawnCallbackInfo.
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

                // Register tag
                if (!string.IsNullOrEmpty(tag))
                {
                    Register(tag, go);
                    Main.MelonLog.Msg($"[SpawnTag] Registered '{tag}' → '{go.name}'");
                }

                // Apply scale if needed
                if (scale > 0f && Mathf.Abs(scale - 1f) > 0.001f)
                {
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

                    Main.MelonLog.Msg($"[SpawnTag] Applied scale {scale}x to '{go.name}'");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[SpawnTag] Callback error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Shared SpawnCallback setup — used by all spawn controllers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Set the SpawnCallback on a SpawnRequestInfo to register tag and apply scale.
        /// Uses expression trees to handle the Il2Cpp-typed delegate.
        /// </summary>
        public static bool TrySetSpawnCallback(object spawnReq, Type spawnRequestType, string tag, float scale)
        {
            try
            {
                var callbackInfoType = FindType("SpawnCallbackInfo");
                var callbackMember = spawnRequestType.GetField("SpawnCallback")
                    ?? (MemberInfo)spawnRequestType.GetProperty("SpawnCallback");

                if (callbackInfoType == null || callbackMember == null)
                    return false;

                var actionType = typeof(Action<>).MakeGenericType(callbackInfoType);
                var param = Expression.Parameter(callbackInfoType, "info");

                var helperMethod = typeof(SpawnTagRegistry).GetMethod(
                    "OnSpawnCallback",
                    BindingFlags.Public | BindingFlags.Static);

                if (helperMethod == null) return false;

                var boxedParam = Expression.Convert(param, typeof(object));
                var tagConst = Expression.Constant(tag, typeof(string));
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
                Main.MelonLog.Warning($"[SpawnTag] TrySetSpawnCallback error: {ex.Message}");
                return false;
            }
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName) return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
