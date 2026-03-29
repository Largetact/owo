using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using BoneLib;
using BoneLib.Notifications;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LabFusion.Safety;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace BonelabUtilityMod
{
    public static class ScreenShareController
    {
        // ── Public Settings ──
        public static bool Enabled;
        public static bool PreviewVisible = true;
        public static bool StreamEnabled;
        public static int Scale = 50;
        public static int TargetFps = 15;
        public static int StreamPort = 9850;
        public static bool UsePublicIp; // false = LAN IP, true = public IP (requires port-forward)
        public static int SelectedSourceIndex = -1; // -1 = desktop

        // ── FFmpeg ──
        private static string _ffmpegPath;
        private static bool _ffmpegReady;
        private static bool _ffmpegDownloading;
        private const string FFMPEG_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        // ── Window Enumeration ──
        public static List<string> AvailableWindows = new List<string>();
        private static List<IntPtr> _windowHandles = new List<IntPtr>();

        public static string SourceDisplayName =>
            SelectedSourceIndex < 0
                ? "Desktop"
                : (SelectedSourceIndex < AvailableWindows.Count
                    ? AvailableWindows[SelectedSourceIndex]
                    : "Desktop");

        // ── Stream URL (read-only) ──
        public static string StreamUrl { get; private set; } = "";

        // ── Whitelist management ──
        private static readonly List<string> _addedDomains = new List<string>();

        // ── P/Invoke for window bounds ──
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        // ── Constants ──
        private const int RT_W = 1920;
        private const int RT_H = 1080;

        // ── Capture pipeline ──
        private static int _streamW = 960;
        private static int _streamH = 540;
        private static volatile bool _captureActive;
        private static Thread _captureThread;
        private static Process _ffmpegCapture;
        private static byte[] _bufA = Array.Empty<byte>();
        private static byte[] _bufB = Array.Empty<byte>();
        private static byte[] _frontBuf = Array.Empty<byte>();
        private static readonly object _swapLock = new object();
        private static volatile bool _newFrame;

        // ── Display ──
        private static RenderTexture _renderTex;
        private static Texture2D _uploadTex;
        private static GameObject _previewQuad;
        private static MeshRenderer _previewRenderer;
        private static Material _previewMat;
        private static Shader _cachedShader;
        private static Texture2D _greenTex;

        // ── HTTP stream ──
        private static Process _ffmpegStream;
        private static TcpListener _tcpListener;
        private static Thread _streamPipeThread;
        private static Thread _streamAcceptThread;
        private static volatile bool _streamActive;
        private static volatile NetworkStream _activeClientStream;
        private static readonly object _clientLock = new object();

        // ── IP caches ──
        private static string _lanIp;
        private static string _publicIp;
        private static bool _publicIpFetched;

        // ══════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════

        public static void Initialize()
        {
            _lanIp = DetectLanIp();
            FetchPublicIpAsync();
            LocateOrDownloadFfmpeg();
            ApplyHarmonyPatches();
        }

        /// <summary>
        /// Manually apply Harmony patches after LabFusion types are loaded.
        /// </summary>
        private static void ApplyHarmonyPatches()
        {
            try
            {
                var harmony = new HarmonyLib.Harmony("BonelabUtilityMod.ScreenShare");
                var target = AccessTools.Method(typeof(URLWhitelistManager), "IsURLWhitelisted");
                var postfix = AccessTools.Method(typeof(ScreenShareController), "WhitelistPostfix",
                    new[] { typeof(string), typeof(bool).MakeByRefType() });
                if (target != null && postfix != null)
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    Main.MelonLog?.Msg("[ScreenShare] Harmony: Patched URLWhitelistManager.IsURLWhitelisted");
                }
                else
                {
                    Main.MelonLog?.Warning("[ScreenShare] Harmony: Could not find target/postfix method");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] Harmony patch failed (Fusion may not be loaded): {ex.Message}");
            }
        }

        /// <summary>
        /// Harmony postfix — allows stream-like URLs through LabFusion's whitelist.
        /// Works for both local and remote players who have this mod.
        /// </summary>
        private static void WhitelistPostfix(string url, ref bool __result)
        {
            if (__result) return;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                && uri.AbsolutePath.StartsWith("/stream", StringComparison.OrdinalIgnoreCase)
                && uri.Port >= 1024)
            {
                __result = true;
            }
        }

        /// <summary>
        /// Adds IP/domain to LabFusion's URL whitelist so Media Player accepts the stream URL.
        /// </summary>
        private static void RegisterStreamDomains()
        {
            try
            {
                var list = URLWhitelistManager.List?.Whitelist;
                if (list == null) return;

                string[] domains = { "127.0.0.1", "localhost", _lanIp };
                if (_publicIpFetched && !string.IsNullOrEmpty(_publicIp))
                    domains = new[] { "127.0.0.1", "localhost", _lanIp, _publicIp };

                foreach (string d in domains)
                {
                    if (string.IsNullOrEmpty(d)) continue;
                    if (list.Any(x => x.Domain == d)) continue;
                    list.Add(new URLInfo { Domain = d });
                    _addedDomains.Add(d);
                    Main.MelonLog?.Msg($"[ScreenShare] Added '{d}' to URL whitelist");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] RegisterStreamDomains: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes our IPs from LabFusion's URL whitelist.
        /// </summary>
        private static void UnregisterStreamDomains()
        {
            try
            {
                var list = URLWhitelistManager.List?.Whitelist;
                if (list == null || _addedDomains.Count == 0) return;
                foreach (string d in _addedDomains)
                    list.RemoveAll(x => x.Domain == d);
                _addedDomains.Clear();
            }
            catch { }
        }

        /// <summary>
        /// Finds FFmpeg on PATH, in UserData/ScreenShare, or downloads it.
        /// </summary>
        private static void LocateOrDownloadFfmpeg()
        {
            // 1. Check UserData folder first (our cached copy)
            string userDataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "ScreenShare");
            string cachedExe = Path.Combine(userDataDir, "ffmpeg.exe");
            if (File.Exists(cachedExe))
            {
                _ffmpegPath = cachedExe;
                _ffmpegReady = true;
                Main.MelonLog?.Msg($"[ScreenShare] FFmpeg found: {_ffmpegPath}");
                return;
            }

            // 2. Check system PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "ffmpeg",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        _ffmpegPath = output.Split('\n')[0].Trim();
                        _ffmpegReady = true;
                        Main.MelonLog?.Msg($"[ScreenShare] FFmpeg on PATH: {_ffmpegPath}");
                        return;
                    }
                }
            }
            catch { /* where.exe failed, continue to download */ }

            // 3. Not found — download asynchronously
            Main.MelonLog?.Warning("[ScreenShare] FFmpeg not found — downloading...");
            NotificationHelper.Send(NotificationType.Information, "FFmpeg not found.\nDownloading (~90 MB)...");
            _ffmpegDownloading = true;

            new Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(userDataDir);
                    string zipPath = Path.Combine(userDataDir, "ffmpeg.zip");

                    using (var http = new HttpClient())
                    {
                        using (var stream = http.GetStreamAsync(FFMPEG_URL).GetAwaiter().GetResult())
                        using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                            stream.CopyTo(fs);
                    }

                    // Extract — the zip contains a folder like "ffmpeg-7.1-essentials_build/bin/ffmpeg.exe"
                    string extractDir = Path.Combine(userDataDir, "_extract");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    // Find ffmpeg.exe inside extracted tree
                    string found = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null)
                    {
                        File.Copy(found, cachedExe, true);
                        _ffmpegPath = cachedExe;
                        _ffmpegReady = true;
                        Main.MelonLog?.Msg($"[ScreenShare] FFmpeg downloaded to: {_ffmpegPath}");
                    }
                    else
                    {
                        Main.MelonLog?.Error("[ScreenShare] ffmpeg.exe not found inside downloaded zip!");
                    }

                    // Cleanup
                    try { File.Delete(zipPath); } catch { }
                    try { Directory.Delete(extractDir, true); } catch { }
                }
                catch (Exception ex)
                {
                    Main.MelonLog?.Error($"[ScreenShare] FFmpeg download failed: {ex.Message}");
                }
                finally
                {
                    _ffmpegDownloading = false;
                }
            })
            { IsBackground = true, Name = "FFmpeg_Download" }.Start();
        }

        /// <summary>
        /// Called every frame from Main.OnUpdate(). Uploads captured frames to GPU.
        /// </summary>
        public static void Update()
        {
            if (!Enabled) return;

            // Detect if capture FFmpeg died unexpectedly
            if (_captureActive && _ffmpegCapture != null && _ffmpegCapture.HasExited)
            {
                int code = _ffmpegCapture.ExitCode;
                _captureActive = false;
                Main.MelonLog?.Error($"[ScreenShare] FFmpeg capture exited (code {code}) — source: {SourceDisplayName}");
                NotificationHelper.Send(NotificationType.Error,
                    $"Screen capture failed (code {code})\nSource: {SourceDisplayName}");
            }

            if (!_newFrame) return;

            byte[] buf;
            lock (_swapLock)
            {
                if (!_newFrame) return;
                buf = _frontBuf;
                _newFrame = false;
            }
            UploadFrame(buf);
        }

        /// <summary>
        /// Called when a new level loads. Rebuilds display objects.
        /// </summary>
        public static void OnLevelLoaded()
        {
            CacheShader();
            if (Enabled)
            {
                DestroyQuad();
                CreateQuad();
                if (PreviewVisible)
                {
                    PositionQuad();
                    AssignShader();
                }
            }
        }

        /// <summary>
        /// Clean shutdown — stop everything.
        /// </summary>
        public static void Shutdown()
        {
            SetEnabled(false);
        }

        // ══════════════════════════════════════════
        //  Enable / Disable
        // ══════════════════════════════════════════

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on)
            {
                StopCapture();
                StopStream();
                DestroyQuad();
                DestroyTextures();
                return;
            }

            DestroyTextures();
            CreateRenderTexture();
            CreateQuad();
            StartCapture();

            if (PreviewVisible)
            {
                PositionQuad();
                AssignShader();
                if (_previewRenderer != null)
                    ((Renderer)_previewRenderer).enabled = true;
            }
        }

        public static void SetPreviewVisible(bool on)
        {
            PreviewVisible = on;
            if (_previewRenderer == null) return;
            if (on)
            {
                PositionQuad();
                AssignShader();
            }
            ((Renderer)_previewRenderer).enabled = on;
        }

        public static void SetScale(int percent)
        {
            Scale = percent;
            if (Enabled && _captureActive)
            {
                StopCapture();
                StartCapture();
            }
        }

        public static void SetFps(int fps)
        {
            TargetFps = fps;
            if (Enabled && _captureActive)
            {
                StopCapture();
                StartCapture();
            }
        }

        /// <summary>
        /// Reposition the display quad in front of the player's camera.
        /// </summary>
        public static void Reposition()
        {
            if (!Enabled || _previewQuad == null) return;
            PositionQuad();
            AssignShader();
            NotificationHelper.Send(NotificationType.Success, "Quad repositioned");
        }

        // ══════════════════════════════════════════
        //  Window Enumeration
        // ══════════════════════════════════════════

        public static void RefreshWindows()
        {
            AvailableWindows.Clear();
            _windowHandles.Clear();
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero
                            && !string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                        {
                            string title = proc.MainWindowTitle;
                            if (!AvailableWindows.Contains(title))
                            {
                                AvailableWindows.Add(title);
                                _windowHandles.Add(proc.MainWindowHandle);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }
                // Keep parallel lists in sync — sort titles and handles together
                var pairs = AvailableWindows.Zip(_windowHandles, (t, h) => (t, h)).OrderBy(p => p.t).ToList();
                AvailableWindows = pairs.Select(p => p.t).ToList();
                _windowHandles = pairs.Select(p => p.h).ToList();
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] RefreshWindows: {ex.Message}");
            }
        }

        public static void SelectSource(int index)
        {
            SelectedSourceIndex = index;
            if (Enabled)
            {
                try
                {
                    StopCapture();
                    if (StreamEnabled) StopStream();
                }
                catch (Exception ex)
                {
                    Main.MelonLog?.Warning($"[ScreenShare] Error stopping old source: {ex.Message}");
                }

                // Small delay so threads fully exit before we start new pipeline
                Thread.Sleep(100);

                StartCapture();
                if (StreamEnabled) StartStream();
            }
            NotificationHelper.Send(NotificationType.Success, $"Source: {SourceDisplayName}");
        }

        // ══════════════════════════════════════════
        //  Capture Pipeline (FFmpeg gdigrab → pipe)
        // ══════════════════════════════════════════

        private static void StartCapture()
        {
            if (!_ffmpegReady)
            {
                string msg = _ffmpegDownloading ? "FFmpeg is still downloading..." : "FFmpeg not available";
                Main.MelonLog?.Warning($"[ScreenShare] {msg}");
                NotificationHelper.Send(NotificationType.Warning, msg);
                return;
            }
            StopCapture();

            _streamW = (int)(RT_W * Scale / 100f) & ~1; // even only
            _streamH = (int)(RT_H * Scale / 100f) & ~1;
            RecreateUploadTex();

            int frameSize = _streamW * _streamH * 3;
            _bufA = new byte[frameSize];
            _bufB = new byte[frameSize];
            _frontBuf = _bufA;

            string inputArg = BuildInputArg(out string extraArgs);

            string args = $"-f gdigrab -framerate {TargetFps} {extraArgs}-i {inputArg} "
                        + $"-vf scale={_streamW}:{_streamH}:flags=fast_bilinear,vflip "
                        + "-f rawvideo -pix_fmt rgb24 -an -threads 1 pipe:1";

            Main.MelonLog?.Msg($"[ScreenShare] FFmpeg capture args: {args}");

            try
            {
                _ffmpegCapture = Process.Start(new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (_ffmpegCapture != null)
                {
                    _captureActive = true;
                    var proc = _ffmpegCapture;
                    _captureThread = new Thread(() => CaptureLoop(proc, frameSize))
                    {
                        IsBackground = true,
                        Name = "ScreenShareCapture",
                        Priority = System.Threading.ThreadPriority.BelowNormal
                    };
                    _captureThread.Start();
                    Main.MelonLog?.Msg($"[ScreenShare] Capture: {SourceDisplayName} @ {_streamW}x{_streamH} ({Scale}%) {TargetFps}fps");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Error($"[ScreenShare] StartCapture: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Capture failed: {ex.Message}");
            }
        }

        private static void StopCapture()
        {
            _captureActive = false;

            // Clear pending frame so Update() doesn't upload stale data
            lock (_swapLock) { _newFrame = false; }

            // Give the capture thread a moment to notice the flag before we kill the process
            Thread.Sleep(50);

            try { _ffmpegCapture?.Kill(); } catch { }
            try { _ffmpegCapture?.WaitForExit(1000); } catch { }
            _ffmpegCapture = null;

            _captureThread?.Join(3000);
            _captureThread = null;
        }

        private static void CaptureLoop(Process proc, int frameSize)
        {
            try
            {
                // Log FFmpeg stderr so we can see errors (e.g. window not found)
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Main.MelonLog?.Msg($"[ScreenShare-Capture] {e.Data}");
                };
                proc.BeginErrorReadLine();

                Stream stream = proc.StandardOutput.BaseStream;
                byte[] writeBuf = _bufB;

                while (_captureActive)
                {
                    int read = 0;
                    while (read < frameSize && _captureActive)
                    {
                        int n;
                        try
                        {
                            n = stream.Read(writeBuf, read, frameSize - read);
                        }
                        catch
                        {
                            // Stream closed (process killed) — exit cleanly
                            return;
                        }
                        if (n <= 0)
                        {
                            if (!_captureActive) return;
                            // n == 0 means EOF — FFmpeg exited
                            if (n == 0) return;
                        }
                        read += n;
                    }

                    if (!_captureActive) return;

                    lock (_swapLock)
                    {
                        _frontBuf = writeBuf;
                        _newFrame = true;
                    }
                    writeBuf = (writeBuf == _bufA) ? _bufB : _bufA;
                }
            }
            catch
            {
                // Expected when process is killed during source change
            }
        }

        /// <summary>
        /// Builds the FFmpeg -i argument and optional offset/size args for the selected source.
        /// For window capture, uses region-based desktop capture (more reliable than title= on modern Windows).
        /// Returns the full set of extra args to insert before -i.
        /// </summary>
        private static string BuildInputArg()
        {
            return BuildInputArg(out _);
        }

        private static string BuildInputArg(out string extraArgs)
        {
            extraArgs = "";
            if (SelectedSourceIndex < 0 || SelectedSourceIndex >= AvailableWindows.Count
                || SelectedSourceIndex >= _windowHandles.Count)
                return "desktop";

            IntPtr hwnd = _windowHandles[SelectedSourceIndex];

            // Restore window if minimized
            try { if (IsIconic(hwnd)) ShowWindow(hwnd, 9 /* SW_RESTORE */); } catch { }

            if (GetWindowRect(hwnd, out RECT rect))
            {
                int w = (rect.Right - rect.Left) & ~1; // even
                int h = (rect.Bottom - rect.Top) & ~1;
                if (w > 0 && h > 0)
                {
                    extraArgs = $"-offset_x {rect.Left} -offset_y {rect.Top} -video_size {w}x{h} ";
                    Main.MelonLog?.Msg($"[ScreenShare] Window region: {rect.Left},{rect.Top} {w}x{h}");
                    return "desktop";
                }
            }

            // Fallback: try title-based capture
            Main.MelonLog?.Warning("[ScreenShare] GetWindowRect failed, falling back to title match");
            string title = AvailableWindows[SelectedSourceIndex];
            title = title.Replace("\"", "\\\"");
            return $"title=\"{title}\"";
        }

        // ══════════════════════════════════════════
        //  Frame Upload
        // ══════════════════════════════════════════

        private static void UploadFrame(byte[] rgb)
        {
            if (_uploadTex == null || _renderTex == null) return;
            Il2CppStructArray<byte> il2cppArr = rgb;
            _uploadTex.LoadRawTextureData(il2cppArr);
            _uploadTex.Apply(false, false);
            Graphics.Blit((Texture)(object)_uploadTex, _renderTex);
        }

        // ══════════════════════════════════════════
        //  Texture Management
        // ══════════════════════════════════════════

        private static void CreateRenderTexture()
        {
            _renderTex = new RenderTexture(RT_W, RT_H, 0, RenderTextureFormat.ARGB32)
            {
                name = "ScreenShareRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _renderTex.Create();
            RecreateUploadTex();
        }

        private static void RecreateUploadTex()
        {
            if (_uploadTex != null)
                UnityEngine.Object.Destroy((UnityEngine.Object)(object)_uploadTex);
            _uploadTex = new Texture2D(_streamW, _streamH, TextureFormat.RGB24, false, false);
        }

        private static void DestroyTextures()
        {
            if (_greenTex != null) { UnityEngine.Object.Destroy((UnityEngine.Object)(object)_greenTex); _greenTex = null; }
            if (_uploadTex != null) { UnityEngine.Object.Destroy((UnityEngine.Object)(object)_uploadTex); _uploadTex = null; }
            if (_renderTex != null) { _renderTex.Release(); UnityEngine.Object.Destroy((UnityEngine.Object)(object)_renderTex); _renderTex = null; }
            if (_previewMat != null) { UnityEngine.Object.Destroy((UnityEngine.Object)(object)_previewMat); _previewMat = null; }
        }

        // ══════════════════════════════════════════
        //  Quad Display
        // ══════════════════════════════════════════

        private static void CreateQuad()
        {
            DestroyQuad();
            _previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ((UnityEngine.Object)_previewQuad).name = "ScreenShare_Preview";

            var collider = _previewQuad.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy((UnityEngine.Object)(object)collider);

            _previewRenderer = _previewQuad.GetComponent<MeshRenderer>();
            _previewQuad.transform.localScale = new Vector3(1.78f, 1f, 1f); // 16:9 aspect

            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            _previewMat = new Material(shader)
            {
                mainTexture = (Texture)(object)_renderTex
            };
            ((Renderer)_previewRenderer).sharedMaterial = _previewMat;
            ((Renderer)_previewRenderer).enabled = PreviewVisible;
        }

        private static void DestroyQuad()
        {
            if (_previewQuad != null)
            {
                UnityEngine.Object.Destroy((UnityEngine.Object)(object)_previewQuad);
                _previewQuad = null;
            }
            _previewRenderer = null;
        }

        private static void PositionQuad()
        {
            if (_previewQuad == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform t = ((Component)cam).transform;
            Vector3 pos = t.position + t.forward * 2f;
            _previewQuad.transform.position = pos;
            _previewQuad.transform.rotation = Quaternion.LookRotation(t.forward, t.up);
        }

        // ══════════════════════════════════════════
        //  Shader (SLZ/LitMAS with emission for VR)
        // ══════════════════════════════════════════

        private static void CacheShader()
        {
            try
            {
                foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    var mat = ((Renderer)mr).sharedMaterial;
                    if (mat == null) continue;
                    var s = mat.shader;
                    if (s != null && ((UnityEngine.Object)s).name == "SLZ/LitMAS/LitMAS Standard")
                    {
                        _cachedShader = s;
                        return;
                    }
                }
            }
            catch { }
        }

        private static void AssignShader()
        {
            if (_previewRenderer == null || _renderTex == null) return;

            Shader shader = _cachedShader ?? Shader.Find("SLZ/LitMAS/LitMAS Standard");

            // Fallback: find from scene
            if (shader == null)
            {
                try
                {
                    foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true))
                    {
                        if (mr == null || mr == _previewRenderer) continue;
                        var mat = ((Renderer)mr).sharedMaterial;
                        if (mat?.shader != null && ((UnityEngine.Object)mat.shader).name == "SLZ/LitMAS/LitMAS Standard")
                        {
                            shader = mat.shader;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (shader == null) return;

            if (_previewMat != null)
                UnityEngine.Object.Destroy((UnityEngine.Object)(object)_previewMat);

            _previewMat = new Material(shader);
            _previewMat.SetTexture("_BaseMap", (Texture)(object)_renderTex);
            _previewMat.SetTexture("_MainTex", (Texture)(object)_renderTex);
            _previewMat.mainTexture = (Texture)(object)_renderTex;

            // Green metallic map so the emission shows through properly
            if (_greenTex == null)
            {
                _greenTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _greenTex.SetPixel(0, 0, new Color(0f, 1f, 0f, 1f));
                _greenTex.Apply();
            }
            _previewMat.SetTexture("_MetallicGlossMap", (Texture)(object)_greenTex);
            _previewMat.SetTexture("_EmissionMap", (Texture)(object)_renderTex);
            _previewMat.SetFloat("_Emission", 1f);
            _previewMat.EnableKeyword("_EMISSION");

            if (_previewMat.HasProperty("_BaseColor")) _previewMat.SetColor("_BaseColor", Color.white);
            if (_previewMat.HasProperty("_Color")) _previewMat.SetColor("_Color", Color.white);
            if (_previewMat.HasProperty("_Surface")) _previewMat.SetFloat("_Surface", 0f);

            _previewMat.DisableKeyword("_NORMALMAP");
            _previewMat.DisableKeyword("_METALLICSPECGLOSSMAP");
            _previewMat.DisableKeyword("_OCCLUSIONMAP");
            _previewMat.SetInt("_Cull", 0); // double-sided

            ((Renderer)_previewRenderer).sharedMaterial = _previewMat;
        }

        // ══════════════════════════════════════════
        //  Network Stream (FFmpeg → C# TCP HTTP server)
        // ══════════════════════════════════════════

        public static void SetStreamEnabled(bool on)
        {
            StreamEnabled = on;
            if (on && Enabled)
                StartStream();
            else
                StopStream();
        }

        private static void StartStream()
        {
            if (!_ffmpegReady)
            {
                string msg = _ffmpegDownloading ? "FFmpeg is still downloading..." : "FFmpeg not available";
                Main.MelonLog?.Warning($"[ScreenShare] {msg}");
                NotificationHelper.Send(NotificationType.Warning, msg);
                return;
            }
            StopStream();

            int w = _streamW > 0 ? _streamW : 960;
            int h = _streamH > 0 ? _streamH : 540;
            string inputArg = BuildInputArg(out string extraArgs);

            // FFmpeg encodes to fragmented MP4 on stdout for Unity VideoPlayer compatibility.
            // -movflags: frag_keyframe puts a keyframe at each fragment start,
            //           empty_moov puts moov at start so player can start immediately,
            //           default_base_moof enables proper fMP4 segment addressing.
            string args = $"-f gdigrab -framerate {TargetFps} {extraArgs}-i {inputArg} "
                        + $"-vf scale={w}:{h} "
                        + $"-c:v libx264 -preset ultrafast -tune zerolatency -g {TargetFps} -b:v 1200k "
                        + "-movflags frag_keyframe+empty_moov+default_base_moof -f mp4 pipe:1";

            try
            {
                // Start TCP listener first so the port is available before FFmpeg starts
                _tcpListener = new TcpListener(IPAddress.Any, StreamPort);
                _tcpListener.Start();

                _ffmpegStream = Process.Start(new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (_ffmpegStream == null)
                {
                    _tcpListener.Stop();
                    NotificationHelper.Send(NotificationType.Error, "FFmpeg failed to start");
                    return;
                }

                _streamActive = true;

                // Log FFmpeg stderr
                _ffmpegStream.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Main.MelonLog?.Msg($"[ScreenShare-Stream] {e.Data}");
                };
                _ffmpegStream.BeginErrorReadLine();

                // Thread 1: Read FFmpeg stdout → forward to active HTTP client
                _streamPipeThread = new Thread(StreamPipeLoop)
                {
                    IsBackground = true,
                    Name = "ScreenShare_StreamPipe"
                };
                _streamPipeThread.Start();

                // Thread 2: Accept incoming HTTP connections
                _streamAcceptThread = new Thread(StreamAcceptLoop)
                {
                    IsBackground = true,
                    Name = "ScreenShare_StreamAccept"
                };
                _streamAcceptThread.Start();

                // Add our IPs to LabFusion's URL whitelist so Media Player accepts the URL
                RegisterStreamDomains();

                string ip = UsePublicIp ? GetPublicIp() : GetLanIp();
                StreamUrl = $"http://{ip}:{StreamPort}/stream.mp4";

                Main.MelonLog?.Msg($"[ScreenShare] Stream server started: {StreamUrl}");

                CopyUrlToClipboard();

                string ipMode = UsePublicIp ? "PUBLIC IP (port-forward required)" : "LAN";
                NotificationHelper.Send(NotificationType.Success,
                    $"Stream ready ({ipMode}):\n{StreamUrl}\nCopied to clipboard!\nPaste into Media Player SDK (Press B)");

                BroadcastStreamUrl(StreamUrl);
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Error($"[ScreenShare] StartStream: {ex.Message}");
                NotificationHelper.Send(NotificationType.Error, $"Stream failed: {ex.Message}");
                StopStream();
            }
        }

        private static void StopStream()
        {
            _streamActive = false;

            // Give threads a moment to notice the flag
            Thread.Sleep(50);

            lock (_clientLock)
            {
                try { _activeClientStream?.Close(); } catch { }
                _activeClientStream = null;
            }

            try { _tcpListener?.Stop(); } catch { }
            _tcpListener = null;

            try { _ffmpegStream?.Kill(); } catch { }
            try { _ffmpegStream?.WaitForExit(1000); } catch { }
            _ffmpegStream = null;

            _streamPipeThread?.Join(3000);
            _streamPipeThread = null;
            _streamAcceptThread?.Join(3000);
            _streamAcceptThread = null;

            if (!string.IsNullOrEmpty(StreamUrl))
            {
                UnregisterStreamDomains();
                StreamUrl = "";
                BroadcastStreamUrl("");
            }
        }

        /// <summary>
        /// Reads MPEG-TS from FFmpeg stdout and forwards to whatever HTTP client is connected.
        /// If no client, data is discarded (FFmpeg keeps running).
        /// </summary>
        private static void StreamPipeLoop()
        {
            try
            {
                var stdout = _ffmpegStream.StandardOutput.BaseStream;
                byte[] buf = new byte[16384];

                while (_streamActive)
                {
                    int read = stdout.Read(buf, 0, buf.Length);
                    if (read <= 0) break;

                    NetworkStream ns;
                    lock (_clientLock) { ns = _activeClientStream; }

                    if (ns != null)
                    {
                        try
                        {
                            ns.Write(buf, 0, read);
                            ns.Flush();
                        }
                        catch
                        {
                            // Client disconnected
                            lock (_clientLock) { _activeClientStream = null; }
                            Main.MelonLog?.Msg("[ScreenShare] Stream client disconnected");
                        }
                    }
                    // If no client connected, data is discarded — that's fine
                }
            }
            catch
            {
                if (_streamActive)
                    Main.MelonLog?.Warning("[ScreenShare] Stream pipe loop ended unexpectedly");
            }
        }

        /// <summary>
        /// Accepts TCP connections on the stream port.
        /// Sends HTTP 200 response headers, then the pipe thread forwards MPEG-TS data.
        /// Handles reconnections (Media Player SDK may probe then re-connect).
        /// </summary>
        private static void StreamAcceptLoop()
        {
            while (_streamActive)
            {
                TcpClient client = null;
                try
                {
                    client = _tcpListener.AcceptTcpClient();
                }
                catch { break; }

                try
                {
                    var ns = client.GetStream();

                    // Read the incoming HTTP request (we just need to consume it)
                    byte[] reqBuf = new byte[4096];
                    ns.ReadTimeout = 3000;
                    try { ns.Read(reqBuf, 0, reqBuf.Length); } catch { }

                    // Send proper HTTP response headers (fMP4 for VideoPlayer compatibility)
                    string headers = "HTTP/1.1 200 OK\r\n"
                                   + "Content-Type: video/mp4\r\n"
                                   + "Cache-Control: no-cache, no-store\r\n"
                                   + "Connection: close\r\n"
                                   + "Access-Control-Allow-Origin: *\r\n"
                                   + "\r\n";
                    byte[] hdr = System.Text.Encoding.ASCII.GetBytes(headers);
                    ns.Write(hdr, 0, hdr.Length);
                    ns.Flush();

                    // Replace the active client (closes old one if any)
                    lock (_clientLock)
                    {
                        var old = _activeClientStream;
                        _activeClientStream = ns;
                        if (old != null)
                        {
                            try { old.Close(); } catch { }
                        }
                    }

                    Main.MelonLog?.Msg("[ScreenShare] Stream client connected");
                }
                catch (Exception ex)
                {
                    Main.MelonLog?.Warning($"[ScreenShare] Client accept failed: {ex.Message}");
                    try { client?.Close(); } catch { }
                }
            }
        }

        // ══════════════════════════════════════════
        //  Fusion Metadata Broadcast
        // ══════════════════════════════════════════

        /// <summary>
        /// Broadcasts the stream URL to all players via Fusion player metadata.
        /// Uses reflection so the feature degrades gracefully if Fusion isn't loaded.
        /// </summary>
        private static void BroadcastStreamUrl(string url)
        {
            try
            {
                // Find LabFusion types via reflection (safe if Fusion not present)
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type senderType = null, idMgrType = null;
                foreach (var asm in assemblies)
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "PlayerSender") senderType = t;
                            else if (t.Name == "PlayerIDManager") idMgrType = t;
                            if (senderType != null && idMgrType != null) break;
                        }
                    }
                    catch { }
                    if (senderType != null && idMgrType != null) break;
                }

                if (senderType == null || idMgrType == null) return;

                var localSmallIdProp = idMgrType.GetProperty("LocalSmallID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (localSmallIdProp == null) return;

                object smallIdObj = localSmallIdProp.GetValue(null);
                if (smallIdObj == null) return;

                var sendMethod = senderType.GetMethod("SendPlayerMetadataRequest",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (sendMethod == null) return;

                sendMethod.Invoke(null, new object[] { smallIdObj, "ScreenShareURL", url ?? "" });
                Main.MelonLog?.Msg($"[ScreenShare] Fusion metadata broadcast: ScreenShareURL={url}");
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] Fusion broadcast failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════
        //  Clipboard
        // ══════════════════════════════════════════

        /// <summary>
        /// Copy current stream URL to system clipboard.
        /// </summary>
        public static void CopyUrlToClipboard()
        {
            if (string.IsNullOrEmpty(StreamUrl))
            {
                NotificationHelper.Send(NotificationType.Warning, "Stream not active — nothing to copy");
                return;
            }
            try
            {
                GUIUtility.systemCopyBuffer = StreamUrl;
                NotificationHelper.Send(NotificationType.Success,
                    $"Copied to clipboard:\n{StreamUrl}\nPaste into Media Player SDK (B)");
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] Clipboard copy failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════
        //  IP Detection
        // ══════════════════════════════════════════

        public static string GetLanIp()
        {
            if (!string.IsNullOrEmpty(_lanIp)) return _lanIp;
            _lanIp = DetectLanIp();
            return _lanIp;
        }

        public static string GetPublicIp()
        {
            if (_publicIpFetched && !string.IsNullOrEmpty(_publicIp)) return _publicIp;
            return GetLanIp(); // fallback if not fetched yet
        }

        private static string DetectLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 80);
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private static async void FetchPublicIpAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                string ip = (await client.GetStringAsync("https://api.ipify.org")).Trim();
                if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out _))
                {
                    _publicIp = ip;
                    _publicIpFetched = true;
                    Main.MelonLog?.Msg($"[ScreenShare] Public IP: {ip}");
                }
            }
            catch (Exception ex)
            {
                Main.MelonLog?.Warning($"[ScreenShare] Public IP fetch failed: {ex.Message}");
            }
        }
    }

}
