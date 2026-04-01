using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Persists all menu toggle/slider settings to a custom config file.
    /// Stored in: BONELAB/UserData/DooberUtils/config.cfg
    /// Uses a simple INI-like format. Presets stored as [Preset.N] sections.
    /// Call Initialize() at mod init, LoadAll() after controllers, SaveAll() periodically.
    /// </summary>
    public static class SettingsManager
    {
        private static string _configDir;
        private static string _configPath;

        private static bool _initialized = false;
        private static float _lastSaveTime = 0f;
        private static float _saveInterval = 5f; // save every 5 seconds if dirty
        private static bool _dirty = false;

        // Section -> Key -> Value storage
        private static Dictionary<string, Dictionary<string, string>> _sections
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        // ─── Initialization ───

        /// <summary>
        /// Set up config directory and file path. Call once at mod init BEFORE LoadAll().
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // MelonLoader standard: <GameDir>/UserData/
                string userDataDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData");
                _configDir = Path.Combine(userDataDir, "DooberUtils");
                _configPath = Path.Combine(_configDir, "config.cfg");

                if (!Directory.Exists(_configDir))
                    Directory.CreateDirectory(_configDir);

                _initialized = true;
                Main.MelonLog.Msg($"SettingsManager initialized - {_configPath}");
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"SettingsManager init error: {ex.Message}");
            }
        }

        // ─── Value Helpers ───

        private static string GetValue(string section, string key, string defaultValue)
        {
            if (_sections.TryGetValue(section, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            return defaultValue;
        }

        private static bool GetBool(string section, string key, bool defaultValue)
        {
            var val = GetValue(section, key, null);
            if (val == null) return defaultValue;
            return val.Equals("True", StringComparison.OrdinalIgnoreCase)
                || val.Equals("1", StringComparison.Ordinal);
        }

        private static float GetFloat(string section, string key, float defaultValue)
        {
            var val = GetValue(section, key, null);
            if (val == null) return defaultValue;
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }

        private static int GetInt(string section, string key, int defaultValue)
        {
            var val = GetValue(section, key, null);
            if (val == null) return defaultValue;
            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;
            return defaultValue;
        }

        private static void SetValue(string section, string key, string value)
        {
            if (!_sections.ContainsKey(section))
                _sections[section] = new Dictionary<string, string>(StringComparer.Ordinal);
            _sections[section][key] = value ?? "";
        }

        private static void SetBool(string section, string key, bool value)
            => SetValue(section, key, value ? "True" : "False");

        private static void SetFloat(string section, string key, float value)
            => SetValue(section, key, value.ToString(CultureInfo.InvariantCulture));

        private static void SetInt(string section, string key, int value)
            => SetValue(section, key, value.ToString(CultureInfo.InvariantCulture));

        // ─── File I/O ───

        /// <summary>
        /// Parse the INI-like config file into the _sections dictionary.
        /// Falls back to .bak if main file is missing or corrupt.
        /// </summary>
        private static void ReadConfigFile()
        {
            _sections.Clear();

            string pathToRead = _configPath;
            if (!File.Exists(pathToRead))
            {
                // Try backup if main config is missing
                string backupPath = _configPath + ".bak";
                if (File.Exists(backupPath))
                {
                    Main.MelonLog.Msg("Config file missing — restoring from backup");
                    pathToRead = backupPath;
                }
                else
                {
                    return;
                }
            }

            string currentSection = "";
            foreach (var rawLine in File.ReadAllLines(pathToRead))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    if (!_sections.ContainsKey(currentSection))
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.Ordinal);
                    continue;
                }

                int eqIdx = line.IndexOf('=');
                if (eqIdx > 0 && !string.IsNullOrEmpty(currentSection))
                {
                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1); // preserve value exactly
                    if (!_sections.ContainsKey(currentSection))
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.Ordinal);
                    _sections[currentSection][key] = val;
                }
            }

            // If we read from backup, write it as the main config
            if (pathToRead != _configPath)
            {
                try { File.Copy(pathToRead, _configPath, true); } catch { }
            }
        }

        /// <summary>
        /// Write all sections to the config file in a readable order.
        /// </summary>
        private static void WriteConfigFile()
        {
            // Create backup before writing (preserves last save in case new version corrupts data)
            try
            {
                if (File.Exists(_configPath))
                {
                    string backupPath = _configPath + ".bak";
                    File.Copy(_configPath, backupPath, true);
                }
            }
            catch { }

            var lines = new List<string>();
            lines.Add("# OwO Utils Settings");
            lines.Add("# Auto-generated - edit with care");
            lines.Add("# Backup saved as config.cfg.bak before each write");
            lines.Add("");

            // Fixed section order for readability
            string[] orderedSections =
            {
                "Global", "Player", "Combat", "CrazyGuns", "Dash", "Flight", "GravityBoots", "Ragdoll", "ExplosivePunch", "GroundPound",
                "ExplosiveImpact", "DespawnAll", "ObjectLauncher", "ForceGrab",
                "WaypointProjectile", "DropOnPlayer", "ServerQueue",
                "BodyLogColor", "ScreenShare", "Keybinds", "CosmeticPresets", "Favorites",
                "AutoRun", "Spinbot", "BunnyHop", "DefaultWorld", "AutoHost", "XYZScale", "DisableAvatarFX", "HolsterHider"
            };

            var written = new HashSet<string>(StringComparer.Ordinal);

            foreach (var section in orderedSections)
            {
                if (_sections.TryGetValue(section, out var dict))
                {
                    WriteSection(lines, section, dict);
                    written.Add(section);
                }
            }

            // Write Preset.N sections (sorted numerically)
            var presetSections = new List<string>();
            foreach (var key in _sections.Keys)
            {
                if (key.StartsWith("Preset.") && !written.Contains(key))
                    presetSections.Add(key);
            }
            presetSections.Sort((a, b) =>
            {
                int aIdx = 0, bIdx = 0;
                int.TryParse(a.Substring(7), out aIdx);
                int.TryParse(b.Substring(7), out bIdx);
                return aIdx.CompareTo(bIdx);
            });
            foreach (var section in presetSections)
            {
                WriteSection(lines, section, _sections[section]);
                written.Add(section);
            }

            // Any remaining sections
            foreach (var kvp in _sections)
            {
                if (!written.Contains(kvp.Key))
                    WriteSection(lines, kvp.Key, kvp.Value);
            }

            File.WriteAllLines(_configPath, lines);
        }

        private static void WriteSection(List<string> lines, string section, Dictionary<string, string> values)
        {
            lines.Add($"[{section}]");
            foreach (var kvp in values)
                lines.Add($"{kvp.Key}={kvp.Value}");
            lines.Add("");
        }

        // ─── Preset I/O ───

        /// <summary>
        /// Read [Preset.N] sections from config into ObjectLauncherController.
        /// </summary>
        private static void LoadPresets()
        {
            var list = new List<ObjectLauncherController.LauncherPreset>();
            int i = 0;
            while (_sections.ContainsKey($"Preset.{i}"))
            {
                string s = $"Preset.{i}";
                try
                {
                    list.Add(new ObjectLauncherController.LauncherPreset
                    {
                        Name = GetValue(s, "Name", $"Preset {i}"),
                        BarcodeID = GetValue(s, "BarcodeID", ""),
                        ItemName = GetValue(s, "ItemName", ""),
                        LaunchForce = GetFloat(s, "LaunchForce", 1000f),
                        SpawnDistance = GetFloat(s, "SpawnDistance", 1.5f),
                        SpawnOffsetX = GetFloat(s, "SpawnOffsetX", GetFloat(s, "SpawnOffsetH", 0f)),
                        SpawnOffsetY = GetFloat(s, "SpawnOffsetY", GetFloat(s, "SpawnOffsetV", 0f)),
                        SpinVelocity = GetFloat(s, "SpinVelocity", 0f),
                        RotationX = GetFloat(s, "RotationX", 0f),
                        RotationY = GetFloat(s, "RotationY", 0f),
                        RotationZ = GetFloat(s, "RotationZ", 0f),
                        Scale = GetFloat(s, "Scale", 1f)
                    });
                }
                catch (Exception ex)
                {
                    Main.MelonLog.Warning($"[Preset] Failed to parse {s}: {ex.Message}");
                }
                i++;
            }
            ObjectLauncherController.SetPresets(list);
        }

        /// <summary>
        /// Write current presets to [Preset.N] sections in the dictionary.
        /// </summary>
        private static void SavePresets()
        {
            // Remove old Preset.N sections
            var toRemove = new List<string>();
            foreach (var key in _sections.Keys)
            {
                if (key.StartsWith("Preset."))
                    toRemove.Add(key);
            }
            foreach (var key in toRemove)
                _sections.Remove(key);

            // Write current presets
            var presets = ObjectLauncherController.GetPresets();
            for (int i = 0; i < presets.Count; i++)
            {
                var p = presets[i];
                string s = $"Preset.{i}";
                SetValue(s, "Name", p.Name ?? "");
                SetValue(s, "BarcodeID", p.BarcodeID ?? "");
                SetValue(s, "ItemName", p.ItemName ?? "");
                SetFloat(s, "LaunchForce", p.LaunchForce);
                SetFloat(s, "SpawnDistance", p.SpawnDistance);
                SetFloat(s, "SpawnOffsetX", p.SpawnOffsetX);
                SetFloat(s, "SpawnOffsetY", p.SpawnOffsetY);
                SetFloat(s, "SpinVelocity", p.SpinVelocity);
                SetFloat(s, "RotationX", p.RotationX);
                SetFloat(s, "RotationY", p.RotationY);
                SetFloat(s, "RotationZ", p.RotationZ);
                SetFloat(s, "Scale", p.Scale);
            }
        }

        // ─── Load / Save ───

        /// <summary>
        /// Load all saved settings from config file and apply to controllers.
        /// Call AFTER controllers are initialized.
        /// </summary>
        public static void LoadAll()
        {
            if (!_initialized) return;

            try
            {
                ReadConfigFile();
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"SettingsManager failed to read config: {ex.Message}");
                return;
            }

            SafeExecute(() =>
            {
                NotificationHelper.NotificationsEnabled = GetBool("Global", "Notifications", true);
            }, "Load Global");

            SafeExecute(() =>
            {
                GodModeController.IsGodModeEnabled = GetBool("Player", "GodMode", false);
                AntiConstraintController.IsEnabled = GetBool("Player", "AntiConstraint", false);
                AntiKnockoutController.IsEnabled = GetBool("Player", "AntiKnockout", false);
                UnbreakableGripController.IsEnabled = GetBool("Player", "UnbreakableGrip", false);
                AntiGravityChangeController.Enabled = GetBool("Player", "AntiGravityChange", false);
            }, "Load Player");

            SafeExecute(() =>
            {
                FullAutoController.IsFullAutoEnabled = GetBool("Combat", "FullAuto", false);
                InfiniteAmmoController.IsEnabled = GetBool("Combat", "InfiniteAmmo", false);
            }, "Load Combat");

            SafeExecute(() =>
            {
                ChaosGunController.PurpleGuns = GetBool("CrazyGuns", "PurpleGuns", false);
                ChaosGunController.InsaneDamage = GetBool("CrazyGuns", "InsaneDamage", false);
                ChaosGunController.NoRecoil = GetBool("CrazyGuns", "NoRecoil", false);
                ChaosGunController.InsaneFirerate = GetBool("CrazyGuns", "InsaneFirerate", false);
                ChaosGunController.NoWeight = GetBool("CrazyGuns", "NoWeight", false);
                ChaosGunController.GunsBounce = GetBool("CrazyGuns", "GunsBounce", false);
                ChaosGunController.NoReload = GetBool("CrazyGuns", "NoReload", false);
            }, "Load Crazy Guns");

            SafeExecute(() =>
            {
                DashController.IsDashEnabled = GetBool("Dash", "Enabled", false);
                DashController.DashForce = GetFloat("Dash", "Force", 10f);
                DashController.IsDashInstantaneous = GetBool("Dash", "Instant", true);
                DashController.IsDashContinuous = GetBool("Dash", "Continuous", false);
                DashController.IsHandOriented = GetBool("Dash", "HandOriented", false);
                DashController.UseLeftHand = GetBool("Dash", "LeftHand", false);
                DashController.LockOnEnabled = GetBool("Dash", "LockOn", false);
                DashController.LockOnFilter = (TargetFilter)GetInt("Dash", "LockOnFilter", (int)TargetFilter.NEAREST);
                DashController.LookAtTarget = GetBool("Dash", "LookAtTarget", false);
                DashController.LookAtHead = GetBool("Dash", "LookAtHead", false);
                DashController.KillVelocityOnLand = GetBool("Dash", "KillVelocityOnLand", false);
                // Dash effects
                DashController.EffectEnabled = GetBool("Dash", "EffectEnabled", false);
                DashController.EffectBarcode = GetValue("Dash", "EffectBarcode", "");
                DashController.SmashBoneEnabled = GetBool("Dash", "SmashBone", false);
                DashController.SmashBoneCount = GetInt("Dash", "SmashBoneCount", 1);
                DashController.SmashBoneFlip = GetBool("Dash", "SmashBoneFlip", false);
                DashController.CosmeticEnabled = GetBool("Dash", "CosmeticEnabled", false);
                DashController.CosmeticBarcode = GetValue("Dash", "CosmeticBarcode", "");
                DashController.CosmeticCount = GetInt("Dash", "CosmeticCount", 1);
                DashController.CosmeticFlip = GetBool("Dash", "CosmeticFlip", false);
                DashController.EffectSpawnDelay = GetFloat("Dash", "EffectSpawnDelay", 0f);
                DashController.EffectSpawnInterval = GetFloat("Dash", "EffectSpawnInterval", 0f);
                DashController.EffectOffsetX = GetFloat("Dash", "EffectOffsetX", 0f);
                DashController.EffectOffsetY = GetFloat("Dash", "EffectOffsetY", 0f);
                DashController.EffectOffsetZ = GetFloat("Dash", "EffectOffsetZ", 0f);
                DashController.EffectMatrixCount = GetInt("Dash", "EffectMatrixCount", 1);
                DashController.EffectMatrixSpacing = GetFloat("Dash", "EffectMatrixSpacing", 0.5f);
                DashController.EffectMatrixMode = (MatrixMode)GetInt("Dash", "EffectMatrixMode", (int)MatrixMode.SQUARE);
            }, "Load Dash");

            SafeExecute(() =>
            {
                FlightController.Enabled = GetBool("Flight", "Enabled", false);
                FlightController.SpeedMultiplier = GetFloat("Flight", "SpeedMultiplier", 1f);
                FlightController.AccelerationEnabled = GetBool("Flight", "Acceleration", false);
                FlightController.AccelerationRate = GetFloat("Flight", "AccelerationRate", 1.5f);
                FlightController.MomentumEnabled = GetBool("Flight", "Momentum", false);
                FlightController.LockOnEnabled = GetBool("Flight", "LockOn", false);
                FlightController.LockOnFilter = (TargetFilter)GetInt("Flight", "LockOnFilter", (int)TargetFilter.NEAREST);
                FlightController.LookAtTarget = GetBool("Flight", "LookAtTarget", false);
                FlightController.LookAtHead = GetBool("Flight", "LookAtHead", false);
                // Flight effects
                FlightController.EffectEnabled = GetBool("Flight", "EffectEnabled", false);
                FlightController.EffectBarcode = GetValue("Flight", "EffectBarcode", "");
                FlightController.SmashBoneEnabled = GetBool("Flight", "SmashBone", false);
                FlightController.SmashBoneCount = GetInt("Flight", "SmashBoneCount", 1);
                FlightController.SmashBoneFlip = GetBool("Flight", "SmashBoneFlip", false);
                FlightController.CosmeticEnabled = GetBool("Flight", "CosmeticEnabled", false);
                FlightController.CosmeticBarcode = GetValue("Flight", "CosmeticBarcode", "");
                FlightController.CosmeticCount = GetInt("Flight", "CosmeticCount", 1);
                FlightController.CosmeticFlip = GetBool("Flight", "CosmeticFlip", false);
                FlightController.EffectSpawnDelay = GetFloat("Flight", "EffectSpawnDelay", 0f);
                FlightController.EffectSpawnInterval = GetFloat("Flight", "EffectSpawnInterval", 0f);
                FlightController.EffectOffsetX = GetFloat("Flight", "EffectOffsetX", 0f);
                FlightController.EffectOffsetY = GetFloat("Flight", "EffectOffsetY", 0f);
                FlightController.EffectOffsetZ = GetFloat("Flight", "EffectOffsetZ", 0f);
                FlightController.EffectHandOriented = GetBool("Flight", "EffectHandOriented", false);
                FlightController.EffectUseLeftHand = GetBool("Flight", "EffectUseLeftHand", false);
                FlightController.EffectMatrixCount = GetInt("Flight", "EffectMatrixCount", 1);
                FlightController.EffectMatrixSpacing = GetFloat("Flight", "EffectMatrixSpacing", 0.5f);
                FlightController.EffectMatrixMode = (MatrixMode)GetInt("Flight", "EffectMatrixMode", (int)MatrixMode.SQUARE);
            }, "Load Flight");

            SafeExecute(() =>
            {
                GravityBootsController.Enabled = GetBool("GravityBoots", "Enabled", false);
                GravityBootsController.GravityStrength = GetFloat("GravityBoots", "GravityStrength", 9.81f);
                GravityBootsController.SurfaceDetectRange = GetFloat("GravityBoots", "SurfaceDetectRange", 3f);
                GravityBootsController.RotationSpeed = GetFloat("GravityBoots", "RotationSpeed", 5f);
                GravityBootsController.StickForce = GetFloat("GravityBoots", "StickForce", 20f);
            }, "Load Gravity Boots");

            SafeExecute(() =>
            {
                RagdollController.Enabled = GetBool("Ragdoll", "Enabled", false);
                RagdollController.GrabEnabled = GetBool("Ragdoll", "GrabEnabled", true);
                RagdollController.NeckGrabDisablesArms = GetBool("Ragdoll", "NeckGrabDisablesArms", true);
                RagdollController.ArmGrabEnabled = GetBool("Ragdoll", "ArmGrabEnabled", true);
                RagdollController.Mode = (RagdollMode)GetInt("Ragdoll", "Mode", (int)RagdollMode.LIMP);
                RagdollController.TantrumMode = GetBool("Ragdoll", "TantrumMode", false);
                RagdollController.Binding = (RagdollBinding)GetInt("Ragdoll", "Binding", (int)RagdollBinding.NONE);
                RagdollController.KeybindHand = (RagdollHand)GetInt("Ragdoll", "KeybindHand", (int)RagdollHand.RIGHT_HAND);
                RagdollController.FallEnabled = GetBool("Ragdoll", "FallEnabled", true);
                RagdollController.FallVelocityThreshold = GetFloat("Ragdoll", "FallVelocity", 10f);
                RagdollController.ImpactEnabled = GetBool("Ragdoll", "ImpactEnabled", true);
                RagdollController.ImpactThreshold = GetFloat("Ragdoll", "ImpactThreshold", 10f);
                RagdollController.LaunchEnabled = GetBool("Ragdoll", "LaunchEnabled", true);
                RagdollController.LaunchThreshold = GetFloat("Ragdoll", "LaunchThreshold", 10f);
                RagdollController.SlipEnabled = GetBool("Ragdoll", "SlipEnabled", false);
                RagdollController.SlipFrictionThreshold = GetFloat("Ragdoll", "SlipFriction", 0.15f);
                RagdollController.SlipVelocityThreshold = GetFloat("Ragdoll", "SlipVelocity", 3f);
                RagdollController.WallPushEnabled = GetBool("Ragdoll", "WallPushEnabled", true);
                RagdollController.WallPushVelocityThreshold = GetFloat("Ragdoll", "WallPushVelocity", 3.2f);
            }, "Load Ragdoll");

            SafeExecute(() =>
            {
                ExplosivePunchController.IsExplosivePunchEnabled = GetBool("ExplosivePunch", "Enabled", false);
                ExplosivePunchController.IsSuperExplosivePunchEnabled = GetBool("ExplosivePunch", "SuperExplosive", false);
                ExplosivePunchController.IsBlackFlashEnabled = GetBool("ExplosivePunch", "BlackFlash", false);
                ExplosivePunchController.IsTinyExplosiveEnabled = GetBool("ExplosivePunch", "TinyExplosive", false);
                ExplosivePunchController.IsBoomEnabled = GetBool("ExplosivePunch", "Boom", false);
                ExplosivePunchController.IsCustomPunchEnabled = GetBool("ExplosivePunch", "CustomPunch", false);
                ExplosivePunchController.CustomPunchBarcode = GetValue("ExplosivePunch", "CustomPunchBarcode", "");
                ExplosivePunchController.SpawnDelay = GetFloat("ExplosivePunch", "SpawnDelay", 0f);
                ExplosivePunchController.PunchVelocityThreshold = GetFloat("ExplosivePunch", "PunchSpeed", 6f);
                ExplosivePunchController.PunchCooldown = GetFloat("ExplosivePunch", "PunchCooldown", 0.2f);
                ExplosivePunchController.RigCheckOnly = GetBool("ExplosivePunch", "RigCheckOnly", true);
                ExplosivePunchController.FaceTarget = GetBool("ExplosivePunch", "FaceTarget", false);
                ExplosivePunchController.IsLegacyPunchEnabled = GetBool("ExplosivePunch", "LegacyPunch", false);
                ExplosivePunchController.IsSmashBoneEnabled = GetBool("ExplosivePunch", "SmashBone", false);
                ExplosivePunchController.SmashBoneCount = GetInt("ExplosivePunch", "SmashBoneCount", 1);
                ExplosivePunchController.SmashBoneFlip = GetBool("ExplosivePunch", "SmashBoneFlip", false);
                ExplosivePunchController.PunchMode = (PunchHandMode)GetInt("ExplosivePunch", "PunchMode", (int)PunchHandMode.BOTH);
                ExplosivePunchController.IsCosmeticEnabled = GetBool("ExplosivePunch", "CosmeticEnabled", false);
                ExplosivePunchController.CosmeticBarcode = GetValue("ExplosivePunch", "CosmeticBarcode", "");
                ExplosivePunchController.CosmeticCount = GetInt("ExplosivePunch", "CosmeticCount", 1);
                ExplosivePunchController.CosmeticFlip = GetBool("ExplosivePunch", "CosmeticFlip", false);
                ExplosivePunchController.PunchSpawnCount = GetInt("ExplosivePunch", "PunchSpawnCount", 1);
                ExplosivePunchController.PunchSpacing = GetFloat("ExplosivePunch", "PunchSpacing", 0.5f);
                ExplosivePunchController.PunchMatrixMode = (MatrixMode)GetInt("ExplosivePunch", "PunchMatrixMode", (int)MatrixMode.SQUARE);
                // Per-hand left
                ExplosivePunchController.LeftExplosionType = (ExplosionType)GetInt("ExplosivePunch", "LeftExplosionType", (int)ExplosionType.Normal);
                ExplosivePunchController.LeftCustomBarcode = GetValue("ExplosivePunch", "LeftCustomBarcode", "");
                ExplosivePunchController.LeftSmashBoneEnabled = GetBool("ExplosivePunch", "LeftSmashBone", false);
                ExplosivePunchController.LeftSmashBoneCount = GetInt("ExplosivePunch", "LeftSmashBoneCount", 1);
                ExplosivePunchController.LeftSmashBoneFlip = GetBool("ExplosivePunch", "LeftSmashBoneFlip", false);
                ExplosivePunchController.LeftCosmeticEnabled = GetBool("ExplosivePunch", "LeftCosmeticEnabled", false);
                ExplosivePunchController.LeftCosmeticBarcode = GetValue("ExplosivePunch", "LeftCosmeticBarcode", "");
                ExplosivePunchController.LeftCosmeticCount = GetInt("ExplosivePunch", "LeftCosmeticCount", 1);
                ExplosivePunchController.LeftCosmeticFlip = GetBool("ExplosivePunch", "LeftCosmeticFlip", false);
                // Per-hand right
                ExplosivePunchController.RightExplosionType = (ExplosionType)GetInt("ExplosivePunch", "RightExplosionType", (int)ExplosionType.Normal);
                ExplosivePunchController.RightCustomBarcode = GetValue("ExplosivePunch", "RightCustomBarcode", "");
                ExplosivePunchController.RightSmashBoneEnabled = GetBool("ExplosivePunch", "RightSmashBone", false);
                ExplosivePunchController.RightSmashBoneCount = GetInt("ExplosivePunch", "RightSmashBoneCount", 1);
                ExplosivePunchController.RightSmashBoneFlip = GetBool("ExplosivePunch", "RightSmashBoneFlip", false);
                ExplosivePunchController.RightCosmeticEnabled = GetBool("ExplosivePunch", "RightCosmeticEnabled", false);
                ExplosivePunchController.RightCosmeticBarcode = GetValue("ExplosivePunch", "RightCosmeticBarcode", "");
                ExplosivePunchController.RightCosmeticCount = GetInt("ExplosivePunch", "RightCosmeticCount", 1);
                ExplosivePunchController.RightCosmeticFlip = GetBool("ExplosivePunch", "RightCosmeticFlip", false);
            }, "Load Explosive Punch");

            SafeExecute(() =>
            {
                GroundPoundController.Enabled = GetBool("GroundPound", "Enabled", false);
                GroundPoundController.VelocityThreshold = GetFloat("GroundPound", "VelocityThreshold", 5f);
                GroundPoundController.Cooldown = GetFloat("GroundPound", "Cooldown", 0.5f);
                GroundPoundController.SpawnDelay = GetFloat("GroundPound", "SpawnDelay", 0f);
                GroundPoundController.SelectedExplosion = (ExplosionType)GetInt("GroundPound", "ExplosionType", (int)ExplosionType.Normal);
                GroundPoundController.CustomBarcode = GetValue("GroundPound", "CustomBarcode", "");
                GroundPoundController.SmashBoneEnabled = GetBool("GroundPound", "SmashBone", false);
                GroundPoundController.SmashBoneCount = GetInt("GroundPound", "SmashBoneCount", 1);
                GroundPoundController.SmashBoneFlip = GetBool("GroundPound", "SmashBoneFlip", false);
                GroundPoundController.CosmeticEnabled = GetBool("GroundPound", "CosmeticEnabled", false);
                GroundPoundController.CosmeticBarcode = GetValue("GroundPound", "CosmeticBarcode", "");
                GroundPoundController.CosmeticCount = GetInt("GroundPound", "CosmeticCount", 1);
                GroundPoundController.CosmeticFlip = GetBool("GroundPound", "CosmeticFlip", false);
                GroundPoundController.MatrixCount = GetInt("GroundPound", "MatrixCount", 1);
                GroundPoundController.MatrixSpacing = GetFloat("GroundPound", "MatrixSpacing", 0.5f);
                GroundPoundController.SelectedMatrixMode = (MatrixMode)GetInt("GroundPound", "MatrixMode", (int)MatrixMode.SQUARE);
            }, "Load Ground Slam");

            SafeExecute(() =>
            {
                ExplosiveImpactController.Enabled = GetBool("ExplosiveImpact", "Enabled", false);
                ExplosiveImpactController.VelocityThreshold = GetFloat("ExplosiveImpact", "VelocityThreshold", 8f);
                ExplosiveImpactController.Cooldown = GetFloat("ExplosiveImpact", "Cooldown", 0.5f);
                ExplosiveImpactController.SpawnDelay = GetFloat("ExplosiveImpact", "SpawnDelay", 0f);
                ExplosiveImpactController.SelectedExplosion = (ExplosionType)GetInt("ExplosiveImpact", "ExplosionType", (int)ExplosionType.Normal);
                ExplosiveImpactController.CustomBarcode = GetValue("ExplosiveImpact", "CustomBarcode", "");
                ExplosiveImpactController.SmashBoneEnabled = GetBool("ExplosiveImpact", "SmashBone", false);
                ExplosiveImpactController.SmashBoneCount = GetInt("ExplosiveImpact", "SmashBoneCount", 1);
                ExplosiveImpactController.SmashBoneFlip = GetBool("ExplosiveImpact", "SmashBoneFlip", false);
                ExplosiveImpactController.CosmeticEnabled = GetBool("ExplosiveImpact", "CosmeticEnabled", false);
                ExplosiveImpactController.CosmeticBarcode = GetValue("ExplosiveImpact", "CosmeticBarcode", "");
                ExplosiveImpactController.CosmeticCount = GetInt("ExplosiveImpact", "CosmeticCount", 1);
                ExplosiveImpactController.CosmeticFlip = GetBool("ExplosiveImpact", "CosmeticFlip", false);
                ExplosiveImpactController.MatrixCount = GetInt("ExplosiveImpact", "MatrixCount", 1);
                ExplosiveImpactController.MatrixSpacing = GetFloat("ExplosiveImpact", "MatrixSpacing", 0.5f);
                ExplosiveImpactController.SelectedMatrixMode = (MatrixMode)GetInt("ExplosiveImpact", "MatrixMode", (int)MatrixMode.SQUARE);
            }, "Load Explosive Impact");

            SafeExecute(() =>
            {
                AntiDespawnController.Enabled = GetBool("AntiDespawn", "Enabled", false);
            }, "Load Anti-Despawn");

            SafeExecute(() =>
            {
                AntiGrabController.Enabled = GetBool("AntiGrab", "Enabled", false);
            }, "Load Anti-Grab");

            SafeExecute(() =>
            {
                SpawnLimiterController.Enabled = GetBool("SpawnLimiter", "Enabled", true);
                SpawnLimiterController.SpawnDelay = GetFloat("SpawnLimiter", "SpawnDelay", 0.75f);
                SpawnLimiterController.MaxPerFrame = GetInt("SpawnLimiter", "MaxPerFrame", 5);
            }, "Load Spawn Limiter");

            SafeExecute(() =>
            {
                BodyLogColorController.Enabled = GetBool("BodyLogColor", "Enabled", false);
                BodyLogColorController.BodyLogR = GetFloat("BodyLogColor", "BodyLogR", 255f);
                BodyLogColorController.BodyLogG = GetFloat("BodyLogColor", "BodyLogG", 255f);
                BodyLogColorController.BodyLogB = GetFloat("BodyLogColor", "BodyLogB", 255f);
                BodyLogColorController.BodyLogA = GetFloat("BodyLogColor", "BodyLogA", 255f);
                BodyLogColorController.BallR = GetFloat("BodyLogColor", "BallR", 255f);
                BodyLogColorController.BallG = GetFloat("BodyLogColor", "BallG", 255f);
                BodyLogColorController.BallB = GetFloat("BodyLogColor", "BallB", 255f);
                BodyLogColorController.BallA = GetFloat("BodyLogColor", "BallA", 255f);
                BodyLogColorController.LineR = GetFloat("BodyLogColor", "LineR", 255f);
                BodyLogColorController.LineG = GetFloat("BodyLogColor", "LineG", 255f);
                BodyLogColorController.LineB = GetFloat("BodyLogColor", "LineB", 255f);
                BodyLogColorController.LineA = GetFloat("BodyLogColor", "LineA", 255f);
                BodyLogColorController.RadialR = GetFloat("BodyLogColor", "RadialR", 255f);
                BodyLogColorController.RadialG = GetFloat("BodyLogColor", "RadialG", 255f);
                BodyLogColorController.RadialB = GetFloat("BodyLogColor", "RadialB", 255f);
                BodyLogColorController.RadialA = GetFloat("BodyLogColor", "RadialA", 255f);
            }, "Load BodyLog Color");

            SafeExecute(() =>
            {
                CosmeticPresetController.DeserializePresets(GetValue("CosmeticPresets", "Data", ""));
            }, "Load Cosmetic Presets");

            SafeExecute(() =>
            {
                DespawnAllController.Filter = (DespawnFilter)GetInt("DespawnAll", "Filter", 0);
                DespawnAllController.AutoDespawnIntervalMins = GetFloat("DespawnAll", "AutoDespawnInterval", 5f);
                DespawnAllController.AutoDespawnEnabled = GetBool("DespawnAll", "AutoDespawn", false);
                DespawnAllController.KeepHolsteredItems = GetBool("DespawnAll", "KeepHolsteredItems", false);
                DespawnAllController.KeepOnlyMyHolsters = GetBool("DespawnAll", "KeepOnlyMyHolsters", true);
            }, "Load Despawn All");

            SafeExecute(() =>
            {
                // Don't load Enabled — always start disabled to prevent accidental auto-enable
                ObjectLauncherController.SafetyEnabled = GetBool("ObjectLauncher", "Safety", true);
                ObjectLauncherController.UseLeftHand = GetBool("ObjectLauncher", "LeftHand", false);
                ObjectLauncherController.IsFullAuto = GetBool("ObjectLauncher", "FullAuto", false);
                ObjectLauncherController.ShowTrajectory = GetBool("ObjectLauncher", "Trajectory", false);
                ObjectLauncherController.FullAutoDelay = GetFloat("ObjectLauncher", "FullAutoDelay", 0.1f);
                ObjectLauncherController.LaunchForce = GetFloat("ObjectLauncher", "LaunchForce", 1000f);
                ObjectLauncherController.SpawnDistance = GetFloat("ObjectLauncher", "SpawnDistance", 1.5f);
                ObjectLauncherController.SpawnOffsetX = GetFloat("ObjectLauncher", "SpawnOffsetX", GetFloat("ObjectLauncher", "SpawnOffsetH", 0f));
                ObjectLauncherController.SpawnOffsetY = GetFloat("ObjectLauncher", "SpawnOffsetY", GetFloat("ObjectLauncher", "SpawnOffsetV", 0f));
                ObjectLauncherController.ProjectileCount = GetInt("ObjectLauncher", "ProjectileCount", 1);
                ObjectLauncherController.ProjectileSpacing = GetFloat("ObjectLauncher", "ProjectileSpacing", 0.5f);
                ObjectLauncherController.SpinVelocity = GetFloat("ObjectLauncher", "SpinVelocity", 0f);
                ObjectLauncherController.SpawnScale = GetFloat("ObjectLauncher", "Scale", 1f);
                ObjectLauncherController.HomingEnabled = GetBool("ObjectLauncher", "Homing", false);
                ObjectLauncherController.HomingFilter = (TargetFilter)GetInt("ObjectLauncher", "HomingFilter", (int)TargetFilter.NEAREST);
                ObjectLauncherController.HomingStrength = GetFloat("ObjectLauncher", "HomingStrength", 5f);
                ObjectLauncherController.HomingDuration = GetFloat("ObjectLauncher", "HomingDuration", 0f);
                ObjectLauncherController.HomingRotationLock = GetBool("ObjectLauncher", "HomingRotationLock", false);
                ObjectLauncherController.HomingSpeed = GetFloat("ObjectLauncher", "HomingSpeed", 0f);
                ObjectLauncherController.HomingAccelEnabled = GetBool("ObjectLauncher", "HomingAccel", false);
                ObjectLauncherController.HomingAccelRate = GetFloat("ObjectLauncher", "HomingAccelRate", 2f);
                ObjectLauncherController.HomingTargetHead = GetBool("ObjectLauncher", "HomingTargetHead", false);
                ObjectLauncherController.HomingMomentum = GetBool("ObjectLauncher", "HomingMomentum", false);
                ObjectLauncherController.HomingStayDuration = GetFloat("ObjectLauncher", "HomingStayDuration", 2f);
                ObjectLauncherController.ForceDelay = GetFloat("ObjectLauncher", "ForceDelay", 0.02f);
                ObjectLauncherController.AutoCleanupEnabled = GetBool("ObjectLauncher", "AutoCleanup", false);
                ObjectLauncherController.AutoCleanupInterval = GetFloat("ObjectLauncher", "CleanupInterval", 30f);
                ObjectLauncherController.SpawnForceDelay = GetFloat("ObjectLauncher", "SpawnForceDelay", 0.02f);
                ObjectLauncherController.AutoDespawnEnabled = GetBool("ObjectLauncher", "AutoDespawn", false);
                ObjectLauncherController.AutoDespawnDelay = GetFloat("ObjectLauncher", "AutoDespawnDelay", 10f);
                ObjectLauncherController.AimRotationEnabled = GetBool("ObjectLauncher", "AimRotation", false);
                ObjectLauncherController.RotationX = GetFloat("ObjectLauncher", "RotationX", 0f);
                ObjectLauncherController.RotationY = GetFloat("ObjectLauncher", "RotationY", 0f);
                ObjectLauncherController.RotationZ = GetFloat("ObjectLauncher", "RotationZ", 0f);
                ObjectLauncherController.PreActivateMenuTap = GetBool("ObjectLauncher", "PreActivateMenuTap", false);
                var savedBarcode = GetValue("ObjectLauncher", "BarcodeID", "");
                var savedItemName = GetValue("ObjectLauncher", "ItemName", "");
                if (!string.IsNullOrEmpty(savedBarcode))
                {
                    ObjectLauncherController.CurrentBarcodeID = savedBarcode;
                    ObjectLauncherController.CurrentItemName = !string.IsNullOrEmpty(savedItemName) ? savedItemName : "Custom";
                }
            }, "Load Object Launcher");

            SafeExecute(() =>
            {
                ForceGrabController.IsEnabled = GetBool("ForceGrab", "Enabled", false);
                ForceGrabController.GlobalMode = GetBool("ForceGrab", "Global", false);
                ForceGrabController.InstantMode = GetBool("ForceGrab", "Instant", false);
                ForceGrabController.FlySpeed = GetFloat("ForceGrab", "FlySpeed", 25f);
                ForceGrabController.GripOnly = GetBool("ForceGrab", "GripOnly", true);
                ForceGrabController.IgnorePlayerRig = GetBool("ForceGrab", "IgnoreRig", true);
                ForceGrabController.ForcePush = GetBool("ForceGrab", "Push", false);
                ForceGrabController.PushForce = GetFloat("ForceGrab", "PushForce", 30f);
            }, "Load Force Grab");

            // Stare at Player — removed

            SafeExecute(() =>
            {
                WaypointController.SpawnHeight = GetFloat("WaypointProjectile", "SpawnHeight", 5f);
                WaypointController.LaunchForce = GetFloat("WaypointProjectile", "LaunchForce", 0f);
                WaypointController.ProjectileCount = GetInt("WaypointProjectile", "ProjectileCount", 1);
                WaypointController.ProjectileSpacing = GetFloat("WaypointProjectile", "ProjectileSpacing", 0.8f);
                WaypointController.SpawnScale = GetFloat("WaypointProjectile", "Scale", 1f);
                WaypointController.SpinVelocity = GetFloat("WaypointProjectile", "SpinVelocity", 0f);
                WaypointController.AimRotationEnabled = GetBool("WaypointProjectile", "AimRotation", false);
                WaypointController.PreActivateMenuTap = GetBool("WaypointProjectile", "PreActivateMenuTap", false);
                WaypointController.SpawnForceDelay = GetFloat("WaypointProjectile", "SpawnForceDelay", 0.02f);
                WaypointController.HomingEnabled = GetBool("WaypointProjectile", "Homing", false);
                WaypointController.HomingFilter = (TargetFilter)GetInt("WaypointProjectile", "HomingFilter", (int)TargetFilter.NEAREST);
                WaypointController.HomingStrength = GetFloat("WaypointProjectile", "HomingStrength", 5f);
                WaypointController.HomingDuration = GetFloat("WaypointProjectile", "HomingDuration", 0f);
                WaypointController.HomingRotationLock = GetBool("WaypointProjectile", "HomingRotationLock", false);
                WaypointController.HomingSpeed = GetFloat("WaypointProjectile", "HomingSpeed", 0f);
                WaypointController.HomingAccelEnabled = GetBool("WaypointProjectile", "HomingAccel", false);
                WaypointController.HomingAccelRate = GetFloat("WaypointProjectile", "HomingAccelRate", 2f);
                WaypointController.HomingTargetHead = GetBool("WaypointProjectile", "HomingTargetHead", false);
                WaypointController.HomingMomentum = GetBool("WaypointProjectile", "HomingMomentum", false);
                WaypointController.HomingStayDuration = GetFloat("WaypointProjectile", "HomingStayDuration", 2f);
                WaypointController.ForceDelay = GetFloat("WaypointProjectile", "ForceDelay", 0.02f);
                WaypointController.ControllerShortcut = GetBool("WaypointProjectile", "ControllerShortcut", true);
            }, "Load Waypoint Projectile");

            SafeExecute(() =>
            {
                PlayerSpawnController.HeightAbovePlayer = GetFloat("DropOnPlayer", "Height", 3f);
                PlayerSpawnController.LaunchForce = GetFloat("DropOnPlayer", "LaunchForce", 0f);
                PlayerSpawnController.ProjectileCount = GetInt("DropOnPlayer", "ProjectileCount", 1);
                PlayerSpawnController.ProjectileSpacing = GetFloat("DropOnPlayer", "ProjectileSpacing", 0.8f);
                PlayerSpawnController.SpawnScale = GetFloat("DropOnPlayer", "Scale", 1f);
                PlayerSpawnController.SpinVelocity = GetFloat("DropOnPlayer", "SpinVelocity", 0f);
                PlayerSpawnController.AimRotationEnabled = GetBool("DropOnPlayer", "AimRotation", false);
                PlayerSpawnController.PreActivateMenuTap = GetBool("DropOnPlayer", "PreActivateMenuTap", false);
                PlayerSpawnController.SpawnForceDelay = GetFloat("DropOnPlayer", "SpawnForceDelay", 0.02f);
                PlayerSpawnController.HomingEnabled = GetBool("DropOnPlayer", "Homing", false);
                PlayerSpawnController.HomingFilter = (TargetFilter)GetInt("DropOnPlayer", "HomingFilter", (int)TargetFilter.NEAREST);
                PlayerSpawnController.HomingStrength = GetFloat("DropOnPlayer", "HomingStrength", 5f);
                PlayerSpawnController.HomingDuration = GetFloat("DropOnPlayer", "HomingDuration", 0f);
                PlayerSpawnController.HomingRotationLock = GetBool("DropOnPlayer", "HomingRotationLock", false);
                PlayerSpawnController.HomingSpeed = GetFloat("DropOnPlayer", "HomingSpeed", 0f);
                PlayerSpawnController.HomingAccelEnabled = GetBool("DropOnPlayer", "HomingAccel", false);
                PlayerSpawnController.HomingAccelRate = GetFloat("DropOnPlayer", "HomingAccelRate", 2f);
                PlayerSpawnController.HomingTargetHead = GetBool("DropOnPlayer", "HomingTargetHead", false);
                PlayerSpawnController.HomingMomentum = GetBool("DropOnPlayer", "HomingMomentum", false);
                PlayerSpawnController.HomingStayDuration = GetFloat("DropOnPlayer", "HomingStayDuration", 2f);
                PlayerSpawnController.ForceDelay = GetFloat("DropOnPlayer", "ForceDelay", 0.02f);
            }, "Load Drop on Player");

            SafeExecute(() =>
            {
                ServerQueueController.Enabled = GetBool("ServerQueue", "Enabled", true);
                ServerQueueController.PollInterval = GetFloat("ServerQueue", "PollInterval", 10f);
                ServerQueueController.LastServerCode = GetValue("ServerQueue", "LastServerCode", "");
            }, "Load Server Queue");

            SafeExecute(() =>
            {
                var keybinds = KeybindManager.Keybinds;
                for (int i = 0; i < keybinds.Count; i++)
                {
                    var kb = keybinds[i];
                    KeybindManager.SetKey(kb.Id, (UnityEngine.KeyCode)GetInt("Keybinds", kb.Id, (int)kb.DefaultKey));
                }
            }, "Load Keybinds");

            SafeExecute(() =>
            {
                ScreenShareController.Scale = GetInt("ScreenShare", "Scale", 50);
                ScreenShareController.TargetFps = GetInt("ScreenShare", "TargetFps", 15);
                ScreenShareController.StreamPort = GetInt("ScreenShare", "StreamPort", 9850);
                ScreenShareController.UsePublicIp = GetBool("ScreenShare", "UsePublicIp", false);
            }, "Load Screen Share");

            SafeExecute(() =>
            {
                string favData = GetValue("Favorites", "Data", "");
                SpawnableSearcher.DeserializeFavorites(favData);
            }, "Load Favorites");

            SafeExecute(() =>
            {
                RandomExplodeController.Enabled = GetBool("RandomExplode", "Enabled", false);
                RandomExplodeController.SelectedExplosion = (ExplosionType)GetInt("RandomExplode", "ExplosionType", (int)ExplosionType.Normal);
                RandomExplodeController.CustomBarcode = GetValue("RandomExplode", "CustomBarcode", "");
                RandomExplodeController.Interval = GetFloat("RandomExplode", "Interval", 1f);
                RandomExplodeController.ChanceDenominator = GetInt("RandomExplode", "ChanceDenom", 10000);
                RandomExplodeController.LaunchForce = GetFloat("RandomExplode", "LaunchForce", 50f);
                RandomExplodeController.LaunchDirection = (ExplodeLaunchDir)GetInt("RandomExplode", "LaunchDir", (int)ExplodeLaunchDir.RANDOM);
                RandomExplodeController.RagdollOnExplode = GetBool("RandomExplode", "RagdollOnExplode", false);
                RandomExplodeController.Target = (ExplodeTarget)GetInt("RandomExplode", "Target", (int)ExplodeTarget.SELF);
                RandomExplodeController.ControllerShortcut = GetBool("RandomExplode", "ControllerShortcut", false);
                RandomExplodeController.HoldDuration = GetFloat("RandomExplode", "HoldDuration", 1.5f);
            }, "Load Random Explode");

            SafeExecute(() =>
            {
                ForceSpawnerController.Enabled = GetBool("ForceSpawner", "Enabled", false);
                ForceSpawnerController.UnredactAll = GetBool("ForceSpawner", "UnredactAll", false);
                ForceSpawnerController.Distance = GetInt("ForceSpawner", "Distance", 2);
                ForceSpawnerController.OffsetX = GetFloat("ForceSpawner", "OffsetX", 0f);
                ForceSpawnerController.OffsetY = GetFloat("ForceSpawner", "OffsetY", 0f);
                ForceSpawnerController.OffsetZ = GetFloat("ForceSpawner", "OffsetZ", 0f);
            }, "Load Force Spawner");

            SafeExecute(() =>
            {
                RemoveWindSFXController.Enabled = GetBool("RemoveWindSFX", "Enabled", false);
            }, "Load Remove Wind SFX");

            SafeExecute(() =>
            {
                WeepingAngelController.Enabled = GetBool("WeepingAngel", "Enabled", false);
                WeepingAngelController.TargetEveryone = GetBool("WeepingAngel", "TargetEveryone", true);
                WeepingAngelController.ViewAngle = GetFloat("WeepingAngel", "ViewAngle", 60f);
                WeepingAngelController.ViewDistance = GetFloat("WeepingAngel", "ViewDistance", 100f);
            }, "Load Weeping Angel");

            SafeExecute(() =>
            {
                AutoRunController.Enabled = GetBool("AutoRun", "Enabled", false);
            }, "Load Auto Run");

            SafeExecute(() =>
            {
                SpinbotController.Enabled = GetBool("Spinbot", "Enabled", false);
                SpinbotController.Speed = GetFloat("Spinbot", "Speed", 720f);
                SpinbotController.Direction = (SpinDirection)GetInt("Spinbot", "Direction", (int)SpinDirection.RIGHT);
            }, "Load Spinbot");

            SafeExecute(() =>
            {
                BunnyHopController.Enabled = GetBool("BunnyHop", "Enabled", false);
                BunnyHopController.HopBoost = GetFloat("BunnyHop", "HopBoost", 1.5f);
                BunnyHopController.MaxSpeed = GetFloat("BunnyHop", "MaxSpeed", 50f);
                BunnyHopController.AirStrafeForce = GetFloat("BunnyHop", "AirStrafeForce", 12f);
                BunnyHopController.JumpForce = GetFloat("BunnyHop", "JumpForce", 5.5f);
                BunnyHopController.AutoHop = GetBool("BunnyHop", "AutoHop", true);
                BunnyHopController.StrafeMode = (AirStrafeMode)GetInt("BunnyHop", "StrafeMode", (int)AirStrafeMode.EASY);
                BunnyHopController.StandableNormal = GetFloat("BunnyHop", "StandableNormal", 0.7f);
                BunnyHopController.TrimpEnabled = GetBool("BunnyHop", "TrimpEnabled", true);
                BunnyHopController.TrimpMultiplier = GetFloat("BunnyHop", "TrimpMultiplier", 1.0f);
            }, "Load BunnyHop");

            SafeExecute(() =>
            {
                DefaultWorldController.Enabled = GetBool("DefaultWorld", "Enabled", false);
                DefaultWorldController.Barcode = GetValue("DefaultWorld", "Barcode", "");
                DefaultWorldController.LevelName = GetValue("DefaultWorld", "LevelName", "");
            }, "Load Default World");

            SafeExecute(() =>
            {
                AutoHostController.Enabled = GetBool("AutoHost", "Enabled", false);
            }, "Load Auto Host");

            SafeExecute(() =>
            {
                // Enabled is intentionally NOT loaded — XYZ Scale always starts off
                XYZScaleController.ScaleX = GetFloat("XYZScale", "ScaleX", 1f);
                XYZScaleController.ScaleY = GetFloat("XYZScale", "ScaleY", 1f);
                XYZScaleController.ScaleZ = GetFloat("XYZScale", "ScaleZ", 1f);
            }, "Load XYZ Scale");

            SafeExecute(() =>
            {
                DisableAvatarFXController.Enabled = GetBool("DisableAvatarFX", "Enabled", false);
            }, "Load Disable Avatar FX");

            SafeExecute(() =>
            {
                HolsterHiderController.HideHolsters = GetBool("HolsterHider", "HideHolsters", false);
                HolsterHiderController.HideAmmoPouch = GetBool("HolsterHider", "HideAmmoPouch", false);
                HolsterHiderController.HideBodyLog = GetBool("HolsterHider", "HideBodyLog", false);
            }, "Load Holster Hider");

            SafeExecute(() =>
            {
                GhostModeController.Enabled = GetBool("GhostMode", "Enabled", false);
            }, "Load Ghost Mode");

            SafeExecute(() =>
            {
                AntiSlowmoController.Enabled = GetBool("AntiSlowmo", "Enabled", false);
            }, "Load Anti-Slowmo");

            SafeExecute(() =>
            {
                AntiTeleportController.Enabled = GetBool("AntiTeleport", "Enabled", false);
            }, "Load Anti-Teleport");

            SafeExecute(() =>
            {
                AntiRagdollController.Enabled = GetBool("AntiRagdoll", "Enabled", false);
            }, "Load Anti-Ragdoll");

            SafeExecute(() =>
            {
                SpawnLoggerController.Enabled = GetBool("SpawnLogger", "Enabled", false);
                SpawnLoggerController.ShowNotifications = GetBool("SpawnLogger", "ShowNotifications", true);
            }, "Load Spawn Logger");

            SafeExecute(() =>
            {
                DamageMultiplierController.GunMultiplier = GetFloat("DamageMultiplier", "GunMultiplier", 1f);
                DamageMultiplierController.MeleeMultiplier = GetFloat("DamageMultiplier", "MeleeMultiplier", 1f);
            }, "Load Damage Multiplier");

            SafeExecute(() =>
            {
                AvatarLoggerController.Enabled = GetBool("AvatarLogger", "Enabled", false);
                AvatarLoggerController.ShowNotifications = GetBool("AvatarLogger", "ShowNotifications", true);
            }, "Load Avatar Logger");

            SafeExecute(() =>
            {
                PlayerActionLoggerController.Enabled = GetBool("PlayerActionLogger", "Enabled", false);
                PlayerActionLoggerController.LogJoins = GetBool("PlayerActionLogger", "LogJoins", true);
                PlayerActionLoggerController.LogLeaves = GetBool("PlayerActionLogger", "LogLeaves", true);
                PlayerActionLoggerController.LogDeaths = GetBool("PlayerActionLogger", "LogDeaths", true);
                PlayerActionLoggerController.ShowNotifications = GetBool("PlayerActionLogger", "ShowNotifications", true);
            }, "Load Player Action Logger");

            SafeExecute(() =>
            {
                OverlayMenu.MenuOpacity = GetFloat("Overlay", "Opacity", 1f);
                OverlayMenu.FontSize = GetInt("Overlay", "FontSize", 14);
                OverlayMenu.RainbowTitle = GetBool("Overlay", "RainbowTitle", false);
                OverlayMenu.AccentColor = new Color(
                    GetFloat("Overlay", "AccentR", 0f),
                    GetFloat("Overlay", "AccentG", 1f),
                    GetFloat("Overlay", "AccentB", 1f),
                    1f);
                OverlayMenu.SectionColor = new Color(
                    GetFloat("Overlay", "SectionR", 1f),
                    GetFloat("Overlay", "SectionG", 0.85f),
                    GetFloat("Overlay", "SectionB", 0.4f),
                    1f);
                OverlayMenu.BgColor = new Color(
                    GetFloat("Overlay", "BgR", 0.15f),
                    GetFloat("Overlay", "BgG", 0.15f),
                    GetFloat("Overlay", "BgB", 0.15f),
                    GetFloat("Overlay", "BgA", 0.92f));
                OverlayMenu.GradientEnabled = GetBool("Overlay", "GradientEnabled", false);
                OverlayMenu.GradientEndColor = new Color(
                    GetFloat("Overlay", "GradEndR", 1f),
                    GetFloat("Overlay", "GradEndG", 0f),
                    GetFloat("Overlay", "GradEndB", 1f),
                    1f);
            }, "Load Overlay");

            SafeExecute(() =>
            {
                LoadPresets();
            }, "Load Presets");

            Main.MelonLog.Msg("Settings loaded from config file");
        }

        /// <summary>
        /// Read current controller values and save to config file.
        /// </summary>
        public static void SaveAll()
        {
            if (!_initialized) return;

            SafeExecute(() =>
            {
                SetBool("Global", "Notifications", NotificationHelper.NotificationsEnabled);
            }, "Save Global");

            SafeExecute(() =>
            {
                SetBool("Player", "GodMode", GodModeController.IsGodModeEnabled);
                SetBool("Player", "AntiConstraint", AntiConstraintController.IsEnabled);
                SetBool("Player", "AntiKnockout", AntiKnockoutController.IsEnabled);
                SetBool("Player", "UnbreakableGrip", UnbreakableGripController.IsEnabled);
                SetBool("Player", "AntiGravityChange", AntiGravityChangeController.Enabled);
            }, "Save Player");

            SafeExecute(() =>
            {
                SetBool("Combat", "FullAuto", FullAutoController.IsFullAutoEnabled);
                SetBool("Combat", "InfiniteAmmo", InfiniteAmmoController.IsEnabled);
            }, "Save Combat");

            SafeExecute(() =>
            {
                SetBool("CrazyGuns", "PurpleGuns", ChaosGunController.PurpleGuns);
                SetBool("CrazyGuns", "InsaneDamage", ChaosGunController.InsaneDamage);
                SetBool("CrazyGuns", "NoRecoil", ChaosGunController.NoRecoil);
                SetBool("CrazyGuns", "InsaneFirerate", ChaosGunController.InsaneFirerate);
                SetBool("CrazyGuns", "NoWeight", ChaosGunController.NoWeight);
                SetBool("CrazyGuns", "GunsBounce", ChaosGunController.GunsBounce);
                SetBool("CrazyGuns", "NoReload", ChaosGunController.NoReload);
            }, "Save Crazy Guns");

            SafeExecute(() =>
            {
                SetBool("Dash", "Enabled", DashController.IsDashEnabled);
                SetFloat("Dash", "Force", DashController.DashForce);
                SetBool("Dash", "Instant", DashController.IsDashInstantaneous);
                SetBool("Dash", "Continuous", DashController.IsDashContinuous);
                SetBool("Dash", "HandOriented", DashController.IsHandOriented);
                SetBool("Dash", "LeftHand", DashController.UseLeftHand);
                SetBool("Dash", "LockOn", DashController.LockOnEnabled);
                SetInt("Dash", "LockOnFilter", (int)DashController.LockOnFilter);
                SetBool("Dash", "LookAtTarget", DashController.LookAtTarget);
                SetBool("Dash", "LookAtHead", DashController.LookAtHead);
                SetBool("Dash", "KillVelocityOnLand", DashController.KillVelocityOnLand);
                // Dash effects
                SetBool("Dash", "EffectEnabled", DashController.EffectEnabled);
                SetValue("Dash", "EffectBarcode", DashController.EffectBarcode);
                SetBool("Dash", "SmashBone", DashController.SmashBoneEnabled);
                SetInt("Dash", "SmashBoneCount", DashController.SmashBoneCount);
                SetBool("Dash", "SmashBoneFlip", DashController.SmashBoneFlip);
                SetBool("Dash", "CosmeticEnabled", DashController.CosmeticEnabled);
                SetValue("Dash", "CosmeticBarcode", DashController.CosmeticBarcode);
                SetInt("Dash", "CosmeticCount", DashController.CosmeticCount);
                SetBool("Dash", "CosmeticFlip", DashController.CosmeticFlip);
                SetFloat("Dash", "EffectSpawnDelay", DashController.EffectSpawnDelay);
                SetFloat("Dash", "EffectSpawnInterval", DashController.EffectSpawnInterval);
                SetFloat("Dash", "EffectOffsetX", DashController.EffectOffsetX);
                SetFloat("Dash", "EffectOffsetY", DashController.EffectOffsetY);
                SetFloat("Dash", "EffectOffsetZ", DashController.EffectOffsetZ);
                SetInt("Dash", "EffectMatrixCount", DashController.EffectMatrixCount);
                SetFloat("Dash", "EffectMatrixSpacing", DashController.EffectMatrixSpacing);
                SetInt("Dash", "EffectMatrixMode", (int)DashController.EffectMatrixMode);
            }, "Save Dash");

            SafeExecute(() =>
            {
                SetBool("Flight", "Enabled", FlightController.Enabled);
                SetFloat("Flight", "SpeedMultiplier", FlightController.SpeedMultiplier);
                SetBool("Flight", "Acceleration", FlightController.AccelerationEnabled);
                SetFloat("Flight", "AccelerationRate", FlightController.AccelerationRate);
                SetBool("Flight", "Momentum", FlightController.MomentumEnabled);
                SetBool("Flight", "LockOn", FlightController.LockOnEnabled);
                SetInt("Flight", "LockOnFilter", (int)FlightController.LockOnFilter);
                SetBool("Flight", "LookAtTarget", FlightController.LookAtTarget);
                SetBool("Flight", "LookAtHead", FlightController.LookAtHead);
                // Flight effects
                SetBool("Flight", "EffectEnabled", FlightController.EffectEnabled);
                SetValue("Flight", "EffectBarcode", FlightController.EffectBarcode);
                SetBool("Flight", "SmashBone", FlightController.SmashBoneEnabled);
                SetInt("Flight", "SmashBoneCount", FlightController.SmashBoneCount);
                SetBool("Flight", "SmashBoneFlip", FlightController.SmashBoneFlip);
                SetBool("Flight", "CosmeticEnabled", FlightController.CosmeticEnabled);
                SetValue("Flight", "CosmeticBarcode", FlightController.CosmeticBarcode);
                SetInt("Flight", "CosmeticCount", FlightController.CosmeticCount);
                SetBool("Flight", "CosmeticFlip", FlightController.CosmeticFlip);
                SetFloat("Flight", "EffectSpawnDelay", FlightController.EffectSpawnDelay);
                SetFloat("Flight", "EffectSpawnInterval", FlightController.EffectSpawnInterval);
                SetFloat("Flight", "EffectOffsetX", FlightController.EffectOffsetX);
                SetFloat("Flight", "EffectOffsetY", FlightController.EffectOffsetY);
                SetFloat("Flight", "EffectOffsetZ", FlightController.EffectOffsetZ);
                SetBool("Flight", "EffectHandOriented", FlightController.EffectHandOriented);
                SetBool("Flight", "EffectUseLeftHand", FlightController.EffectUseLeftHand);
                SetInt("Flight", "EffectMatrixCount", FlightController.EffectMatrixCount);
                SetFloat("Flight", "EffectMatrixSpacing", FlightController.EffectMatrixSpacing);
                SetInt("Flight", "EffectMatrixMode", (int)FlightController.EffectMatrixMode);
            }, "Save Flight");

            SafeExecute(() =>
            {
                SetBool("GravityBoots", "Enabled", GravityBootsController.Enabled);
                SetFloat("GravityBoots", "GravityStrength", GravityBootsController.GravityStrength);
                SetFloat("GravityBoots", "SurfaceDetectRange", GravityBootsController.SurfaceDetectRange);
                SetFloat("GravityBoots", "RotationSpeed", GravityBootsController.RotationSpeed);
                SetFloat("GravityBoots", "StickForce", GravityBootsController.StickForce);
            }, "Save Gravity Boots");

            SafeExecute(() =>
            {
                SetBool("Ragdoll", "Enabled", RagdollController.Enabled);
                SetBool("Ragdoll", "GrabEnabled", RagdollController.GrabEnabled);
                SetBool("Ragdoll", "NeckGrabDisablesArms", RagdollController.NeckGrabDisablesArms);
                SetBool("Ragdoll", "ArmGrabEnabled", RagdollController.ArmGrabEnabled);
                SetInt("Ragdoll", "Mode", (int)RagdollController.Mode);
                SetBool("Ragdoll", "TantrumMode", RagdollController.TantrumMode);
                SetInt("Ragdoll", "Binding", (int)RagdollController.Binding);
                SetInt("Ragdoll", "KeybindHand", (int)RagdollController.KeybindHand);
                SetBool("Ragdoll", "FallEnabled", RagdollController.FallEnabled);
                SetFloat("Ragdoll", "FallVelocity", RagdollController.FallVelocityThreshold);
                SetBool("Ragdoll", "ImpactEnabled", RagdollController.ImpactEnabled);
                SetFloat("Ragdoll", "ImpactThreshold", RagdollController.ImpactThreshold);
                SetBool("Ragdoll", "LaunchEnabled", RagdollController.LaunchEnabled);
                SetFloat("Ragdoll", "LaunchThreshold", RagdollController.LaunchThreshold);
                SetBool("Ragdoll", "SlipEnabled", RagdollController.SlipEnabled);
                SetFloat("Ragdoll", "SlipFriction", RagdollController.SlipFrictionThreshold);
                SetFloat("Ragdoll", "SlipVelocity", RagdollController.SlipVelocityThreshold);
                SetBool("Ragdoll", "WallPushEnabled", RagdollController.WallPushEnabled);
                SetFloat("Ragdoll", "WallPushVelocity", RagdollController.WallPushVelocityThreshold);
            }, "Save Ragdoll");

            SafeExecute(() =>
            {
                SetBool("ExplosivePunch", "Enabled", ExplosivePunchController.IsExplosivePunchEnabled);
                SetBool("ExplosivePunch", "SuperExplosive", ExplosivePunchController.IsSuperExplosivePunchEnabled);
                SetBool("ExplosivePunch", "BlackFlash", ExplosivePunchController.IsBlackFlashEnabled);
                SetBool("ExplosivePunch", "TinyExplosive", ExplosivePunchController.IsTinyExplosiveEnabled);
                SetBool("ExplosivePunch", "Boom", ExplosivePunchController.IsBoomEnabled);
                SetBool("ExplosivePunch", "CustomPunch", ExplosivePunchController.IsCustomPunchEnabled);
                SetValue("ExplosivePunch", "CustomPunchBarcode", ExplosivePunchController.CustomPunchBarcode);
                SetFloat("ExplosivePunch", "SpawnDelay", ExplosivePunchController.SpawnDelay);
                SetFloat("ExplosivePunch", "PunchSpeed", ExplosivePunchController.PunchVelocityThreshold);
                SetFloat("ExplosivePunch", "PunchCooldown", ExplosivePunchController.PunchCooldown);
                SetBool("ExplosivePunch", "RigCheckOnly", ExplosivePunchController.RigCheckOnly);
                SetBool("ExplosivePunch", "FaceTarget", ExplosivePunchController.FaceTarget);
                SetBool("ExplosivePunch", "LegacyPunch", ExplosivePunchController.IsLegacyPunchEnabled);
                SetBool("ExplosivePunch", "SmashBone", ExplosivePunchController.IsSmashBoneEnabled);
                SetInt("ExplosivePunch", "SmashBoneCount", ExplosivePunchController.SmashBoneCount);
                SetBool("ExplosivePunch", "SmashBoneFlip", ExplosivePunchController.SmashBoneFlip);
                SetInt("ExplosivePunch", "PunchMode", (int)ExplosivePunchController.PunchMode);
                SetBool("ExplosivePunch", "CosmeticEnabled", ExplosivePunchController.IsCosmeticEnabled);
                SetValue("ExplosivePunch", "CosmeticBarcode", ExplosivePunchController.CosmeticBarcode);
                SetInt("ExplosivePunch", "CosmeticCount", ExplosivePunchController.CosmeticCount);
                SetBool("ExplosivePunch", "CosmeticFlip", ExplosivePunchController.CosmeticFlip);
                SetInt("ExplosivePunch", "PunchSpawnCount", ExplosivePunchController.PunchSpawnCount);
                SetFloat("ExplosivePunch", "PunchSpacing", ExplosivePunchController.PunchSpacing);
                SetInt("ExplosivePunch", "PunchMatrixMode", (int)ExplosivePunchController.PunchMatrixMode);
                // Per-hand left
                SetInt("ExplosivePunch", "LeftExplosionType", (int)ExplosivePunchController.LeftExplosionType);
                SetValue("ExplosivePunch", "LeftCustomBarcode", ExplosivePunchController.LeftCustomBarcode);
                SetBool("ExplosivePunch", "LeftSmashBone", ExplosivePunchController.LeftSmashBoneEnabled);
                SetInt("ExplosivePunch", "LeftSmashBoneCount", ExplosivePunchController.LeftSmashBoneCount);
                SetBool("ExplosivePunch", "LeftSmashBoneFlip", ExplosivePunchController.LeftSmashBoneFlip);
                SetBool("ExplosivePunch", "LeftCosmeticEnabled", ExplosivePunchController.LeftCosmeticEnabled);
                SetValue("ExplosivePunch", "LeftCosmeticBarcode", ExplosivePunchController.LeftCosmeticBarcode);
                SetInt("ExplosivePunch", "LeftCosmeticCount", ExplosivePunchController.LeftCosmeticCount);
                SetBool("ExplosivePunch", "LeftCosmeticFlip", ExplosivePunchController.LeftCosmeticFlip);
                // Per-hand right
                SetInt("ExplosivePunch", "RightExplosionType", (int)ExplosivePunchController.RightExplosionType);
                SetValue("ExplosivePunch", "RightCustomBarcode", ExplosivePunchController.RightCustomBarcode);
                SetBool("ExplosivePunch", "RightSmashBone", ExplosivePunchController.RightSmashBoneEnabled);
                SetInt("ExplosivePunch", "RightSmashBoneCount", ExplosivePunchController.RightSmashBoneCount);
                SetBool("ExplosivePunch", "RightSmashBoneFlip", ExplosivePunchController.RightSmashBoneFlip);
                SetBool("ExplosivePunch", "RightCosmeticEnabled", ExplosivePunchController.RightCosmeticEnabled);
                SetValue("ExplosivePunch", "RightCosmeticBarcode", ExplosivePunchController.RightCosmeticBarcode);
                SetInt("ExplosivePunch", "RightCosmeticCount", ExplosivePunchController.RightCosmeticCount);
                SetBool("ExplosivePunch", "RightCosmeticFlip", ExplosivePunchController.RightCosmeticFlip);
            }, "Save Explosive Punch");

            SafeExecute(() =>
            {
                SetBool("GroundPound", "Enabled", GroundPoundController.Enabled);
                SetFloat("GroundPound", "VelocityThreshold", GroundPoundController.VelocityThreshold);
                SetFloat("GroundPound", "Cooldown", GroundPoundController.Cooldown);
                SetFloat("GroundPound", "SpawnDelay", GroundPoundController.SpawnDelay);
                SetInt("GroundPound", "ExplosionType", (int)GroundPoundController.SelectedExplosion);
                SetValue("GroundPound", "CustomBarcode", GroundPoundController.CustomBarcode);
                SetBool("GroundPound", "SmashBone", GroundPoundController.SmashBoneEnabled);
                SetInt("GroundPound", "SmashBoneCount", GroundPoundController.SmashBoneCount);
                SetBool("GroundPound", "SmashBoneFlip", GroundPoundController.SmashBoneFlip);
                SetBool("GroundPound", "CosmeticEnabled", GroundPoundController.CosmeticEnabled);
                SetValue("GroundPound", "CosmeticBarcode", GroundPoundController.CosmeticBarcode);
                SetInt("GroundPound", "CosmeticCount", GroundPoundController.CosmeticCount);
                SetBool("GroundPound", "CosmeticFlip", GroundPoundController.CosmeticFlip);
                SetInt("GroundPound", "MatrixCount", GroundPoundController.MatrixCount);
                SetFloat("GroundPound", "MatrixSpacing", GroundPoundController.MatrixSpacing);
                SetInt("GroundPound", "MatrixMode", (int)GroundPoundController.SelectedMatrixMode);
            }, "Save Ground Slam");

            SafeExecute(() =>
            {
                SetBool("ExplosiveImpact", "Enabled", ExplosiveImpactController.Enabled);
                SetFloat("ExplosiveImpact", "VelocityThreshold", ExplosiveImpactController.VelocityThreshold);
                SetFloat("ExplosiveImpact", "Cooldown", ExplosiveImpactController.Cooldown);
                SetFloat("ExplosiveImpact", "SpawnDelay", ExplosiveImpactController.SpawnDelay);
                SetInt("ExplosiveImpact", "ExplosionType", (int)ExplosiveImpactController.SelectedExplosion);
                SetValue("ExplosiveImpact", "CustomBarcode", ExplosiveImpactController.CustomBarcode);
                SetBool("ExplosiveImpact", "SmashBone", ExplosiveImpactController.SmashBoneEnabled);
                SetInt("ExplosiveImpact", "SmashBoneCount", ExplosiveImpactController.SmashBoneCount);
                SetBool("ExplosiveImpact", "SmashBoneFlip", ExplosiveImpactController.SmashBoneFlip);
                SetBool("ExplosiveImpact", "CosmeticEnabled", ExplosiveImpactController.CosmeticEnabled);
                SetValue("ExplosiveImpact", "CosmeticBarcode", ExplosiveImpactController.CosmeticBarcode);
                SetInt("ExplosiveImpact", "CosmeticCount", ExplosiveImpactController.CosmeticCount);
                SetBool("ExplosiveImpact", "CosmeticFlip", ExplosiveImpactController.CosmeticFlip);
                SetInt("ExplosiveImpact", "MatrixCount", ExplosiveImpactController.MatrixCount);
                SetFloat("ExplosiveImpact", "MatrixSpacing", ExplosiveImpactController.MatrixSpacing);
                SetInt("ExplosiveImpact", "MatrixMode", (int)ExplosiveImpactController.SelectedMatrixMode);
            }, "Save Explosive Impact");

            SafeExecute(() =>
            {
                SetBool("AntiDespawn", "Enabled", AntiDespawnController.Enabled);
            }, "Save Anti-Despawn");

            SafeExecute(() =>
            {
                SetBool("AntiGrab", "Enabled", AntiGrabController.Enabled);
            }, "Save Anti-Grab");

            SafeExecute(() =>
            {
                SetBool("SpawnLimiter", "Enabled", SpawnLimiterController.Enabled);
                SetFloat("SpawnLimiter", "SpawnDelay", SpawnLimiterController.SpawnDelay);
                SetInt("SpawnLimiter", "MaxPerFrame", SpawnLimiterController.MaxPerFrame);
            }, "Save Spawn Limiter");

            SafeExecute(() =>
            {
                SetBool("BodyLogColor", "Enabled", BodyLogColorController.Enabled);
                SetFloat("BodyLogColor", "BodyLogR", BodyLogColorController.BodyLogR);
                SetFloat("BodyLogColor", "BodyLogG", BodyLogColorController.BodyLogG);
                SetFloat("BodyLogColor", "BodyLogB", BodyLogColorController.BodyLogB);
                SetFloat("BodyLogColor", "BodyLogA", BodyLogColorController.BodyLogA);
                SetFloat("BodyLogColor", "BallR", BodyLogColorController.BallR);
                SetFloat("BodyLogColor", "BallG", BodyLogColorController.BallG);
                SetFloat("BodyLogColor", "BallB", BodyLogColorController.BallB);
                SetFloat("BodyLogColor", "BallA", BodyLogColorController.BallA);
                SetFloat("BodyLogColor", "LineR", BodyLogColorController.LineR);
                SetFloat("BodyLogColor", "LineG", BodyLogColorController.LineG);
                SetFloat("BodyLogColor", "LineB", BodyLogColorController.LineB);
                SetFloat("BodyLogColor", "LineA", BodyLogColorController.LineA);
                SetFloat("BodyLogColor", "RadialR", BodyLogColorController.RadialR);
                SetFloat("BodyLogColor", "RadialG", BodyLogColorController.RadialG);
                SetFloat("BodyLogColor", "RadialB", BodyLogColorController.RadialB);
                SetFloat("BodyLogColor", "RadialA", BodyLogColorController.RadialA);
            }, "Save BodyLog Color");

            SafeExecute(() =>
            {
                SetValue("CosmeticPresets", "Data", CosmeticPresetController.SerializePresets());
            }, "Save Cosmetic Presets");

            SafeExecute(() =>
            {
                SetInt("DespawnAll", "Filter", (int)DespawnAllController.Filter);
                SetBool("DespawnAll", "AutoDespawn", DespawnAllController.AutoDespawnEnabled);
                SetFloat("DespawnAll", "AutoDespawnInterval", DespawnAllController.AutoDespawnIntervalMins);
                SetBool("DespawnAll", "KeepHolsteredItems", DespawnAllController.KeepHolsteredItems);
                SetBool("DespawnAll", "KeepOnlyMyHolsters", DespawnAllController.KeepOnlyMyHolsters);
            }, "Save Despawn All");

            SafeExecute(() =>
            {
                // Don't save Enabled — always start disabled to prevent accidental auto-enable
                SetBool("ObjectLauncher", "Safety", ObjectLauncherController.SafetyEnabled);
                SetBool("ObjectLauncher", "LeftHand", ObjectLauncherController.UseLeftHand);
                SetBool("ObjectLauncher", "FullAuto", ObjectLauncherController.IsFullAuto);
                SetBool("ObjectLauncher", "Trajectory", ObjectLauncherController.ShowTrajectory);
                SetFloat("ObjectLauncher", "FullAutoDelay", ObjectLauncherController.FullAutoDelay);
                SetFloat("ObjectLauncher", "LaunchForce", ObjectLauncherController.LaunchForce);
                SetFloat("ObjectLauncher", "SpawnDistance", ObjectLauncherController.SpawnDistance);
                SetFloat("ObjectLauncher", "SpawnOffsetX", ObjectLauncherController.SpawnOffsetX);
                SetFloat("ObjectLauncher", "SpawnOffsetY", ObjectLauncherController.SpawnOffsetY);
                SetInt("ObjectLauncher", "ProjectileCount", ObjectLauncherController.ProjectileCount);
                SetFloat("ObjectLauncher", "ProjectileSpacing", ObjectLauncherController.ProjectileSpacing);
                SetFloat("ObjectLauncher", "SpinVelocity", ObjectLauncherController.SpinVelocity);
                SetFloat("ObjectLauncher", "Scale", ObjectLauncherController.SpawnScale);
                SetBool("ObjectLauncher", "Homing", ObjectLauncherController.HomingEnabled);
                SetInt("ObjectLauncher", "HomingFilter", (int)ObjectLauncherController.HomingFilter);
                SetFloat("ObjectLauncher", "HomingStrength", ObjectLauncherController.HomingStrength);
                SetFloat("ObjectLauncher", "HomingDuration", ObjectLauncherController.HomingDuration);
                SetBool("ObjectLauncher", "HomingRotationLock", ObjectLauncherController.HomingRotationLock);
                SetFloat("ObjectLauncher", "HomingSpeed", ObjectLauncherController.HomingSpeed);
                SetBool("ObjectLauncher", "HomingAccel", ObjectLauncherController.HomingAccelEnabled);
                SetFloat("ObjectLauncher", "HomingAccelRate", ObjectLauncherController.HomingAccelRate);
                SetBool("ObjectLauncher", "HomingTargetHead", ObjectLauncherController.HomingTargetHead);
                SetBool("ObjectLauncher", "HomingMomentum", ObjectLauncherController.HomingMomentum);
                SetFloat("ObjectLauncher", "HomingStayDuration", ObjectLauncherController.HomingStayDuration);
                SetFloat("ObjectLauncher", "ForceDelay", ObjectLauncherController.ForceDelay);
                SetBool("ObjectLauncher", "AutoCleanup", ObjectLauncherController.AutoCleanupEnabled);
                SetFloat("ObjectLauncher", "CleanupInterval", ObjectLauncherController.AutoCleanupInterval);
                SetFloat("ObjectLauncher", "SpawnForceDelay", ObjectLauncherController.SpawnForceDelay);
                SetBool("ObjectLauncher", "AutoDespawn", ObjectLauncherController.AutoDespawnEnabled);
                SetFloat("ObjectLauncher", "AutoDespawnDelay", ObjectLauncherController.AutoDespawnDelay);
                SetBool("ObjectLauncher", "AimRotation", ObjectLauncherController.AimRotationEnabled);
                SetFloat("ObjectLauncher", "RotationX", ObjectLauncherController.RotationX);
                SetFloat("ObjectLauncher", "RotationY", ObjectLauncherController.RotationY);
                SetFloat("ObjectLauncher", "RotationZ", ObjectLauncherController.RotationZ);
                SetBool("ObjectLauncher", "PreActivateMenuTap", ObjectLauncherController.PreActivateMenuTap);
                SetValue("ObjectLauncher", "BarcodeID", ObjectLauncherController.CurrentBarcodeID);
                SetValue("ObjectLauncher", "ItemName", ObjectLauncherController.CurrentItemName);
            }, "Save Object Launcher");

            SafeExecute(() =>
            {
                SetBool("ForceGrab", "Enabled", ForceGrabController.IsEnabled);
                SetBool("ForceGrab", "Global", ForceGrabController.GlobalMode);
                SetBool("ForceGrab", "Instant", ForceGrabController.InstantMode);
                SetFloat("ForceGrab", "FlySpeed", ForceGrabController.FlySpeed);
                SetBool("ForceGrab", "GripOnly", ForceGrabController.GripOnly);
                SetBool("ForceGrab", "IgnoreRig", ForceGrabController.IgnorePlayerRig);
                SetBool("ForceGrab", "Push", ForceGrabController.ForcePush);
                SetFloat("ForceGrab", "PushForce", ForceGrabController.PushForce);
            }, "Save Force Grab");

            // Stare at Player — removed

            SafeExecute(() =>
            {
                SetFloat("WaypointProjectile", "SpawnHeight", WaypointController.SpawnHeight);
                SetFloat("WaypointProjectile", "LaunchForce", WaypointController.LaunchForce);
                SetInt("WaypointProjectile", "ProjectileCount", WaypointController.ProjectileCount);
                SetFloat("WaypointProjectile", "ProjectileSpacing", WaypointController.ProjectileSpacing);
                SetFloat("WaypointProjectile", "Scale", WaypointController.SpawnScale);
                SetFloat("WaypointProjectile", "SpinVelocity", WaypointController.SpinVelocity);
                SetBool("WaypointProjectile", "AimRotation", WaypointController.AimRotationEnabled);
                SetBool("WaypointProjectile", "PreActivateMenuTap", WaypointController.PreActivateMenuTap);
                SetFloat("WaypointProjectile", "SpawnForceDelay", WaypointController.SpawnForceDelay);
                SetBool("WaypointProjectile", "Homing", WaypointController.HomingEnabled);
                SetInt("WaypointProjectile", "HomingFilter", (int)WaypointController.HomingFilter);
                SetFloat("WaypointProjectile", "HomingStrength", WaypointController.HomingStrength);
                SetFloat("WaypointProjectile", "HomingDuration", WaypointController.HomingDuration);
                SetBool("WaypointProjectile", "HomingRotationLock", WaypointController.HomingRotationLock);
                SetFloat("WaypointProjectile", "HomingSpeed", WaypointController.HomingSpeed);
                SetBool("WaypointProjectile", "HomingAccel", WaypointController.HomingAccelEnabled);
                SetFloat("WaypointProjectile", "HomingAccelRate", WaypointController.HomingAccelRate);
                SetBool("WaypointProjectile", "HomingTargetHead", WaypointController.HomingTargetHead);
                SetBool("WaypointProjectile", "HomingMomentum", WaypointController.HomingMomentum);
                SetFloat("WaypointProjectile", "HomingStayDuration", WaypointController.HomingStayDuration);
                SetFloat("WaypointProjectile", "ForceDelay", WaypointController.ForceDelay);
                SetBool("WaypointProjectile", "ControllerShortcut", WaypointController.ControllerShortcut);
            }, "Save Waypoint Projectile");

            SafeExecute(() =>
            {
                SetFloat("DropOnPlayer", "Height", PlayerSpawnController.HeightAbovePlayer);
                SetFloat("DropOnPlayer", "LaunchForce", PlayerSpawnController.LaunchForce);
                SetInt("DropOnPlayer", "ProjectileCount", PlayerSpawnController.ProjectileCount);
                SetFloat("DropOnPlayer", "ProjectileSpacing", PlayerSpawnController.ProjectileSpacing);
                SetFloat("DropOnPlayer", "Scale", PlayerSpawnController.SpawnScale);
                SetFloat("DropOnPlayer", "SpinVelocity", PlayerSpawnController.SpinVelocity);
                SetBool("DropOnPlayer", "AimRotation", PlayerSpawnController.AimRotationEnabled);
                SetBool("DropOnPlayer", "PreActivateMenuTap", PlayerSpawnController.PreActivateMenuTap);
                SetFloat("DropOnPlayer", "SpawnForceDelay", PlayerSpawnController.SpawnForceDelay);
                SetBool("DropOnPlayer", "Homing", PlayerSpawnController.HomingEnabled);
                SetInt("DropOnPlayer", "HomingFilter", (int)PlayerSpawnController.HomingFilter);
                SetFloat("DropOnPlayer", "HomingStrength", PlayerSpawnController.HomingStrength);
                SetFloat("DropOnPlayer", "HomingDuration", PlayerSpawnController.HomingDuration);
                SetBool("DropOnPlayer", "HomingRotationLock", PlayerSpawnController.HomingRotationLock);
                SetFloat("DropOnPlayer", "HomingSpeed", PlayerSpawnController.HomingSpeed);
                SetBool("DropOnPlayer", "HomingAccel", PlayerSpawnController.HomingAccelEnabled);
                SetFloat("DropOnPlayer", "HomingAccelRate", PlayerSpawnController.HomingAccelRate);
                SetBool("DropOnPlayer", "HomingTargetHead", PlayerSpawnController.HomingTargetHead);
                SetBool("DropOnPlayer", "HomingMomentum", PlayerSpawnController.HomingMomentum);
                SetFloat("DropOnPlayer", "HomingStayDuration", PlayerSpawnController.HomingStayDuration);
                SetFloat("DropOnPlayer", "ForceDelay", PlayerSpawnController.ForceDelay);
            }, "Save Drop on Player");

            SafeExecute(() =>
            {
                SetBool("ServerQueue", "Enabled", ServerQueueController.Enabled);
                SetFloat("ServerQueue", "PollInterval", ServerQueueController.PollInterval);
                SetValue("ServerQueue", "LastServerCode", ServerQueueController.LastServerCode ?? "");
            }, "Save Server Queue");

            SafeExecute(() =>
            {
                foreach (var kb in KeybindManager.Keybinds)
                {
                    SetInt("Keybinds", kb.Id, (int)kb.Key);
                }
            }, "Save Keybinds");

            SafeExecute(() =>
            {
                SetInt("ScreenShare", "Scale", ScreenShareController.Scale);
                SetInt("ScreenShare", "TargetFps", ScreenShareController.TargetFps);
                SetInt("ScreenShare", "StreamPort", ScreenShareController.StreamPort);
                SetBool("ScreenShare", "UsePublicIp", ScreenShareController.UsePublicIp);
            }, "Save Screen Share");

            SafeExecute(() =>
            {
                SetValue("Favorites", "Data", SpawnableSearcher.SerializeFavorites());
            }, "Save Favorites");

            SafeExecute(() =>
            {
                SetBool("RandomExplode", "Enabled", RandomExplodeController.Enabled);
                SetInt("RandomExplode", "ExplosionType", (int)RandomExplodeController.SelectedExplosion);
                SetValue("RandomExplode", "CustomBarcode", RandomExplodeController.CustomBarcode);
                SetFloat("RandomExplode", "Interval", RandomExplodeController.Interval);
                SetInt("RandomExplode", "ChanceDenom", RandomExplodeController.ChanceDenominator);
                SetFloat("RandomExplode", "LaunchForce", RandomExplodeController.LaunchForce);
                SetInt("RandomExplode", "LaunchDir", (int)RandomExplodeController.LaunchDirection);
                SetBool("RandomExplode", "RagdollOnExplode", RandomExplodeController.RagdollOnExplode);
                SetInt("RandomExplode", "Target", (int)RandomExplodeController.Target);
                SetBool("RandomExplode", "ControllerShortcut", RandomExplodeController.ControllerShortcut);
                SetFloat("RandomExplode", "HoldDuration", RandomExplodeController.HoldDuration);
            }, "Save Random Explode");

            SafeExecute(() =>
            {
                SetBool("ForceSpawner", "Enabled", ForceSpawnerController.Enabled);
                SetBool("ForceSpawner", "UnredactAll", ForceSpawnerController.UnredactAll);
                SetInt("ForceSpawner", "Distance", ForceSpawnerController.Distance);
                SetFloat("ForceSpawner", "OffsetX", ForceSpawnerController.OffsetX);
                SetFloat("ForceSpawner", "OffsetY", ForceSpawnerController.OffsetY);
                SetFloat("ForceSpawner", "OffsetZ", ForceSpawnerController.OffsetZ);
            }, "Save Force Spawner");

            SafeExecute(() =>
            {
                SetBool("RemoveWindSFX", "Enabled", RemoveWindSFXController.Enabled);
            }, "Save Remove Wind SFX");

            SafeExecute(() =>
            {
                SetBool("WeepingAngel", "Enabled", WeepingAngelController.Enabled);
                SetBool("WeepingAngel", "TargetEveryone", WeepingAngelController.TargetEveryone);
                SetFloat("WeepingAngel", "ViewAngle", WeepingAngelController.ViewAngle);
                SetFloat("WeepingAngel", "ViewDistance", WeepingAngelController.ViewDistance);
            }, "Save Weeping Angel");

            SafeExecute(() =>
            {
                SetBool("AutoRun", "Enabled", AutoRunController.Enabled);
            }, "Save Auto Run");

            SafeExecute(() =>
            {
                SetBool("Spinbot", "Enabled", SpinbotController.Enabled);
                SetFloat("Spinbot", "Speed", SpinbotController.Speed);
                SetInt("Spinbot", "Direction", (int)SpinbotController.Direction);
            }, "Save Spinbot");

            SafeExecute(() =>
            {
                SetBool("BunnyHop", "Enabled", BunnyHopController.Enabled);
                SetFloat("BunnyHop", "HopBoost", BunnyHopController.HopBoost);
                SetFloat("BunnyHop", "MaxSpeed", BunnyHopController.MaxSpeed);
                SetFloat("BunnyHop", "AirStrafeForce", BunnyHopController.AirStrafeForce);
                SetFloat("BunnyHop", "JumpForce", BunnyHopController.JumpForce);
                SetBool("BunnyHop", "AutoHop", BunnyHopController.AutoHop);
                SetInt("BunnyHop", "StrafeMode", (int)BunnyHopController.StrafeMode);
                SetFloat("BunnyHop", "StandableNormal", BunnyHopController.StandableNormal);
                SetBool("BunnyHop", "TrimpEnabled", BunnyHopController.TrimpEnabled);
                SetFloat("BunnyHop", "TrimpMultiplier", BunnyHopController.TrimpMultiplier);
            }, "Save BunnyHop");

            SafeExecute(() =>
            {
                SetBool("DefaultWorld", "Enabled", DefaultWorldController.Enabled);
                SetValue("DefaultWorld", "Barcode", DefaultWorldController.Barcode);
                SetValue("DefaultWorld", "LevelName", DefaultWorldController.LevelName);
            }, "Save Default World");

            SafeExecute(() =>
            {
                SetBool("AutoHost", "Enabled", AutoHostController.Enabled);
            }, "Save Auto Host");

            SafeExecute(() =>
            {
                // Enabled is intentionally NOT saved — XYZ Scale always starts off
                SetFloat("XYZScale", "ScaleX", XYZScaleController.ScaleX);
                SetFloat("XYZScale", "ScaleY", XYZScaleController.ScaleY);
                SetFloat("XYZScale", "ScaleZ", XYZScaleController.ScaleZ);
            }, "Save XYZ Scale");

            SafeExecute(() =>
            {
                SetBool("DisableAvatarFX", "Enabled", DisableAvatarFXController.Enabled);
            }, "Save Disable Avatar FX");

            SafeExecute(() =>
            {
                SetBool("HolsterHider", "HideHolsters", HolsterHiderController.HideHolsters);
                SetBool("HolsterHider", "HideAmmoPouch", HolsterHiderController.HideAmmoPouch);
                SetBool("HolsterHider", "HideBodyLog", HolsterHiderController.HideBodyLog);
            }, "Save Holster Hider");

            SafeExecute(() =>
            {
                SetBool("GhostMode", "Enabled", GhostModeController.Enabled);
            }, "Save Ghost Mode");

            SafeExecute(() =>
            {
                SetBool("AntiSlowmo", "Enabled", AntiSlowmoController.Enabled);
            }, "Save Anti-Slowmo");

            SafeExecute(() =>
            {
                SetBool("AntiTeleport", "Enabled", AntiTeleportController.Enabled);
            }, "Save Anti-Teleport");

            SafeExecute(() =>
            {
                SetBool("AntiRagdoll", "Enabled", AntiRagdollController.Enabled);
            }, "Save Anti-Ragdoll");

            SafeExecute(() =>
            {
                SetBool("SpawnLogger", "Enabled", SpawnLoggerController.Enabled);
                SetBool("SpawnLogger", "ShowNotifications", SpawnLoggerController.ShowNotifications);
            }, "Save Spawn Logger");

            SafeExecute(() =>
            {
                SetFloat("DamageMultiplier", "GunMultiplier", DamageMultiplierController.GunMultiplier);
                SetFloat("DamageMultiplier", "MeleeMultiplier", DamageMultiplierController.MeleeMultiplier);
            }, "Save Damage Multiplier");

            SafeExecute(() =>
            {
                SetBool("AvatarLogger", "Enabled", AvatarLoggerController.Enabled);
                SetBool("AvatarLogger", "ShowNotifications", AvatarLoggerController.ShowNotifications);
            }, "Save Avatar Logger");

            SafeExecute(() =>
            {
                SetBool("PlayerActionLogger", "Enabled", PlayerActionLoggerController.Enabled);
                SetBool("PlayerActionLogger", "LogJoins", PlayerActionLoggerController.LogJoins);
                SetBool("PlayerActionLogger", "LogLeaves", PlayerActionLoggerController.LogLeaves);
                SetBool("PlayerActionLogger", "LogDeaths", PlayerActionLoggerController.LogDeaths);
                SetBool("PlayerActionLogger", "ShowNotifications", PlayerActionLoggerController.ShowNotifications);
            }, "Save Player Action Logger");

            SafeExecute(() =>
            {
                SetFloat("Overlay", "Opacity", OverlayMenu.MenuOpacity);
                SetInt("Overlay", "FontSize", OverlayMenu.FontSize);
                SetBool("Overlay", "RainbowTitle", OverlayMenu.RainbowTitle);
                SetFloat("Overlay", "AccentR", OverlayMenu.AccentColor.r);
                SetFloat("Overlay", "AccentG", OverlayMenu.AccentColor.g);
                SetFloat("Overlay", "AccentB", OverlayMenu.AccentColor.b);
                SetFloat("Overlay", "SectionR", OverlayMenu.SectionColor.r);
                SetFloat("Overlay", "SectionG", OverlayMenu.SectionColor.g);
                SetFloat("Overlay", "SectionB", OverlayMenu.SectionColor.b);
                SetFloat("Overlay", "BgR", OverlayMenu.BgColor.r);
                SetFloat("Overlay", "BgG", OverlayMenu.BgColor.g);
                SetFloat("Overlay", "BgB", OverlayMenu.BgColor.b);
                SetFloat("Overlay", "BgA", OverlayMenu.BgColor.a);
                SetBool("Overlay", "GradientEnabled", OverlayMenu.GradientEnabled);
                SetFloat("Overlay", "GradEndR", OverlayMenu.GradientEndColor.r);
                SetFloat("Overlay", "GradEndG", OverlayMenu.GradientEndColor.g);
                SetFloat("Overlay", "GradEndB", OverlayMenu.GradientEndColor.b);
            }, "Save Overlay");

            try
            {
                SavePresets();
                WriteConfigFile();
                _dirty = false;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"SettingsManager save error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark settings as dirty (needs saving).
        /// Call this when any setting changes.
        /// </summary>
        public static void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Execute an action with error isolation so one failure doesn't abort the whole load/save.
        /// </summary>
        private static void SafeExecute(Action action, string context)
        {
            try { action(); }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[Settings] {context}: {ex.Message}");
            }
        }

        /// <summary>
        /// Force an immediate save regardless of dirty flag or timer.
        /// Called on game exit (OnDeinitializeMelon) to prevent data loss.
        /// </summary>
        public static void ForceSave()
        {
            if (!_initialized) return;
            SaveAll();
            Main.MelonLog.Msg("Settings force-saved on exit");
        }

        /// <summary>
        /// Call from OnUpdate - saves periodically when dirty.
        /// </summary>
        public static void Update()
        {
            if (!_initialized || !_dirty) return;

            if (UnityEngine.Time.time - _lastSaveTime >= _saveInterval)
            {
                _lastSaveTime = UnityEngine.Time.time;
                SaveAll();
            }
        }
    }
}
