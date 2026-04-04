using MelonLoader;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Security.Cryptography;

namespace BonelabUtilityMod
{
    /// <summary>
    /// Auto-updater settings and DLL management controller.
    /// Manages mod DLL files and provides update checking capability.
    /// </summary>
    public static class AutoUpdaterController
    {
        // ───── Settings ─────
        private static bool _autoCheckEnabled = false;
        private static bool _autoInstallEnabled = false;
        private static string _updateUrl = "";
        private static float _checkIntervalHours = 24f;
        private static bool _backupOldDlls = true;
        private static bool _notifyOnUpdate = true;

        // ───── Internal State ─────
        private static string _modsFolder = "";
        private static string _pluginsFolder = "";
        private static string _backupFolder = "";
        private static float _lastCheckTime = 0f;
        private static string _statusMessage = "Not checked";
        private static bool _isChecking = false;
        private static List<DllInfo> _installedDlls = new List<DllInfo>();

        // ───── Properties ─────
        public static bool AutoCheckEnabled { get => _autoCheckEnabled; set => _autoCheckEnabled = value; }
        public static bool AutoInstallEnabled { get => _autoInstallEnabled; set => _autoInstallEnabled = value; }
        public static string UpdateUrl { get => _updateUrl; set => _updateUrl = value ?? ""; }
        public static float CheckIntervalHours { get => _checkIntervalHours; set => _checkIntervalHours = Mathf.Clamp(value, 0.5f, 168f); }
        public static bool BackupOldDlls { get => _backupOldDlls; set => _backupOldDlls = value; }
        public static bool NotifyOnUpdate { get => _notifyOnUpdate; set => _notifyOnUpdate = value; }
        public static string StatusMessage => _statusMessage;
        public static bool IsChecking => _isChecking;
        public static int InstalledDllCount => _installedDlls.Count;

        public struct DllInfo
        {
            public string FileName;
            public string FullPath;
            public string SizeText;
            public string LastModified;
        }

        public static void Initialize()
        {
            try
            {
                string gameDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                _modsFolder = Path.Combine(gameDir, "Mods");
                _pluginsFolder = Path.Combine(gameDir, "Plugins");
                _backupFolder = Path.Combine(gameDir, "Mods", "Backups");
            }
            catch
            {
                _modsFolder = "";
                _pluginsFolder = "";
                _backupFolder = "";
            }
        }

        /// <summary>Scan installed DLLs in Mods and Plugins folders.</summary>
        public static void RefreshDllList()
        {
            _installedDlls.Clear();
            try
            {
                if (!string.IsNullOrEmpty(_modsFolder) && Directory.Exists(_modsFolder))
                {
                    foreach (var file in Directory.GetFiles(_modsFolder, "*.dll"))
                    {
                        var info = new FileInfo(file);
                        _installedDlls.Add(new DllInfo
                        {
                            FileName = info.Name,
                            FullPath = file,
                            SizeText = FormatSize(info.Length),
                            LastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                        });
                    }
                }
                if (!string.IsNullOrEmpty(_pluginsFolder) && Directory.Exists(_pluginsFolder))
                {
                    foreach (var file in Directory.GetFiles(_pluginsFolder, "*.dll"))
                    {
                        var info = new FileInfo(file);
                        _installedDlls.Add(new DllInfo
                        {
                            FileName = "[Plugin] " + info.Name,
                            FullPath = file,
                            SizeText = FormatSize(info.Length),
                            LastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                        });
                    }
                }
                _statusMessage = $"Found {_installedDlls.Count} DLLs";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Scan error: {ex.Message}";
            }
        }

        public static List<DllInfo> GetInstalledDlls() => _installedDlls;

        /// <summary>Backup a specific DLL before updating.</summary>
        public static bool BackupDll(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath)) return false;
                if (!Directory.Exists(_backupFolder))
                    Directory.CreateDirectory(_backupFolder);

                string fileName = Path.GetFileName(fullPath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(_backupFolder, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}.dll.bak");
                File.Copy(fullPath, backupPath, true);
                _statusMessage = $"Backed up: {fileName}";
                return true;
            }
            catch (Exception ex)
            {
                _statusMessage = $"Backup failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>Delete a mod DLL (with optional backup).</summary>
        public static bool DeleteDll(int index)
        {
            if (index < 0 || index >= _installedDlls.Count) return false;
            var dll = _installedDlls[index];
            try
            {
                if (_backupOldDlls)
                    BackupDll(dll.FullPath);

                File.Delete(dll.FullPath);
                _installedDlls.RemoveAt(index);
                _statusMessage = $"Deleted: {dll.FileName}";
                return true;
            }
            catch (Exception ex)
            {
                _statusMessage = $"Delete failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>Open the Mods folder in the file explorer.</summary>
        public static void OpenModsFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(_modsFolder) && Directory.Exists(_modsFolder))
                    System.Diagnostics.Process.Start("explorer.exe", _modsFolder);
            }
            catch { }
        }

        /// <summary>Open the Backups folder in the file explorer.</summary>
        public static void OpenBackupsFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(_backupFolder))
                {
                    if (!Directory.Exists(_backupFolder))
                        Directory.CreateDirectory(_backupFolder);
                    System.Diagnostics.Process.Start("explorer.exe", _backupFolder);
                }
            }
            catch { }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
        }
    }
}
