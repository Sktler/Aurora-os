using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Windows webcam service backed directly by OpenCvSharp.
    /// The camera remains open and continuously captures frames until explicitly stopped.
    /// No WPF/OpenCvSharp.WpfExtensions types are used here; consumers receive encoded JPEG frames.
    /// </summary>
    public sealed class CameraService : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _captureSync = new();
        private VideoCapture? _capture;
        private CancellationTokenSource? _previewCts;
        private Task? _previewTask;
        private Mat? _latestFrame;
        private VideoWriter? _videoWriter;
        private int _selectedIndex;

        public bool IsInitialized => _capture?.IsOpened() == true;
        public bool IsActive => IsInitialized && _previewTask is { IsCompleted: false };
        public bool IsRecording => _videoWriter?.IsOpened() == true;
        public IReadOnlyList<CameraDeviceInfo> Devices { get; private set; } = Array.Empty<CameraDeviceInfo>();
        public string? SelectedDeviceId => _selectedIndex.ToString();

        /// <summary>Latest JPEG-encoded frame. This is UI-framework neutral.</summary>
        public byte[]? LatestFrameJpeg { get; private set; }

        public event EventHandler<CameraFrameEventArgs>? FrameReady;

        public Task<IReadOnlyList<CameraDeviceInfo>> RefreshDevicesAsync()
        {
            var devices = new List<CameraDeviceInfo>();
            for (var index = 0; index < 10; index++)
            {
                try
                {
                    using var probe = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                    if (!probe.IsOpened())
                        continue;

                    devices.Add(new CameraDeviceInfo(index.ToString(), $"Camera {index + 1}"));
                }
                catch
                {
                    // Some camera drivers throw while probing. Continue scanning.
                }
            }

            Devices = devices;
            if (!Devices.Any(d => d.Id == _selectedIndex.ToString()))
                _selectedIndex = devices.Count == 0 ? 0 : int.Parse(devices[0].Id);

            return Task.FromResult<IReadOnlyList<CameraDeviceInfo>>(Devices);
        }

        public async Task<CameraPermissionResult> CheckPermissionAsync()
        {
            try
            {
                if (Devices.Count == 0)
                    await RefreshDevicesAsync();
                if (Devices.Count == 0)
                    return CameraPermissionResult.NoCamera;

                using var probe = new VideoCapture(_selectedIndex, VideoCaptureAPIs.DSHOW);
                return probe.IsOpened() ? CameraPermissionResult.Allowed : CameraPermissionResult.Denied;
            }
            catch (UnauthorizedAccessException)
            {
                return CameraPermissionResult.Denied;
            }
            catch
            {
                return CameraPermissionResult.Error;
            }
        }

        public async Task InitializeAsync(string? deviceId = null)
        {
            await _gate.WaitAsync();
            try
            {
                if (Devices.Count == 0)
                    await RefreshDevicesAsync();

                if (!string.IsNullOrWhiteSpace(deviceId) && int.TryParse(deviceId, out var requested))
                    _selectedIndex = requested;

                if (Devices.Count == 0)
                    throw new InvalidOperationException("No camera devices were found.");
                if (!Devices.Any(d => d.Id == _selectedIndex.ToString()))
                    throw new InvalidOperationException($"Camera {_selectedIndex + 1} is not available.");

                await StopAsync();

                var capture = new VideoCapture(_selectedIndex, VideoCaptureAPIs.DSHOW);
                if (!capture.IsOpened())
                {
                    capture.Dispose();
                    throw new UnauthorizedAccessException("Windows or the camera driver would not allow Aurora to open the webcam. Check Windows Settings > Privacy & security > Camera and enable camera access for desktop apps.");
                }

                capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                capture.Set(VideoCaptureProperties.FrameHeight, 720);
                capture.Set(VideoCaptureProperties.Fps, 30);
                _capture = capture;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Starts the persistent webcam capture loop. Calling it again is harmless.</summary>
        public async Task StartPreviewAsync()
        {
            if (!IsInitialized)
                await InitializeAsync();
            if (IsActive)
                return;

            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;
            _previewTask = Task.Run(() => PreviewLoop(token), token);
        }

        public async Task StopPreviewAsync()
        {
            _previewCts?.Cancel();
            if (_previewTask != null)
            {
                try { await _previewTask; }
                catch (OperationCanceledException) { }
            }

            _previewTask = null;
            _previewCts?.Dispose();
            _previewCts = null;
        }

        public string GetStatus()
        {
            if (Devices.Count == 0)
                return "No camera devices found.";

            var name = Devices.FirstOrDefault(d => d.Id == _selectedIndex.ToString())?.Name ?? "selected camera";
            if (!IsInitialized)
                return $"Camera ready: {name}.";
            if (IsRecording)
                return $"Camera live: {name}. Continuous stream is running and video recording is active.";
            return IsActive
                ? $"Camera live: {name}. Continuous stream is running."
                : $"Camera initialized: {name}.";
        }

        public void OpenWindowsCameraApp()
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "microsoft.windows.camera:",
                UseShellExecute = true
            });
        }

        public async Task<StorageFileResult> CapturePhotoAsync(string? destinationFolder = null)
        {
            if (!IsInitialized)
                await InitializeAsync();
            if (!IsActive)
                await StartPreviewAsync();

            Mat frame;
            lock (_captureSync)
            {
                frame = _latestFrame?.Clone() ?? new Mat();
            }

            if (frame.Empty())
            {
                frame.Dispose();
                throw new InvalidOperationException("The webcam did not return a frame.");
            }

            using (frame)
            {
                var folder = string.IsNullOrWhiteSpace(destinationFolder)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Aurora Camera")
                    : destinationFolder;

                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss-fff}.jpg");
                Cv2.ImWrite(path, frame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                return new StorageFileResult(path);
            }
        }

        /// <summary>Starts recording the same continuous capture stream to an MP4 file.</summary>
        public async Task<StorageFileResult> StartRecordingAsync(string? destinationFolder = null)
        {
            if (!IsInitialized)
                await InitializeAsync();
            if (!IsActive)
                await StartPreviewAsync();
            if (IsRecording)
                throw new InvalidOperationException("Camera video recording is already active.");

            var folder = string.IsNullOrWhiteSpace(destinationFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Aurora Camera")
                : destinationFolder;
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss-fff}.mp4");
            var writer = new VideoWriter(
                path,
                FourCC.MP4V,
                30,
                new OpenCvSharp.Size(1280, 720));

            if (!writer.IsOpened())
            {
                writer.Dispose();
                throw new InvalidOperationException("Aurora could not initialize MP4 video recording on this system.");
            }

            lock (_captureSync)
                _videoWriter = writer;

            return new StorageFileResult(path);
        }

        public Task<StorageFileResult?> StopRecordingAsync()
        {
            lock (_captureSync)
            {
                if (_videoWriter == null)
                    return Task.FromResult<StorageFileResult?>(null);

                _videoWriter.Release();
                _videoWriter.Dispose();
                _videoWriter = null;
            }

            return Task.FromResult<StorageFileResult?>(null);
        }

        public async Task StopAsync()
        {
            await StopPreviewAsync();
            lock (_captureSync)
            {
                if (_videoWriter != null)
                {
                    _videoWriter.Release();
                    _videoWriter.Dispose();
                    _videoWriter = null;
                }

                if (_capture != null)
                {
                    _capture.Release();
                    _capture.Dispose();
                    _capture = null;
                }

                _latestFrame?.Dispose();
                _latestFrame = null;
                LatestFrameJpeg = null;
            }
        }

        private void PreviewLoop(CancellationToken token)
        {
            using var frame = new Mat();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    lock (_captureSync)
                    {
                        if (_capture?.IsOpened() != true)
                            break;

                        if (!_capture.Read(frame) || frame.Empty())
                        {
                            // Keep the stream alive through transient driver read failures.
                        }
                        else
                        {
                            _latestFrame?.Dispose();
                            _latestFrame = frame.Clone();

                            Cv2.ImEncode(".jpg", frame, out var encoded, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
                            LatestFrameJpeg = encoded.ToArray();

                            if (_videoWriter?.IsOpened() == true)
                                _videoWriter.Write(frame);

                            FrameReady?.Invoke(this, new CameraFrameEventArgs(
                                LatestFrameJpeg,
                                frame.Width,
                                frame.Height,
                                DateTimeOffset.UtcNow));
                        }
                    }

                    Thread.Sleep(15);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _gate.Dispose();
        }
    }

    public sealed class CameraFrameEventArgs : EventArgs
    {
        public CameraFrameEventArgs(byte[] jpeg, int width, int height, DateTimeOffset timestamp)
        {
            Jpeg = jpeg;
            Width = width;
            Height = height;
            Timestamp = timestamp;
        }

        public byte[] Jpeg { get; }
        public int Width { get; }
        public int Height { get; }
        public DateTimeOffset Timestamp { get; }
    }

    public enum CameraPermissionResult
    {
        Allowed,
        Denied,
        NoCamera,
        Error
    }

    public sealed record CameraDeviceInfo(string Id, string Name);
    public sealed record StorageFileResult(string Path);
}
