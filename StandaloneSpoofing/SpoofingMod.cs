using MelonLoader;
using UnityEngine;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using BoneLib.BoneMenu;

[assembly: MelonInfo(typeof(StandaloneSpoofing.SpoofingMod), "Standalone Spoofing", "1.0.0", "XI")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace StandaloneSpoofing
{
    /// <summary>
    /// Standalone spoofing mod — separate from the main utility mod.
    /// Spoofs Username, Nickname, and Description via LabFusion's LocalPlayer.Metadata API.
    /// Uses the same SetValue(string) pattern as AvatarCopierController.
    /// </summary>
    public class SpoofingMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        private static bool _usernameSpoofEnabled;
        public static bool UsernameSpoofEnabled
        {
            get => _usernameSpoofEnabled;
            set { if (_usernameSpoofEnabled && !value) RestoreOriginal("Username"); _usernameSpoofEnabled = value; }
        }
        public static string FakeUsername { get; set; } = "Player";

        private static bool _nicknameSpoofEnabled;
        public static bool NicknameSpoofEnabled
        {
            get => _nicknameSpoofEnabled;
            set { if (_nicknameSpoofEnabled && !value) RestoreOriginal("Nickname"); _nicknameSpoofEnabled = value; }
        }
        public static string FakeNickname { get; set; } = "";

        private static bool _descriptionSpoofEnabled;
        public static bool DescriptionSpoofEnabled
        {
            get => _descriptionSpoofEnabled;
            set { if (_descriptionSpoofEnabled && !value) RestoreOriginal("Description"); _descriptionSpoofEnabled = value; }
        }
        public static string FakeDescription { get; set; } = "";

        // Original values captured before spoofing
        private static string _originalUsername;
        private static string _originalNickname;
        private static string _originalDescription;
        private static bool _originalsCaptured;

        // Cached reflection targets
        private static Type _localPlayerType;
        private static PropertyInfo _metadataProp;

        // Cached metadata wrapper objects + SetValue methods
        private static object _nicknameObj;
        private static MethodInfo _nicknameSetValue;
        private static object _descriptionObj;
        private static MethodInfo _descriptionSetValue;
        private static object _usernameObj;
        private static MethodInfo _usernameSetValue;

        private static bool _reflectionResolved;
        private static float _lastApply;
        private const float APPLY_INTERVAL = 2f;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Log.Msg("Standalone Spoofing mod loaded");
            SetupBoneMenu();
        }

        public override void OnUpdate()
        {
            if (!UsernameSpoofEnabled && !NicknameSpoofEnabled && !DescriptionSpoofEnabled)
                return;

            if (!_reflectionResolved)
                ResolveReflection();

            if (!_reflectionResolved) return;

            // Re-apply periodically since Fusion may reset values
            if (Time.time - _lastApply < APPLY_INTERVAL) return;
            _lastApply = Time.time;

            ApplySpoofs();
        }

        private static void ResolveReflection()
        {
            try
            {
                // Find LocalPlayer type (same approach as AvatarCopierController)
                _localPlayerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                    .FirstOrDefault(t => t.Name == "LocalPlayer");

                if (_localPlayerType == null)
                {
                    Log.Warning("[Spoof] LocalPlayer type not found (LabFusion not loaded yet?)");
                    return;
                }

                _metadataProp = _localPlayerType.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Static);
                if (_metadataProp == null)
                {
                    Log.Warning("[Spoof] LocalPlayer.Metadata property not found");
                    return;
                }

                var metadata = _metadataProp.GetValue(null);
                if (metadata == null)
                {
                    Log.Warning("[Spoof] Metadata is null (not connected yet?)");
                    return;
                }

                // Resolve Nickname wrapper
                var nicknameProp = metadata.GetType().GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                if (nicknameProp != null)
                {
                    _nicknameObj = nicknameProp.GetValue(metadata);
                    if (_nicknameObj != null)
                        _nicknameSetValue = _nicknameObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                }

                // Resolve Description wrapper
                var descProp = metadata.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                if (descProp != null)
                {
                    _descriptionObj = descProp.GetValue(metadata);
                    if (_descriptionObj != null)
                        _descriptionSetValue = _descriptionObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                }

                // Resolve Username wrapper
                var usernameProp = metadata.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                if (usernameProp != null)
                {
                    _usernameObj = usernameProp.GetValue(metadata);
                    if (_usernameObj != null)
                        _usernameSetValue = _usernameObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                }

                _reflectionResolved = true;

                // Capture original values before any spoofing
                CaptureOriginals();

                Log.Msg("[Spoof] LabFusion reflection resolved successfully");
                Log.Msg($"  Nickname: {(_nicknameSetValue != null ? "OK" : "NOT FOUND")}");
                Log.Msg($"  Description: {(_descriptionSetValue != null ? "OK" : "NOT FOUND")}");
                Log.Msg($"  Username: {(_usernameSetValue != null ? "OK" : "NOT FOUND")}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Spoof] Reflection resolve failed: {ex.Message}");
            }
        }

        private static void ApplySpoofs()
        {
            try
            {
                // Re-fetch metadata in case it changed (reconnection etc.)
                var metadata = _metadataProp?.GetValue(null);
                if (metadata == null) return;

                if (NicknameSpoofEnabled && _nicknameSetValue != null)
                {
                    // Re-resolve the wrapper object each time in case metadata changed
                    var nickProp = metadata.GetType().GetProperty("Nickname", BindingFlags.Public | BindingFlags.Instance);
                    var nickObj = nickProp?.GetValue(metadata);
                    if (nickObj != null)
                    {
                        var setVal = nickObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                        setVal?.Invoke(nickObj, new object[] { FakeNickname });
                    }
                }

                if (DescriptionSpoofEnabled && _descriptionSetValue != null)
                {
                    var descProp = metadata.GetType().GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                    var descObj = descProp?.GetValue(metadata);
                    if (descObj != null)
                    {
                        var setVal = descObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                        setVal?.Invoke(descObj, new object[] { FakeDescription });
                    }
                }

                if (UsernameSpoofEnabled && _usernameSetValue != null)
                {
                    var userProp = metadata.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                    var userObj = userProp?.GetValue(metadata);
                    if (userObj != null)
                    {
                        var setVal = userObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string));
                        setVal?.Invoke(userObj, new object[] { FakeUsername });
                    }
                }
            }
            catch { }
        }

        private static void CaptureOriginals()
        {
            if (_originalsCaptured) return;
            try
            {
                var metadata = _metadataProp?.GetValue(null);
                if (metadata == null) return;

                _originalUsername = GetCurrentValue(metadata, "Username");
                _originalNickname = GetCurrentValue(metadata, "Nickname");
                _originalDescription = GetCurrentValue(metadata, "Description");
                _originalsCaptured = true;

                Log.Msg($"[Spoof] Originals captured — User: {_originalUsername}, Nick: {_originalNickname}, Desc: {_originalDescription}");
            }
            catch { }
        }

        private static string GetCurrentValue(object metadata, string propName)
        {
            try
            {
                var prop = metadata.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                var wrapper = prop?.GetValue(metadata);
                if (wrapper == null) return "";
                var getVal = wrapper.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetValue" && m.GetParameters().Length == 0);
                return getVal?.Invoke(wrapper, null) as string ?? "";
            }
            catch { return ""; }
        }

        private static void RestoreOriginal(string which)
        {
            if (!_reflectionResolved || !_originalsCaptured) return;
            try
            {
                var metadata = _metadataProp?.GetValue(null);
                if (metadata == null) return;

                string original = which switch
                {
                    "Username" => _originalUsername,
                    "Nickname" => _originalNickname,
                    "Description" => _originalDescription,
                    _ => ""
                };

                var prop = metadata.GetType().GetProperty(which, BindingFlags.Public | BindingFlags.Instance);
                var wrapper = prop?.GetValue(metadata);
                if (wrapper == null) return;
                var setVal = wrapper.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(string));
                setVal?.Invoke(wrapper, new object[] { original });
                Log.Msg($"[Spoof] Restored {which} to: {original}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Spoof] Failed to restore {which}: {ex.Message}");
            }
        }

        private void SetupBoneMenu()
        {
            try
            {
                var mainPage = Page.Root.CreatePage("Spoofing", Color.red);

                // Username Spoof
                var usernamePage = mainPage.CreatePage("Username Spoof", Color.green);
                usernamePage.CreateBool("Enabled", Color.white, UsernameSpoofEnabled,
                    (v) => UsernameSpoofEnabled = v);
                usernamePage.CreateString("Fake Username", Color.cyan, FakeUsername,
                    (v) => FakeUsername = v);

                // Nickname Spoof
                var nicknamePage = mainPage.CreatePage("Nickname Spoof", Color.cyan);
                nicknamePage.CreateBool("Enabled", Color.white, NicknameSpoofEnabled,
                    (v) => NicknameSpoofEnabled = v);
                nicknamePage.CreateString("Fake Nickname", Color.cyan, FakeNickname,
                    (v) => FakeNickname = v);

                // Description Spoof
                var descPage = mainPage.CreatePage("Description Spoof", Color.magenta);
                descPage.CreateBool("Enabled", Color.white, DescriptionSpoofEnabled,
                    (v) => DescriptionSpoofEnabled = v);
                descPage.CreateString("Fake Description", Color.cyan, FakeDescription,
                    (v) => FakeDescription = v);

                // Panic button
                mainPage.CreateFunction("Disable ALL Spoofs", Color.red, () =>
                {
                    UsernameSpoofEnabled = false;
                    NicknameSpoofEnabled = false;
                    DescriptionSpoofEnabled = false;
                    Log.Msg("All spoofs disabled");
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"BoneMenu setup failed: {ex.Message}");
            }
        }
    }
}
