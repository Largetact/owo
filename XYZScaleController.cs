using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.VRMK;
using System;
using System.Linq;
using System.Reflection;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Independent X/Y/Z avatar scale with LabFusion sync.
    /// Sets avatar transform.localScale and triggers Fusion resync.
    /// </summary>
    public static class XYZScaleController
    {
        private static bool _enabled = false;
        private static float _scaleX = 1f;
        private static float _scaleY = 1f;
        private static float _scaleZ = 1f;
        private static bool _applyingScale = false;
        private static Vector3 _originalAvatarScale = Vector3.one;
        private static string _originalAvatarBarcode = null;

        // Fusion sync reflection cache
        private static bool _fusionResolved = false;
        private static Type _serializedAvatarStatsType;
        private static ConstructorInfo _statsCtorAvatar;
        private static Type _playerSenderType;
        private static MethodInfo _sendPlayerAvatarMethod;
        private static Type _rigDataType;
        private static PropertyInfo _rigAvatarStatsProp;
        private static PropertyInfo _rigAvatarIdProp;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                bool wasEnabled = _enabled;
                _enabled = value;
                if (value)
                    ApplyScale();
                else if (wasEnabled)
                    ResetScale();
            }
        }

        public static float ScaleX
        {
            get => _scaleX;
            set => _scaleX = Mathf.Clamp(value, 0.1f, 30f);
        }

        public static float ScaleY
        {
            get => _scaleY;
            set => _scaleY = Mathf.Clamp(value, 0.1f, 30f);
        }

        public static float ScaleZ
        {
            get => _scaleZ;
            set => _scaleZ = Mathf.Clamp(value, 0.1f, 30f);
        }

        public static void Initialize()
        {
            Hooking.OnSwitchAvatarPostfix += OnAvatarSwapped;
            Main.MelonLog.Msg("XYZScaleController initialized");
        }

        public static void OnLevelUnloaded()
        {
            // Scale will be re-applied on next avatar swap via hook
        }

        /// <summary>
        /// Called after any avatar swap — captures original scale, re-applies our custom scale.
        /// </summary>
        private static void OnAvatarSwapped(Avatar avatar)
        {
            if (_applyingScale) return;

            try
            {
                var rm = Player.RigManager;
                if (rm != null && rm.avatar != null)
                {
                    // Get the current avatar barcode
                    string barcode = null;
                    try { barcode = ((ScannableReference)rm.AvatarCrate).Barcode.ID; }
                    catch { try { barcode = rm.avatarID; } catch { } }

                    // Only capture original scale on a genuinely new avatar
                    if (barcode != _originalAvatarBarcode)
                    {
                        _originalAvatarScale = ((Component)rm.avatar).transform.localScale;
                        _originalAvatarBarcode = barcode;
                    }
                }
            }
            catch { }

            if (_enabled)
                ApplyScale();
        }

        /// <summary>
        /// Apply the current XYZ scale to the player avatar and sync with Fusion.
        /// </summary>
        public static void ApplyScale()
        {
            if (!_enabled) return;

            _applyingScale = true;
            try
            {
                var rm = Player.RigManager;
                if (rm == null || rm.avatar == null) return;

                var avatarTransform = ((Component)rm.avatar).transform;
                avatarTransform.localScale = new Vector3(
                    _originalAvatarScale.x * _scaleX,
                    _originalAvatarScale.y * _scaleY,
                    _originalAvatarScale.z * _scaleZ
                );

                SyncWithFusion(rm);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[XYZScale] ApplyScale error: {ex.Message}");
            }
            finally
            {
                _applyingScale = false;
            }
        }

        /// <summary>
        /// Reset avatar scale by re-swapping the current avatar crate.
        /// Simply restoring localScale leaves the physics rig out of sync,
        /// which breaks bodylog interaction.  A full re-swap forces the game
        /// to rebuild the rig, colliders and interaction zones properly.
        /// </summary>
        private static void ResetScale()
        {
            _applyingScale = true;
            try
            {
                var rm = Player.RigManager;
                if (rm == null || rm.avatar == null) return;

                // Restore visual scale immediately so the Fusion sync reads 1:1
                ((Component)rm.avatar).transform.localScale = _originalAvatarScale;

                // Re-swap the same avatar to force a full rig rebuild
                string barcode = null;
                try { barcode = ((ScannableReference)rm.AvatarCrate).Barcode.ID; } catch { }
                if (barcode == null) try { barcode = rm.avatarID; } catch { }

                if (!string.IsNullOrEmpty(barcode))
                {
                    rm.SwapAvatarCrate(new Il2CppSLZ.Marrow.Warehouse.Barcode(barcode), false, (Il2CppSystem.Action<bool>)null);
                }

                SyncWithFusion(rm);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[XYZScale] ResetScale error: {ex.Message}");
            }
            finally
            {
                _applyingScale = false;
            }
        }

        /// <summary>
        /// Trigger Fusion to resync avatar stats (which includes localScale).
        /// Replicates what LocalAvatar.OnCheckAvatar does:
        ///   1. new SerializedAvatarStats(avatar)
        ///   2. RigData.RigAvatarStats = stats
        ///   3. PlayerSender.SendPlayerAvatar(stats, barcode)
        /// </summary>
        private static void SyncWithFusion(RigManager rm)
        {
            try
            {
                ResolveFusionTypes();
                if (_sendPlayerAvatarMethod == null || _statsCtorAvatar == null) return;

                // Get current avatar barcode
                string barcode = null;
                try
                {
                    barcode = ((ScannableReference)rm.AvatarCrate).Barcode.ID;
                }
                catch
                {
                    try { barcode = rm.avatarID; } catch { }
                }

                if (string.IsNullOrEmpty(barcode)) return;

                // Create SerializedAvatarStats from current avatar (reads localScale)
                var stats = _statsCtorAvatar.Invoke(new object[] { rm.avatar });
                if (stats == null) return;

                // Update RigData cached values
                if (_rigAvatarStatsProp != null)
                    _rigAvatarStatsProp.SetValue(null, stats);
                if (_rigAvatarIdProp != null)
                    _rigAvatarIdProp.SetValue(null, barcode);

                // Send to other players
                _sendPlayerAvatarMethod.Invoke(null, new object[] { stats, barcode });
            }
            catch (Exception ex)
            {
                // Fusion not loaded or not in a server — silently ignore
                Main.MelonLog.Msg($"[XYZScale] Fusion sync skipped: {ex.Message}");
            }
        }

        private static void ResolveFusionTypes()
        {
            if (_fusionResolved) return;
            _fusionResolved = true;

            try
            {
                _serializedAvatarStatsType = FindType("SerializedAvatarStats");
                if (_serializedAvatarStatsType != null)
                {
                    _statsCtorAvatar = _serializedAvatarStatsType.GetConstructor(
                        new[] { typeof(Avatar) });
                }

                _playerSenderType = FindType("PlayerSender");
                if (_playerSenderType != null)
                {
                    _sendPlayerAvatarMethod = _playerSenderType.GetMethod("SendPlayerAvatar",
                        BindingFlags.Public | BindingFlags.Static);
                }

                _rigDataType = FindType("RigData");
                if (_rigDataType != null)
                {
                    _rigAvatarStatsProp = _rigDataType.GetProperty("RigAvatarStats",
                        BindingFlags.Public | BindingFlags.Static);
                    _rigAvatarIdProp = _rigDataType.GetProperty("RigAvatarId",
                        BindingFlags.Public | BindingFlags.Static);
                }

                Main.MelonLog.Msg($"[XYZScale] Fusion types resolved: stats={_statsCtorAvatar != null}, send={_sendPlayerAvatarMethod != null}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[XYZScale] Fusion type resolution failed: {ex.Message}");
            }
        }

        private static Type FindType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.Name == typeName);
        }
    }
}
