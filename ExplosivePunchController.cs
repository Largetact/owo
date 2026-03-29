using MelonLoader;
using UnityEngine;
using BoneLib;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace BonelabUtilityMod
{
    public enum PunchHandMode { BOTH, SEPARATE }
    public enum ExplosionType { Normal, Super, BlackFlash, Tiny, Boom, Custom, NONE }
    public enum MatrixMode { DISABLED, SQUARE, CIRCLE }

    public static class ExplosivePunchController
    {
        // Explosion barcodes
        private const string NormalExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionMissile";
        private const string SuperExplosionBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionTimedNuke";
        private const string BlackFlashBarcode = "Curiosity.BlackFlash.Spawnable.BlackFlash";
        private const string TinyExplosiveBarcode = "BaBaCorp.MiscExplosiveDevices.Spawnable.ExplosionSmallBigDamage";
        private const string BoomBarcode = "Curiosity.BlackFlash.Spawnable.Boom";
        private const string SmashBoneBarcode = "Lakatrazz.FusionContent.Spawnable.DeathExplosion";

        // ═══════════════════════════════════════════════════
        // GLOBAL SETTINGS (shared across modes)
        // ═══════════════════════════════════════════════════
        private static bool legacyPunchEnabled = false;
        private static float spawnDelay = 0f;
        private static float punchVelocityThreshold = 4f;
        private static float punchCooldown = 0.15f;
        private static bool rigCheckOnly = true;
        private static bool faceTarget = false;
        private static PunchHandMode punchMode = PunchHandMode.BOTH;

        // Punch spawn matrix (count + gap)
        private static int punchSpawnCount = 1;
        private static float punchSpacing = 0.5f;
        private static MatrixMode punchMatrixMode = MatrixMode.SQUARE;

        // ═══════════════════════════════════════════════════
        // BOTH MODE SETTINGS (original behavior)
        // ═══════════════════════════════════════════════════
        private static bool explosivePunchEnabled = false;
        private static bool superExplosivePunchEnabled = false;
        private static bool blackFlashEnabled = false;
        private static bool tinyExplosiveEnabled = false;
        private static bool boomEnabled = false;
        private static bool customPunchEnabled = false;
        private static string customPunchBarcode = "";
        private static bool smashBoneEnabled = false;
        private static int smashBoneCount = 1;
        private static bool smashBoneFlip = false;
        private static bool cosmeticEnabled = false;
        private static string cosmeticBarcode = "";
        private static int cosmeticCount = 1;
        private static bool cosmeticFlip = false;

        // ═══════════════════════════════════════════════════
        // SEPARATE MODE SETTINGS (per-hand)
        // ═══════════════════════════════════════════════════
        // Left hand
        private static ExplosionType leftExplosionType = ExplosionType.Normal;
        private static string leftCustomBarcode = "";
        private static bool leftSmashBoneEnabled = false;
        private static int leftSmashBoneCount = 1;
        private static bool leftSmashBoneFlip = false;
        private static bool leftCosmeticEnabled = false;
        private static string leftCosmeticBarcode = "";
        private static int leftCosmeticCount = 1;
        private static bool leftCosmeticFlip = false;

        // Right hand
        private static ExplosionType rightExplosionType = ExplosionType.Normal;
        private static string rightCustomBarcode = "";
        private static bool rightSmashBoneEnabled = false;
        private static int rightSmashBoneCount = 1;
        private static bool rightSmashBoneFlip = false;
        private static bool rightCosmeticEnabled = false;
        private static string rightCosmeticBarcode = "";
        private static int rightCosmeticCount = 1;
        private static bool rightCosmeticFlip = false;

        // State tracking
        private static Vector3 prevLeftHandPos;
        private static Vector3 prevRightHandPos;
        private static bool handPositionsInitialized = false;
        private static float lastPunchTime = 0f;

        // Cached reflection info
        private static MethodInfo spawnCrateMethod = null;
        private static Type spawnableCrateRefType = null;
        private static bool reflectionCacheAttempted = false;

        // ═══════════════════════════════════════════════════
        // PROPERTIES — Global
        // ═══════════════════════════════════════════════════
        public static PunchHandMode PunchMode { get => punchMode; set => punchMode = value; }
        public static bool IsLegacyPunchEnabled { get => legacyPunchEnabled; set => legacyPunchEnabled = value; }
        public static float PunchVelocityThreshold { get => punchVelocityThreshold; set => punchVelocityThreshold = Mathf.Clamp(value, 1f, 15f); }
        public static float SpawnDelay { get => spawnDelay; set => spawnDelay = Mathf.Clamp(value, 0f, 0.5f); }
        public static float PunchCooldown { get => punchCooldown; set => punchCooldown = Mathf.Clamp(value, 0.05f, 1f); }
        public static bool RigCheckOnly { get => rigCheckOnly; set => rigCheckOnly = value; }
        public static bool FaceTarget { get => faceTarget; set => faceTarget = value; }
        public static int PunchSpawnCount { get => punchSpawnCount; set => punchSpawnCount = Mathf.Clamp(value, 1, 25); }
        public static float PunchSpacing { get => punchSpacing; set => punchSpacing = Mathf.Clamp(value, 0.1f, 10f); }
        public static MatrixMode PunchMatrixMode { get => punchMatrixMode; set => punchMatrixMode = value; }

        // ═══════════════════════════════════════════════════
        // PROPERTIES — BOTH mode
        // ═══════════════════════════════════════════════════
        public static bool IsExplosivePunchEnabled { get => explosivePunchEnabled; set => explosivePunchEnabled = value; }
        public static bool IsSuperExplosivePunchEnabled { get => superExplosivePunchEnabled; set => superExplosivePunchEnabled = value; }
        public static bool IsBlackFlashEnabled { get => blackFlashEnabled; set => blackFlashEnabled = value; }
        public static bool IsTinyExplosiveEnabled { get => tinyExplosiveEnabled; set => tinyExplosiveEnabled = value; }
        public static bool IsBoomEnabled { get => boomEnabled; set => boomEnabled = value; }
        public static bool IsCustomPunchEnabled { get => customPunchEnabled; set => customPunchEnabled = value; }
        public static string CustomPunchBarcode { get => customPunchBarcode; set => customPunchBarcode = value ?? ""; }
        public static bool IsSmashBoneEnabled { get => smashBoneEnabled; set => smashBoneEnabled = value; }
        public static int SmashBoneCount { get => smashBoneCount; set => smashBoneCount = Mathf.Clamp(value, 1, 20); }
        public static bool SmashBoneFlip { get => smashBoneFlip; set => smashBoneFlip = value; }
        public static bool IsCosmeticEnabled { get => cosmeticEnabled; set => cosmeticEnabled = value; }
        public static string CosmeticBarcode { get => cosmeticBarcode; set => cosmeticBarcode = value ?? ""; }
        public static int CosmeticCount { get => cosmeticCount; set => cosmeticCount = Mathf.Clamp(value, 1, 20); }
        public static bool CosmeticFlip { get => cosmeticFlip; set => cosmeticFlip = value; }

        // ═══════════════════════════════════════════════════
        // PROPERTIES — SEPARATE mode (Left Hand)
        // ═══════════════════════════════════════════════════
        public static ExplosionType LeftExplosionType { get => leftExplosionType; set => leftExplosionType = value; }
        public static string LeftCustomBarcode { get => leftCustomBarcode; set => leftCustomBarcode = value ?? ""; }
        public static bool LeftSmashBoneEnabled { get => leftSmashBoneEnabled; set => leftSmashBoneEnabled = value; }
        public static int LeftSmashBoneCount { get => leftSmashBoneCount; set => leftSmashBoneCount = Mathf.Clamp(value, 1, 20); }
        public static bool LeftSmashBoneFlip { get => leftSmashBoneFlip; set => leftSmashBoneFlip = value; }
        public static bool LeftCosmeticEnabled { get => leftCosmeticEnabled; set => leftCosmeticEnabled = value; }
        public static string LeftCosmeticBarcode { get => leftCosmeticBarcode; set => leftCosmeticBarcode = value ?? ""; }
        public static int LeftCosmeticCount { get => leftCosmeticCount; set => leftCosmeticCount = Mathf.Clamp(value, 1, 20); }
        public static bool LeftCosmeticFlip { get => leftCosmeticFlip; set => leftCosmeticFlip = value; }

        // ═══════════════════════════════════════════════════
        // PROPERTIES — SEPARATE mode (Right Hand)
        // ═══════════════════════════════════════════════════
        public static ExplosionType RightExplosionType { get => rightExplosionType; set => rightExplosionType = value; }
        public static string RightCustomBarcode { get => rightCustomBarcode; set => rightCustomBarcode = value ?? ""; }
        public static bool RightSmashBoneEnabled { get => rightSmashBoneEnabled; set => rightSmashBoneEnabled = value; }
        public static int RightSmashBoneCount { get => rightSmashBoneCount; set => rightSmashBoneCount = Mathf.Clamp(value, 1, 20); }
        public static bool RightSmashBoneFlip { get => rightSmashBoneFlip; set => rightSmashBoneFlip = value; }
        public static bool RightCosmeticEnabled { get => rightCosmeticEnabled; set => rightCosmeticEnabled = value; }
        public static string RightCosmeticBarcode { get => rightCosmeticBarcode; set => rightCosmeticBarcode = value ?? ""; }
        public static int RightCosmeticCount { get => rightCosmeticCount; set => rightCosmeticCount = Mathf.Clamp(value, 1, 20); }
        public static bool RightCosmeticFlip { get => rightCosmeticFlip; set => rightCosmeticFlip = value; }

        public static void Initialize()
        {
            Main.MelonLog.Msg("Explosive Punch Controller initialized");
        }

        public static void Update()
        {
            bool anyEnabled;
            if (punchMode == PunchHandMode.BOTH)
            {
                anyEnabled = explosivePunchEnabled || superExplosivePunchEnabled || blackFlashEnabled ||
                             tinyExplosiveEnabled || boomEnabled || customPunchEnabled ||
                             smashBoneEnabled || cosmeticEnabled;
            }
            else
            {
                anyEnabled = leftExplosionType != ExplosionType.NONE || rightExplosionType != ExplosionType.NONE ||
                             leftSmashBoneEnabled || rightSmashBoneEnabled ||
                             leftCosmeticEnabled || rightCosmeticEnabled;
            }

            if (!anyEnabled)
                return;

            // When Legacy Punch is enabled, detection is handled by the Harmony patch on HandSFX.PunchAttack
            if (legacyPunchEnabled)
                return;

            try
            {
                UpdatePunchDetection();
            }
            catch { }
        }

        private static void UpdatePunchDetection()
        {
            var leftHand = Player.LeftHand;
            var rightHand = Player.RightHand;

            if (leftHand == null || rightHand == null)
                return;

            Vector3 leftHandPos = leftHand.transform.position;
            Vector3 rightHandPos = rightHand.transform.position;

            // Initialize positions on first frame
            if (!handPositionsInitialized)
            {
                prevLeftHandPos = leftHandPos;
                prevRightHandPos = rightHandPos;
                handPositionsInitialized = true;
                return;
            }

            // Calculate hand velocities
            float dt = Time.deltaTime;
            if (dt <= 0) dt = 0.016f;

            Vector3 leftVelocity = (leftHandPos - prevLeftHandPos) / dt;
            Vector3 rightVelocity = (rightHandPos - prevRightHandPos) / dt;

            // Check cooldown
            if (Time.time - lastPunchTime < punchCooldown)
            {
                prevLeftHandPos = leftHandPos;
                prevRightHandPos = rightHandPos;
                return;
            }

            // Safety: Check if trigger + grip are held for each hand
            // Use Oculus cross-platform axis names (same as ObjectLauncher)
            bool leftTriggerHeld = false;
            bool leftGripHeld = false;
            bool rightTriggerHeld = false;
            bool rightGripHeld = false;

            try
            {
                leftTriggerHeld = Input.GetAxis("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.5f;
                leftGripHeld = Input.GetAxis("Oculus_CrossPlatform_PrimaryHandTrigger") > 0.5f;
                rightTriggerHeld = Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.5f;
                rightGripHeld = Input.GetAxis("Oculus_CrossPlatform_SecondaryHandTrigger") > 0.5f;
            }
            catch { }

            // Keyboard fallback for testing
            if (Input.GetKey(KeyCode.LeftControl)) { leftTriggerHeld = true; leftGripHeld = true; }
            if (Input.GetKey(KeyCode.RightControl)) { rightTriggerHeld = true; rightGripHeld = true; }

            bool leftSafetyOff = leftTriggerHeld && leftGripHeld;
            bool rightSafetyOff = rightTriggerHeld && rightGripHeld;

            // Check left hand for punch (only if safety is off)
            if (leftSafetyOff && leftVelocity.magnitude >= punchVelocityThreshold)
            {
                TryExplosivePunch(leftHandPos, leftVelocity, true);
            }

            // Check right hand for punch (only if safety is off)
            if (rightSafetyOff && rightVelocity.magnitude >= punchVelocityThreshold)
            {
                TryExplosivePunch(rightHandPos, rightVelocity, false);
            }

            // Store for next frame
            prevLeftHandPos = leftHandPos;
            prevRightHandPos = rightHandPos;
        }

        // ═══════════════════════════════════════════════════
        // HELPERS — Per-hand config resolution
        // ═══════════════════════════════════════════════════

        private static string GetExplosionBarcode(bool isLeftHand)
        {
            if (punchMode == PunchHandMode.BOTH)
            {
                if (customPunchEnabled && !string.IsNullOrEmpty(customPunchBarcode))
                    return customPunchBarcode;
                if (boomEnabled) return BoomBarcode;
                if (blackFlashEnabled) return BlackFlashBarcode;
                if (tinyExplosiveEnabled) return TinyExplosiveBarcode;
                if (superExplosivePunchEnabled) return SuperExplosionBarcode;
                if (explosivePunchEnabled) return NormalExplosionBarcode;
                return null;
            }
            else
            {
                var type = isLeftHand ? leftExplosionType : rightExplosionType;
                switch (type)
                {
                    case ExplosionType.Custom:
                        var bc = isLeftHand ? leftCustomBarcode : rightCustomBarcode;
                        return !string.IsNullOrEmpty(bc) ? bc : null;
                    case ExplosionType.Boom: return BoomBarcode;
                    case ExplosionType.BlackFlash: return BlackFlashBarcode;
                    case ExplosionType.Tiny: return TinyExplosiveBarcode;
                    case ExplosionType.Super: return SuperExplosionBarcode;
                    case ExplosionType.Normal: return NormalExplosionBarcode;
                    default: return null;
                }
            }
        }

        private static void GetSmashBoneConfig(bool isLeftHand, out bool enabled, out int count)
        {
            if (punchMode == PunchHandMode.BOTH)
            {
                enabled = smashBoneEnabled;
                count = smashBoneCount;
            }
            else
            {
                enabled = isLeftHand ? leftSmashBoneEnabled : rightSmashBoneEnabled;
                count = isLeftHand ? leftSmashBoneCount : rightSmashBoneCount;
            }
        }

        private static bool GetSmashBoneFlip(bool isLeftHand)
        {
            if (punchMode == PunchHandMode.BOTH)
                return smashBoneFlip;
            return isLeftHand ? leftSmashBoneFlip : rightSmashBoneFlip;
        }

        private static bool GetCosmeticFlip(bool isLeftHand)
        {
            if (punchMode == PunchHandMode.BOTH)
                return cosmeticFlip;
            return isLeftHand ? leftCosmeticFlip : rightCosmeticFlip;
        }

        private static void GetCosmeticConfig(bool isLeftHand, out bool enabled, out string barcode, out int count)
        {
            if (punchMode == PunchHandMode.BOTH)
            {
                enabled = cosmeticEnabled;
                barcode = cosmeticBarcode;
                count = cosmeticCount;
            }
            else
            {
                enabled = isLeftHand ? leftCosmeticEnabled : rightCosmeticEnabled;
                barcode = isLeftHand ? leftCosmeticBarcode : rightCosmeticBarcode;
                count = isLeftHand ? leftCosmeticCount : rightCosmeticCount;
            }
        }

        private static void SpawnMultiple(string barcode, int count, Vector3 position, Quaternion rotation, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                if (delay > 0f)
                    MelonCoroutines.Start(DelayedSpawn(barcode, position, rotation, delay));
                else
                    SpawnEffect(barcode, position, rotation);
            }
        }

        private static void TryExplosivePunch(Vector3 handPos, Vector3 handVelocity, bool isLeftHand)
        {
            try
            {
                // Find nearby colliders to check if we're punching something
                var colliders = Physics.OverlapSphere(handPos, 0.5f);
                bool hitSomething = false;

                var myRigManager = Player.RigManager;
                Transform myRigRoot = null;
                if (myRigManager != null)
                {
                    var rmComp = myRigManager as UnityEngine.Component;
                    if (rmComp != null) myRigRoot = rmComp.transform.root;
                }

                foreach (var col in colliders)
                {
                    if (col == null)
                        continue;

                    // Skip if it's our own player rig
                    if (myRigRoot != null && col.transform.root == myRigRoot)
                        continue;

                    if (rigCheckOnly)
                    {
                        // === RIG CHECK MODE ===
                        // Only activate on things with Rigidbody (NPCs, spawnables, player rigs)
                        // Skip static world geometry (no RB, kinematic RB without InteractableHost)

                        // Check 1: Has a non-kinematic Rigidbody (dynamic object / NPC limb / spawnable)
                        var rb = col.attachedRigidbody;
                        if (rb != null && !rb.isKinematic)
                        {
                            hitSomething = true;
                            break;
                        }

                        // Check 2: Has a RigManager (player rig or NPC rig with kinematic bones)
                        try
                        {
                            var otherRig = col.GetComponentInParent<RigManager>();
                            if (otherRig != null && otherRig != myRigManager)
                            {
                                hitSomething = true;
                                break;
                            }
                        }
                        catch { }

                        // Check 3: InteractableHost (grabbable items that may have kinematic RBs)
                        try
                        {
                            var host = col.GetComponentInParent<InteractableHost>();
                            if (host != null)
                            {
                                hitSomething = true;
                                break;
                            }
                        }
                        catch { }

                        // No match → static world geometry → skip
                    }
                    else
                    {
                        // === NO CHECK MODE ===
                        // Activate on any collider (including walls/floors)
                        hitSomething = true;
                        break;
                    }
                }

                if (!hitSomething)
                    return;

                // Check if the PUNCHING hand is holding a non-person item — if so, skip
                // (Grabbing an NPC/player rig is fine, only skip for weapons/items)
                try
                {
                    var rigManager = Player.RigManager;
                    if (rigManager != null)
                    {
                        var physRig = rigManager.physicsRig;
                        if (physRig != null)
                        {
                            Hand punchingHand = isLeftHand ? physRig.leftHand : physRig.rightHand;
                            if (punchingHand != null && punchingHand.HasAttachedObject())
                            {
                                // Check if we're grabbing a person (has RigManager) — allow punch
                                var attachedGO = punchingHand.m_CurrentAttachedGO;
                                if (attachedGO != null)
                                {
                                    var heldRig = attachedGO.GetComponentInParent<RigManager>();
                                    if (heldRig == null || heldRig == rigManager)
                                        return; // Holding an item/weapon, not a person — skip
                                }
                            }
                        }
                    }
                }
                catch { /* If hold check fails, proceed anyway */ }

                // Select barcode based on mode + hand
                string barcodeToSpawn = GetExplosionBarcode(isLeftHand);
                GetSmashBoneConfig(isLeftHand, out bool sbEnabled, out int sbCount);
                GetCosmeticConfig(isLeftHand, out bool cosEnabled, out string cosBarcode, out int cosCount);

                // Nothing to spawn?
                if (barcodeToSpawn == null && !sbEnabled && !cosEnabled)
                    return;

                // Offset spawn position slightly in punch direction so it spawns on the target
                Vector3 spawnPos = handPos + handVelocity.normalized * 0.3f;

                // Calculate rotation: face back toward the player or identity
                Quaternion spawnRot = faceTarget && handVelocity.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(-handVelocity.normalized)
                    : Quaternion.identity;

                // SmashBone uses FusionPunch-style rotation (faces forward along velocity)
                Vector3 smashDir = GetSmashBoneFlip(isLeftHand) ? -handVelocity.normalized : handVelocity.normalized;
                Quaternion smashRot = handVelocity.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(smashDir)
                    : Quaternion.identity;

                // Cosmetic rotation: flip reverses the cosmetic effect direction
                Quaternion cosRot = spawnRot;
                if (GetCosmeticFlip(isLeftHand) && handVelocity.sqrMagnitude > 0.01f)
                    cosRot = Quaternion.LookRotation(handVelocity.normalized);

                lastPunchTime = Time.time;

                // Calculate matrix direction vectors once
                Vector3 matForward = handVelocity.sqrMagnitude > 0.01f ? handVelocity.normalized : Vector3.forward;
                Vector3 matRight = Vector3.Cross(Vector3.up, matForward).normalized;
                if (matRight.sqrMagnitude < 0.01f) matRight = Vector3.right;
                Vector3 matUp = Vector3.Cross(matForward, matRight).normalized;

                // Spawn explosion effect(s) — matrix pattern based on mode
                if (barcodeToSpawn != null)
                {
                    if (punchMatrixMode == MatrixMode.DISABLED || punchSpawnCount <= 1)
                    {
                        SpawnMultiple(barcodeToSpawn, punchSpawnCount, spawnPos, spawnRot, spawnDelay);
                    }
                    else
                    {
                        var offsets = CalculateMatrixOffsets(punchSpawnCount, punchSpacing, matRight, matUp, punchMatrixMode);
                        foreach (var offset in offsets)
                            SpawnMultiple(barcodeToSpawn, 1, spawnPos + offset, spawnRot, spawnDelay);
                    }
                }

                // Spawn SmashBone effect(s) — also uses matrix
                if (sbEnabled)
                {
                    if (punchMatrixMode == MatrixMode.DISABLED || punchSpawnCount <= 1)
                    {
                        SpawnMultiple(SmashBoneBarcode, sbCount, spawnPos, smashRot, spawnDelay);
                    }
                    else
                    {
                        var offsets = CalculateMatrixOffsets(punchSpawnCount, punchSpacing, matRight, matUp, punchMatrixMode);
                        foreach (var offset in offsets)
                            SpawnMultiple(SmashBoneBarcode, sbCount, spawnPos + offset, smashRot, spawnDelay);
                    }
                }

                // Spawn Cosmetic effect(s) — also uses matrix
                if (cosEnabled && !string.IsNullOrEmpty(cosBarcode))
                {
                    if (punchMatrixMode == MatrixMode.DISABLED || punchSpawnCount <= 1)
                    {
                        SpawnMultiple(cosBarcode, cosCount, spawnPos, cosRot, spawnDelay);
                    }
                    else
                    {
                        var offsets = CalculateMatrixOffsets(punchSpawnCount, punchSpacing, matRight, matUp, punchMatrixMode);
                        foreach (var offset in offsets)
                            SpawnMultiple(cosBarcode, cosCount, spawnPos + offset, cosRot, spawnDelay);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Punch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine to spawn the explosion after a configurable delay.
        /// </summary>
        private static IEnumerator DelayedSpawn(string barcode, Vector3 position, Quaternion rotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnExplosion(barcode, position, rotation);
        }

        private static bool SpawnExplosion(string barcode, Vector3 position, Quaternion rotation = default)
        {
            try
            {
                if (rotation == default) rotation = Quaternion.identity;

                // Cache reflection info once
                if (!reflectionCacheAttempted)
                {
                    CacheReflectionInfo();
                    reflectionCacheAttempted = true;
                }

                // Method 1: Try LabFusion NetworkAssetSpawner first (for multiplayer sync)
                bool networkedSpawn = TryNetworkedSpawn(barcode, position, rotation);
                if (networkedSpawn)
                    return true;

                // Method 2: Use BoneLib HelperMethods.SpawnCrate as fallback
                if (spawnCrateMethod != null)
                    return SpawnWithBoneLib(barcode, position, rotation);

                // Method 3: Direct AssetWarehouse approach
                return SpawnWithAssetWarehouse(barcode, position, rotation);
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to spawn explosion: {ex.Message}");
                return false;
            }
        }

        private static bool TryNetworkedSpawn(string barcode, Vector3 position, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            try
            {
                // Find required LabFusion types
                var networkAssetSpawnerType = FindTypeInAssembly("NetworkAssetSpawner", "LabFusion") ?? FindTypeInAssembly("NetworkAssetSpawner", "");
                var spawnableType = FindTypeInAssembly("Spawnable", "LabFusion") ?? FindTypeInAssembly("Spawnable", "");
                var spawnRequestType = FindTypeInAssembly("SpawnRequestInfo", "LabFusion") ?? FindTypeInAssembly("SpawnRequestInfo", "");

                if (networkAssetSpawnerType == null || spawnableType == null || spawnRequestType == null)
                {
                    return false; // LabFusion not available
                }

                // Create Spawnable instance and set crateRef = new SpawnableCrateReference(barcode)
                var crateRefType = FindTypeInAssembly("SpawnableCrateReference", "Il2CppSLZ.Marrow") ?? FindTypeInAssembly("SpawnableCrateReference", "Assembly-CSharp");
                if (crateRefType == null)
                {
                    return false;
                }

                ConstructorInfo crateCtor = crateRefType.GetConstructor(new[] { typeof(string) });
                if (crateCtor == null)
                {
                    return false;
                }

                object crateRef = crateCtor.Invoke(new object[] { barcode });

                object spawnable = Activator.CreateInstance(spawnableType);
                var crateField = spawnableType.GetField("crateRef") ?? (MemberInfo)spawnableType.GetProperty("crateRef");
                if (crateField is FieldInfo fi)
                    fi.SetValue(spawnable, crateRef);
                else if (crateField is PropertyInfo pi)
                    pi.SetValue(spawnable, crateRef);

                // Create SpawnRequestInfo and set fields
                object spawnReq = Activator.CreateInstance(spawnRequestType);

                var spawnableMember = spawnRequestType.GetField("Spawnable") ?? (MemberInfo)spawnRequestType.GetProperty("Spawnable");
                if (spawnableMember is FieldInfo sf)
                    sf.SetValue(spawnReq, spawnable);
                else if (spawnableMember is PropertyInfo sp)
                    sp.SetValue(spawnReq, spawnable);

                var posMember = spawnRequestType.GetField("Position") ?? (MemberInfo)spawnRequestType.GetProperty("Position");
                if (posMember is FieldInfo pf)
                    pf.SetValue(spawnReq, position);
                else if (posMember is PropertyInfo pp)
                    pp.SetValue(spawnReq, position);

                var rotMember = spawnRequestType.GetField("Rotation") ?? (MemberInfo)spawnRequestType.GetProperty("Rotation");
                if (rotMember is FieldInfo rf)
                    rf.SetValue(spawnReq, rotation);
                else if (rotMember is PropertyInfo rp)
                    rp.SetValue(spawnReq, rotation);

                // Find Spawn method and invoke
                var spawnMethod = networkAssetSpawnerType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (spawnMethod == null)
                {
                    return false;
                }

                spawnMethod.Invoke(null, new object[] { spawnReq });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void CacheReflectionInfo()
        {
            try
            {
                // Find BoneLib HelperMethods
                var helperMethodsType = FindTypeInAssembly("HelperMethods", "BoneLib");
                if (helperMethodsType != null)
                {
                    var candidates = helperMethodsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (var m in candidates)
                    {
                        if (m.Name != "SpawnCrate") continue;
                        var ps = m.GetParameters();
                        if (ps.Length >= 2)
                        {
                            var firstType = ps[0].ParameterType;
                            // Prefer the one that takes a string barcode
                            if (firstType == typeof(string))
                            {
                                spawnCrateMethod = m;
                                break;
                            }
                            // Otherwise use SpawnableCrateReference
                            if (firstType.Name.Contains("SpawnableCrateReference"))
                            {
                                spawnCrateMethod = m;
                                spawnableCrateRefType = firstType;
                            }
                        }
                    }
                }

                if (spawnCrateMethod != null)
                {
                    Main.MelonLog.Msg($"Cached SpawnCrate method: {spawnCrateMethod}");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Failed to cache reflection info: {ex.Message}");
            }
        }

        private static bool SpawnWithBoneLib(string barcode, Vector3 position, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            try
            {
                var ps = spawnCrateMethod.GetParameters();
                var args = new System.Collections.Generic.List<object>();

                for (int i = 0; i < ps.Length; ++i)
                {
                    var p = ps[i];
                    var pType = p.ParameterType;

                    if (i == 0)
                    {
                        // First param: barcode or SpawnableCrateReference
                        if (pType == typeof(string))
                        {
                            args.Add(barcode);
                        }
                        else if (spawnableCrateRefType != null)
                        {
                            // Construct SpawnableCrateReference
                            object crateRef = null;
                            var ctor = spawnableCrateRefType.GetConstructor(new[] { typeof(string) });
                            if (ctor != null)
                            {
                                crateRef = ctor.Invoke(new object[] { barcode });
                            }
                            if (crateRef == null)
                            {
                                // Try Barcode constructor
                                var barcodeType = FindTypeInAssembly("Barcode", "Il2CppSLZ.Marrow");
                                if (barcodeType != null)
                                {
                                    var bcCtor = barcodeType.GetConstructor(new[] { typeof(string) });
                                    if (bcCtor != null)
                                    {
                                        var bc = bcCtor.Invoke(new object[] { barcode });
                                        var refCtor = spawnableCrateRefType.GetConstructor(new[] { barcodeType });
                                        if (refCtor != null)
                                        {
                                            crateRef = refCtor.Invoke(new object[] { bc });
                                        }
                                    }
                                }
                            }
                            args.Add(crateRef);
                        }
                        else
                        {
                            args.Add(barcode);
                        }
                        continue;
                    }

                    // Position
                    if (pType == typeof(Vector3) && i == 1)
                    {
                        args.Add(position);
                        continue;
                    }

                    // Rotation
                    if (pType == typeof(Quaternion))
                    {
                        args.Add(rotation);
                        continue;
                    }

                    // Scale
                    if (pType == typeof(Vector3))
                    {
                        args.Add(Vector3.one);
                        continue;
                    }

                    // Bool params
                    if (pType == typeof(bool))
                    {
                        args.Add(true);
                        continue;
                    }

                    // Action<GameObject> callback
                    if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(System.Action<>))
                    {
                        args.Add(null);
                        continue;
                    }

                    // Fallback
                    args.Add(null);
                }

                spawnCrateMethod.Invoke(null, args.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"BoneLib spawn failed: {ex.Message}");
                return false;
            }
        }

        private static bool SpawnWithAssetWarehouse(string barcode, Vector3 position, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            try
            {
                // Find AssetWarehouse
                var assetWarehouseType = FindTypeInAssembly("AssetWarehouse", "Il2CppSLZ.Marrow");
                if (assetWarehouseType == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse type not found");
                    return false;
                }

                // Get Instance
                var instanceProp = assetWarehouseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse.Instance not found");
                    return false;
                }

                var warehouse = instanceProp.GetValue(null);
                if (warehouse == null)
                {
                    Main.MelonLog.Warning("AssetWarehouse instance is null");
                    return false;
                }

                // Get crate by barcode
                var barcodeType = FindTypeInAssembly("Barcode", "Il2CppSLZ.Marrow");
                if (barcodeType == null)
                    return false;

                var bcCtor = barcodeType.GetConstructor(new[] { typeof(string) });
                if (bcCtor == null)
                    return false;

                var barcodeObj = bcCtor.Invoke(new object[] { barcode });

                // Try GetCrate method
                var getCrateMethod = assetWarehouseType.GetMethod("GetCrate", new[] { barcodeType });
                if (getCrateMethod == null)
                {
                    // Try other overloads
                    var methods = assetWarehouseType.GetMethods();
                    foreach (var m in methods)
                    {
                        if (m.Name == "GetCrate" && m.GetParameters().Length == 1)
                        {
                            getCrateMethod = m;
                            break;
                        }
                    }
                }

                if (getCrateMethod == null)
                {
                    Main.MelonLog.Warning("GetCrate method not found");
                    return false;
                }

                var crate = getCrateMethod.Invoke(warehouse, new object[] { barcodeObj });
                if (crate == null)
                {
                    Main.MelonLog.Warning($"Crate not found for barcode: {barcode}");
                    return false;
                }

                // Spawn the crate
                var spawnMethod = crate.GetType().GetMethod("Spawn", BindingFlags.Public | BindingFlags.Instance);
                if (spawnMethod != null)
                {
                    spawnMethod.Invoke(crate, new object[] { position, rotation });
                    return true;
                }

                Main.MelonLog.Warning("No Spawn method found on crate");
                return false;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"AssetWarehouse spawn failed: {ex.Message}");
                return false;
            }
        }

        private static Type FindTypeInAssembly(string typeName, string assemblyNameContains)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.IsNullOrEmpty(assemblyNameContains) && !asm.FullName.Contains(assemblyNameContains))
                    continue;

                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch { }
            }
            return null;
        }

        // ═══════════════════════════════════════════════════
        // LEGACY PUNCH (ported from FusionPunchEffect)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Called by the Harmony patch on HandSFX.PunchAttack.
        /// Spawns an effect when the player punches any object that triggers a PunchAttack.
        /// </summary>
        public static void OnLegacyPunch(HandSFX instance, Collider hitCollider, Rigidbody hitRigidbody, Vector3 relativeVelocity)
        {
            if (!legacyPunchEnabled) return;

            try
            {
                // Only trigger for the local player
                RigManager localRig = Player.RigManager;
                if (localRig == null) return;

                RigManager punchRig = ((Component)instance).GetComponentInParent<RigManager>();
                if (punchRig == null || punchRig != localRig) return;

                // Skip if we punched ourselves
                if (hitCollider != null && ((Component)hitCollider).transform.IsChildOf(((Component)localRig).transform))
                    return;

                // Verify the hit target has a rigidbody or MarrowBody
                bool validHit = false;
                if (hitRigidbody != null)
                    validHit = true;
                else if (hitCollider != null)
                {
                    try { validHit = MarrowBody.Cache.Get(((Component)hitCollider).gameObject) != null; }
                    catch { }
                }

                if (!validHit) return;

                // Cooldown check
                if (Time.time - lastPunchTime < punchCooldown) return;

                // Detect which hand this HandSFX belongs to
                bool isLeftHand = false;
                try
                {
                    var physRig = localRig.physicsRig;
                    if (physRig != null)
                    {
                        Transform sfxTransform = ((Component)instance).transform;
                        Transform leftHandTransform = ((Component)physRig.leftHand).transform;
                        Transform rightHandTransform = ((Component)physRig.rightHand).transform;
                        float distLeft = Vector3.Distance(sfxTransform.position, leftHandTransform.position);
                        float distRight = Vector3.Distance(sfxTransform.position, rightHandTransform.position);
                        isLeftHand = distLeft < distRight;
                    }
                }
                catch { }

                Vector3 spawnPos = ((Component)instance).transform.position;
                Quaternion spawnRot = faceTarget && relativeVelocity.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(-relativeVelocity.normalized)
                    : Quaternion.identity;

                // SmashBone uses FusionPunch-style rotation (forward along velocity)
                Vector3 legacySmashDir = GetSmashBoneFlip(isLeftHand) ? -relativeVelocity.normalized : relativeVelocity.normalized;
                Quaternion smashRot = relativeVelocity.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(legacySmashDir)
                    : Quaternion.identity;

                // Cosmetic rotation: flip reverses the cosmetic effect direction
                Quaternion cosRot = spawnRot;
                if (GetCosmeticFlip(isLeftHand) && relativeVelocity.sqrMagnitude > 0.01f)
                    cosRot = Quaternion.LookRotation(relativeVelocity.normalized);

                // Get per-hand config
                string explosionBarcode = GetExplosionBarcode(isLeftHand);
                GetSmashBoneConfig(isLeftHand, out bool sbEnabled, out int sbCount);
                GetCosmeticConfig(isLeftHand, out bool cosEnabled, out string cosBarcode, out int cosCount);

                // Spawn explosion effect
                if (explosionBarcode != null)
                    SpawnMultiple(explosionBarcode, 1, spawnPos, spawnRot, spawnDelay);

                // Spawn SmashBone effect(s) — uses matrix in legacy too
                if (sbEnabled)
                {
                    if (punchMatrixMode == MatrixMode.DISABLED || punchSpawnCount <= 1)
                    {
                        SpawnMultiple(SmashBoneBarcode, sbCount, spawnPos, smashRot, spawnDelay);
                    }
                    else
                    {
                        Vector3 legForward = relativeVelocity.sqrMagnitude > 0.01f ? relativeVelocity.normalized : Vector3.forward;
                        Vector3 legRight = Vector3.Cross(Vector3.up, legForward).normalized;
                        if (legRight.sqrMagnitude < 0.01f) legRight = Vector3.right;
                        Vector3 legUp = Vector3.Cross(legForward, legRight).normalized;
                        var offsets = CalculateMatrixOffsets(punchSpawnCount, punchSpacing, legRight, legUp, punchMatrixMode);
                        foreach (var offset in offsets)
                            SpawnMultiple(SmashBoneBarcode, sbCount, spawnPos + offset, smashRot, spawnDelay);
                    }
                }

                // Spawn Cosmetic effect(s)
                if (cosEnabled && !string.IsNullOrEmpty(cosBarcode))
                    SpawnMultiple(cosBarcode, cosCount, spawnPos, cosRot, spawnDelay);

                lastPunchTime = Time.time;
            }
            catch (Exception ex)
            {
                Main.MelonLog.Warning($"Legacy punch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn an effect at a position with network support.
        /// </summary>
        public static void SpawnEffect(string barcode, Vector3 position, Quaternion rotation)
        {
            try
            {
                // Try network spawn first, then fall back to local
                if (TryNetworkedSpawn(barcode, position, rotation))
                    return;

                // Fallback: local spawn
                SpawnExplosion(barcode, position, rotation);
            }
            catch { }
        }

        private static List<Vector3> CalculateMatrixOffsets(int count, float spacing, Vector3 right, Vector3 up, MatrixMode mode)
        {
            var offsets = new List<Vector3>();
            if (count <= 1 || mode == MatrixMode.DISABLED) { offsets.Add(Vector3.zero); return offsets; }

            if (mode == MatrixMode.CIRCLE)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = (2f * Mathf.PI * i) / count;
                    offsets.Add(right * (Mathf.Cos(angle) * spacing) + up * (Mathf.Sin(angle) * spacing));
                }
                return offsets;
            }

            // SQUARE mode
            if (count == 2)
            {
                offsets.Add(-right * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f);
            }
            else if (count == 3)
            {
                offsets.Add(up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else if (count == 4)
            {
                offsets.Add(-right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f + up * spacing * 0.5f);
                offsets.Add(-right * spacing * 0.5f - up * spacing * 0.5f);
                offsets.Add(right * spacing * 0.5f - up * spacing * 0.5f);
            }
            else
            {
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
                float halfGrid = (gridSize - 1) * spacing * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    int row = i / gridSize;
                    int col = i % gridSize;
                    offsets.Add(right * (col * spacing - halfGrid) + up * (row * spacing - halfGrid));
                }
            }
            return offsets;
        }
    }

    /// <summary>
    /// Harmony patch: hooks into HandSFX.PunchAttack to trigger Legacy Punch effects.
    /// This fires when the game's built-in punch detection activates (particle effects on hit).
    /// </summary>
    [HarmonyPatch(typeof(HandSFX), "PunchAttack")]
    public static class LegacyPunchPatch
    {
        public static void Postfix(HandSFX __instance, CollisionCollector.RelevantCollision c, float impulse, float relVelSqr)
        {
            try
            {
                ExplosivePunchController.OnLegacyPunch(
                    __instance,
                    c.collider,
                    c.rigidbody,
                    c.relativeVelocity
                );
            }
            catch { }
        }
    }
}
