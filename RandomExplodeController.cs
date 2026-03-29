using MelonLoader;
using UnityEngine;
using BoneLib;
using Il2CppSLZ.Marrow;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    public enum ExplodeLaunchDir { RANDOM, FACING, OPPOSITE, UP }
    public enum ExplodeTarget { SELF, OTHERS, ALL }

    public static class RandomExplodeController
    {
        // Explosion barcodes (same as ExplosivePunchController, minus SmashBone for "Default")
        private const string NormalExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionMissile";
        private const string SuperExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionTimedNuke";
        private const string BlackFlashBarcode = "Curiosity.BlackFlash.Spawnable.BlackFlash";
        private const string TinyExplosiveBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionSmallBigDamage";
        private const string BoomBarcode = "Curiosity.BlackFlash.Spawnable.Boom";

        private static bool _enabled = false;
        private static ExplosionType _explosionType = ExplosionType.Normal;
        private static string _customBarcode = "";
        private static float _interval = 1f;
        private static int _chanceDenom = 10000;
        private static float _launchForce = 50f;
        private static ExplodeLaunchDir _launchDir = ExplodeLaunchDir.RANDOM;
        private static bool _ragdollOnExplode = false;
        private static ExplodeTarget _target = ExplodeTarget.SELF;
        private static bool _controllerShortcut = false;
        private static float _holdDuration = 1.5f;
        private static string _searchQuery = "";
        private static List<(string name, string barcode)> _searchResults = new List<(string, string)>();

        private static float _lastCheckTime = 0f;
        private static System.Random _rng = new System.Random();

        // B+Y hold tracking
        private static float _holdStartTime = -1f;
        private static bool _prevHoldDetected = false;

        // Reflection cache (same pattern as ExplosivePunchController)
        private static MethodInfo _spawnCrateMethod = null;
        private static Type _spawnableCrateRefType = null;
        private static bool _reflectionCached = false;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static ExplosionType SelectedExplosion
        {
            get => _explosionType;
            set => _explosionType = value;
        }

        public static string CustomBarcode
        {
            get => _customBarcode;
            set => _customBarcode = value ?? "";
        }

        public static float Interval
        {
            get => _interval;
            set => _interval = Mathf.Clamp(value, 0.1f, 60f);
        }

        public static int ChanceDenominator
        {
            get => _chanceDenom;
            set => _chanceDenom = Mathf.Clamp(value, 1, 1000000);
        }

        public static float LaunchForce
        {
            get => _launchForce;
            set => _launchForce = Mathf.Clamp(value, 0f, 1000f);
        }

        public static ExplodeLaunchDir LaunchDirection
        {
            get => _launchDir;
            set => _launchDir = value;
        }

        public static bool RagdollOnExplode
        {
            get => _ragdollOnExplode;
            set => _ragdollOnExplode = value;
        }

        public static ExplodeTarget Target
        {
            get => _target;
            set => _target = value;
        }

        public static bool ControllerShortcut
        {
            get => _controllerShortcut;
            set => _controllerShortcut = value;
        }

        public static float HoldDuration
        {
            get => _holdDuration;
            set => _holdDuration = Mathf.Clamp(value, 0.1f, 5f);
        }

        public static string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        public static IReadOnlyList<(string name, string barcode)> SearchResults => _searchResults;

        public static void Initialize() { }

        /// <summary>
        /// Search for spawnables by name and populate results list.
        /// Delegates to SpawnableSearcher which uses direct AssetWarehouse typed access.
        /// </summary>
        public static void SearchSpawnables(BoneLib.BoneMenu.Page resultsPage)
        {
            _searchResults.Clear();

            // Set up SpawnableSearcher with our query and delegate to it
            SpawnableSearcher.SearchQuery = _searchQuery;
            SpawnableSearcher.SearchToPageWithAction(resultsPage, (barcode) =>
            {
                _customBarcode = barcode;
                _explosionType = ExplosionType.Custom;
                Main.MelonLog.Msg($"[RandomExplode] Set custom: {barcode}");
                SettingsManager.MarkDirty();
            });
        }

        public static void Update()
        {
            // B+Y controller shortcut
            if (_controllerShortcut)
                UpdateControllerShortcut();

            if (!_enabled) return;
            if (Time.time - _lastCheckTime < _interval) return;
            _lastCheckTime = Time.time;

            int roll = _rng.Next(1, _chanceDenom + 1);
            if (roll != 1) return;

            TriggerExplosion();
        }

        private static void UpdateControllerShortcut()
        {
            try
            {
                bool yHeld = Input.GetKey(KeyCode.JoystickButton3);
                bool bHeld = Input.GetKey(KeyCode.JoystickButton1);
                bool holdDetected = yHeld && bHeld;

                if (holdDetected && !_prevHoldDetected)
                {
                    _holdStartTime = Time.time;
                }
                else if (holdDetected && _holdStartTime > 0)
                {
                    if (Time.time - _holdStartTime >= _holdDuration)
                    {
                        TriggerExplosion();
                        _holdStartTime = -1f;
                    }
                }
                else if (!holdDetected)
                {
                    _holdStartTime = -1f;
                }

                _prevHoldDetected = holdDetected;
            }
            catch { }
        }

        public static void TriggerExplosion()
        {
            try
            {
                string barcode = GetBarcode();
                if (string.IsNullOrEmpty(barcode)) return;

                switch (_target)
                {
                    case ExplodeTarget.SELF:
                        ExplodeSelf(barcode);
                        break;
                    case ExplodeTarget.OTHERS:
                        ExplodeOthers(barcode);
                        break;
                    case ExplodeTarget.ALL:
                        ExplodeSelf(barcode);
                        ExplodeOthers(barcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RandomExplode] Error: {ex.Message}");
            }
        }

        private static void ExplodeSelf(string barcode)
        {
            var rigManager = Player.RigManager;
            if (rigManager == null) return;

            // Use physics rig pelvis (actual position) instead of RigManager transform (spawn point)
            Vector3 playerPos;
            var physRigPos = rigManager.physicsRig;
            if (physRigPos?.torso?.rbPelvis != null)
                playerPos = physRigPos.torso.rbPelvis.position;
            else if (Player.Head != null)
                playerPos = Player.Head.position;
            else
                playerPos = ((Component)rigManager).transform.position;
            SpawnExplosion(barcode, playerPos, Quaternion.identity);

            if (_launchForce > 0f)
            {
                var physRig = rigManager.physicsRig;
                if (physRig != null)
                {
                    Vector3 dir = GetLaunchDirection();
                    ApplyLaunchForce(physRig, dir * _launchForce);
                }
            }

            if (_ragdollOnExplode)
            {
                try
                {
                    var physRig2 = rigManager.physicsRig;
                    if (physRig2 != null)
                        physRig2.RagdollRig();
                }
                catch { }
            }
        }

        private static void ExplodeOthers(string barcode)
        {
            try
            {
                var head = Player.Head;
                if (head == null) return;

                var target = PlayerTargeting.FindTarget(TargetFilter.NEAREST, head.position);
                if (target == null) return;

                Vector3? targetPos = PlayerTargeting.GetTargetPosition(target);
                if (!targetPos.HasValue) return;

                SpawnExplosion(barcode, targetPos.Value, Quaternion.identity);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RandomExplode] ExplodeOthers error: {ex.Message}");
            }
        }

        private static string GetBarcode()
        {
            switch (_explosionType)
            {
                case ExplosionType.Normal: return NormalExplosionBarcode;
                case ExplosionType.Super: return SuperExplosionBarcode;
                case ExplosionType.BlackFlash: return BlackFlashBarcode;
                case ExplosionType.Tiny: return TinyExplosiveBarcode;
                case ExplosionType.Boom: return BoomBarcode;
                case ExplosionType.Custom: return _customBarcode;
                default: return NormalExplosionBarcode;
            }
        }

        private static Vector3 GetLaunchDirection()
        {
            switch (_launchDir)
            {
                case ExplodeLaunchDir.FACING:
                    return Player.Head != null ? Player.Head.forward : Vector3.up;
                case ExplodeLaunchDir.OPPOSITE:
                    return Player.Head != null ? -Player.Head.forward : Vector3.up;
                case ExplodeLaunchDir.UP:
                    return Vector3.up;
                case ExplodeLaunchDir.RANDOM:
                default:
                    return new Vector3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(0.3f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                    ).normalized;
            }
        }

        private static void ApplyLaunchForce(PhysicsRig physRig, Vector3 force)
        {
            try
            {
                var pelvis = physRig.torso?.rbPelvis;
                if (pelvis != null)
                    pelvis.AddForce(force, ForceMode.VelocityChange);

                var rbs = ((Component)physRig).GetComponentsInChildren<Rigidbody>();
                if (rbs != null)
                {
                    foreach (var rb in rbs)
                    {
                        if (rb != null)
                            rb.AddForce(force * 0.5f, ForceMode.VelocityChange);
                    }
                }
            }
            catch { }
        }

        // ── Spawn logic (same pattern as ExplosivePunchController) ──

        private static bool SpawnExplosion(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                if (!_reflectionCached)
                {
                    CacheReflectionInfo();
                    _reflectionCached = true;
                }

                if (TryNetworkedSpawn(barcode, position, rotation))
                    return true;

                if (_spawnCrateMethod != null)
                    return SpawnWithBoneLib(barcode, position, rotation);

                return SpawnWithAssetWarehouse(barcode, position, rotation);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"[RandomExplode] Spawn error: {ex.Message}");
                return false;
            }
        }

        private static bool TryNetworkedSpawn(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                var networkAssetSpawnerType = FindTypeInAssembly("NetworkAssetSpawner", "LabFusion") ?? FindTypeInAssembly("NetworkAssetSpawner", "");
                var spawnableType = FindTypeInAssembly("Spawnable", "LabFusion") ?? FindTypeInAssembly("Spawnable", "");
                var spawnRequestType = FindTypeInAssembly("SpawnRequestInfo", "LabFusion") ?? FindTypeInAssembly("SpawnRequestInfo", "");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                    return false;

                var crateRefType = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("SpawnableCrateReference", "Assembly-CSharp");
                if (crateRefType == null) return false;

                var crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null) return false;

                object crateRef = crateCtor.Invoke(new object[] { barcode });

                object spawnable = Activator.CreateInstance(spawnableType);
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi) fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi) pi.SetValue(spawnable, crateRef);

                object spawnReq = Activator.CreateInstance(spawnRequestType);

                var spawnableMember = spawnRequestType.GetField("Spawnable") ?? (MemberInfo)spawnRequestType.GetProperty("Spawnable");
                if (spawnableMember is FieldInfo sf) sf.SetValue(spawnReq, spawnable);
                else if (spawnableMember is PropertyInfo sp) sp.SetValue(spawnReq, spawnable);

                var posMember = spawnRequestType.GetField("Position") ?? (MemberInfo)spawnRequestType.GetProperty("Position");
                if (posMember is FieldInfo pf) pf.SetValue(spawnReq, position);
                else if (posMember is PropertyInfo pp) pp.SetValue(spawnReq, position);

                var rotMember = spawnRequestType.GetField("Rotation") ?? (MemberInfo)spawnRequestType.GetProperty("Rotation");
                if (rotMember is FieldInfo rf) rf.SetValue(spawnReq, rotation);
                else if (rotMember is PropertyInfo rp) rp.SetValue(spawnReq, rotation);

                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null) return false;

                spawnMethod.Invoke(null, new object[] { spawnReq });
                return true;
            }
            catch { return false; }
        }

        private static void CacheReflectionInfo()
        {
            try
            {
                var helperType = FindTypeInAssembly("HelperMethods", "BoneLib");
                if (helperType != null)
                {
                    foreach (var m in helperType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name != "SpawnCrate") continue;
                        var ps = m.GetParameters();
                        if (ps.Length >= 2)
                        {
                            if (ps[0].ParameterType == typeof(string))
                            {
                                _spawnCrateMethod = m;
                                break;
                            }
                            if (ps[0].ParameterType.Name.Contains("SpawnableCrateReference"))
                            {
                                _spawnCrateMethod = m;
                                _spawnableCrateRefType = ps[0].ParameterType;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static bool SpawnWithBoneLib(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                var ps = _spawnCrateMethod.GetParameters();
                object firstArg;
                if (ps[0].ParameterType == typeof(string))
                    firstArg = barcode;
                else
                {
                    var ctor = _spawnableCrateRefType?.GetConstructor(new[] { typeof(string) });
                    if (ctor == null) return false;
                    firstArg = ctor.Invoke(new object[] { barcode });
                }

                object[] args = new object[ps.Length];
                args[0] = firstArg;
                args[1] = position;
                if (ps.Length > 2) args[2] = rotation;
                for (int i = (ps.Length > 2 ? 3 : 2); i < ps.Length; i++)
                    args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                _spawnCrateMethod.Invoke(null, args);
                return true;
            }
            catch { return false; }
        }

        private static bool SpawnWithAssetWarehouse(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                var warehouseType = FindTypeInAssembly("AssetWarehouse", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("AssetWarehouse", "");
                if (warehouseType == null) return false;

                var instanceProp = warehouseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var warehouse = instanceProp?.GetValue(null);
                if (warehouse == null) return false;

                var spawnableType = FindTypeInAssembly("Spawnable", "Il2CppSLZ.Marrow");
                if (spawnableType == null) return false;

                object spawnable = Activator.CreateInstance(spawnableType);
                var crateRefField = spawnableType.GetField("crateRef");
                if (crateRefField != null)
                {
                    var crateRefType2 = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow");
                    if (crateRefType2 != null)
                    {
                        var ctor = crateRefType2.GetConstructor(new[] { typeof(string) });
                        if (ctor != null)
                        {
                            object crateRef = ctor.Invoke(new object[] { barcode });
                            crateRefField.SetValue(spawnable, crateRef);
                        }
                    }
                }

                var assetSpawnerType = FindTypeInAssembly("AssetSpawner", "Il2CppSLZ.Marrow");
                if (assetSpawnerType != null)
                {
                    var registerMethod = assetSpawnerType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                    registerMethod?.Invoke(null, new object[] { spawnable });

                    var spawnMethod = assetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                    if (spawnMethod != null)
                    {
                        var sps = spawnMethod.GetParameters();
                        object[] sArgs = new object[sps.Length];
                        sArgs[0] = spawnable;
                        sArgs[1] = position;
                        sArgs[2] = rotation;
                        for (int i = 3; i < sps.Length; i++)
                            sArgs[i] = sps[i].HasDefaultValue ? sps[i].DefaultValue : null;
                        spawnMethod.Invoke(null, sArgs);
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static Type FindTypeInAssembly(string typeName, string assemblyHint)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(assemblyHint) && !asm.GetName().Name.Contains(assemblyHint))
                        continue;
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
