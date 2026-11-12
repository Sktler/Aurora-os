using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Aurora.App.Services
{
    /// <summary>
    /// Windows webcam service backed directly by OpenCvSharp.
    /// The camera remains open and continuously captures frames until explicitly stopped.
    /// No WPF/OpenCvSharp.WpfExtensions types are used here; consumers receive encoded JPEG frames.
    /// </summary>
    public sealed class CameraService : IAsyncDisposable
    {
        private readonly Func<bool> _isEnabled;
        // Gate holders must not depend on the UI dispatcher, which waits for disposal at exit.
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _captureSync = new();
        private bool _disposed;
        private VideoCapture? _capture;
        private CancellationTokenSource? _previewCts;
        private Task? _previewTask;
        private Mat? _latestFrame;
        private VideoWriter? _videoWriter;
        private int _selectedIndex;

        public CameraService(Func<bool> isEnabled) => _isEnabled = isEnabled;

        public bool IsInitialized => _capture != null;
        public bool IsActive => IsInitialized && _previewTask is { IsCompleted: false };
        public bool IsRecording => _videoWriter != null;
        public IReadOnlyList<CameraDeviceInfo> Devices { get; private set; } = Array.Empty<CameraDeviceInfo>();
        public string? SelectedDeviceId => _selectedIndex.ToString();

        /// <summary>Latest JPEG-encoded frame. This is UI-framework neutral.</summary>
        public byte[]? LatestFrameJpeg { get; private set; }

        public event EventHandler<CameraFrameEventArgs>? FrameReady;

        public async Task<IReadOnlyList<CameraDeviceInfo>> RefreshDevicesAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureAccessEnabled();
                return await Task.Run(() => RefreshDevices(cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private IReadOnlyList<CameraDeviceInfo> RefreshDevices(CancellationToken cancellationToken)
        {
            lock (_captureSync)
            {
                // Do not open temporary probes while the persistent camera is in use.
                if (_capture?.IsOpened() == true)
                    return Devices;
            }

            var devices = new List<CameraDeviceInfo>();
            for (var index = 0; index < 10; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureAccessEnabled();
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

            EnsureAccessEnabled();
            Devices = devices;
            if (!Devices.Any(d => d.Id == _selectedIndex.ToString()))
                _selectedIndex = devices.Count == 0 ? 0 : int.Parse(devices[0].Id);

            return Devices;
        }

        public async Task<CameraPermissionResult> CheckPermissionAsync(string? deviceId = null)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureAccessEnabled();
                if (Devices.Count == 0)
                    await Task.Run(() => RefreshDevices(default)).ConfigureAwait(false);
                if (Devices.Count == 0)
                    return CameraPermissionResult.NoCamera;

                var index = GetDeviceIndex(deviceId);
                lock (_captureSync)
                {
                    // Avoid probing any camera while DirectShow already has a live capture.
                    if (_capture?.IsOpened() == true)
                        return index == _selectedIndex ? CameraPermissionResult.Allowed : CameraPermissionResult.Busy;
                }

                return await Task.Run(() =>
                {
                    EnsureAccessEnabled();
                    using var probe = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                    EnsureAccessEnabled();
                    return probe.IsOpened() ? CameraPermissionResult.Allowed : CameraPermissionResult.Denied;
                }).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return CameraPermissionResult.Denied;
            }
            catch
            {
                return CameraPermissionResult.Error;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task InitializeAsync(string? deviceId = null, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await InitializeCoreAsync(deviceId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task InitializeCoreAsync(string? deviceId, CancellationToken cancellationToken)
        {
            EnsureAccessEnabled();
            if (Devices.Count == 0)
                await Task.Run(() => RefreshDevices(cancellationToken), cancellationToken).ConfigureAwait(false);

            if (Devices.Count == 0)
                throw new InvalidOperationException("No camera devices were found.");

            var index = GetDeviceIndex(deviceId);
            if (IsInitialized && index == _selectedIndex)
                return;

            await StopCoreAsync().ConfigureAwait(false);
            await Task.Run(() => OpenCapture(index, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        private int GetDeviceIndex(string? deviceId)
        {
            var index = int.TryParse(deviceId, out var requested) ? requested : _selectedIndex;
            if (!Devices.Any(device => device.Id == index.ToString()))
                throw new InvalidOperationException($"Camera {index + 1} is not available.");
            return index;
        }

        private void OpenCapture(int index, CancellationToken cancellationToken)
        {
            EnsureAccessEnabled();
            var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
            try
            {
                if (!capture.IsOpened())
                    throw new UnauthorizedAccessException("Windows or the camera driver would not allow Aurora to open the webcam. Check Windows Settings > Privacy & security > Camera and enable camera access for desktop apps.");

                capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                capture.Set(VideoCaptureProperties.FrameHeight, 720);
                capture.Set(VideoCaptureProperties.Fps, 30);
                cancellationToken.ThrowIfCancellationRequested();
                EnsureAccessEnabled();
                lock (_captureSync)
                {
                    _selectedIndex = index;
                    _capture = capture;
                }
            }
            catch
            {
                capture.Dispose();
                throw;
            }
        }

        /// <summary>Starts the persistent webcam capture loop. Calling it again is harmless.</summary>
        public async Task StartPreviewAsync(CancellationToken cancellationToken = default, string? deviceId = null)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartPreviewCoreAsync(deviceId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task StartPreviewCoreAsync(string? deviceId, CancellationToken cancellationToken)
        {
            await InitializeCoreAsync(deviceId, cancellationToken).ConfigureAwait(false);
            EnsureAccessEnabled();
            cancellationToken.ThrowIfCancellationRequested();
            if (IsActive)
                return;

            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;
            _previewTask = Task.Run(() => PreviewLoop(token), token);
        }

        public async Task StopPreviewAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopPreviewCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task StopPreviewCoreAsync()
        {
            _previewCts?.Cancel();
            try
            {
                if (_previewTask != null)
                    await _previewTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _previewTask = null;
                _previewCts?.Dispose();
                _previewCts = null;
            }
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
            EnsureAccessEnabled();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "microsoft.windows.camera:",
                UseShellExecute = true
            });
        }

        public async Task<StorageFileResult> CapturePhotoAsync(
            string? destinationFolder = null,
            CancellationToken cancellationToken = default,
            string? deviceId = null)
        {
            Mat? frame;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartPreviewCoreAsync(deviceId, cancellationToken).ConfigureAwait(false);
                frame = await WaitForFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            if (frame == null)
                throw new InvalidOperationException("The webcam did not return a frame.");

            using (frame)
            {
                var path = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var folder = string.IsNullOrWhiteSpace(destinationFolder)
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Aurora Camera")
                        : destinationFolder;

                    Directory.CreateDirectory(folder);
                    var outputPath = Path.Combine(folder, $"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss-fff}.jpg");
                    Cv2.ImWrite(outputPath, frame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                    return outputPath;
                }, cancellationToken).ConfigureAwait(false);
                return new StorageFileResult(path);
            }
        }

        private async Task<Mat?> WaitForFrameAsync(CancellationToken cancellationToken)
        {
            var deadline = Environment.TickCount64 + 2000;
            do
            {
                EnsureAccessEnabled();
                cancellationToken.ThrowIfCancellationRequested();
                lock (_captureSync)
                {
                    if (_latestFrame != null)
                        return _latestFrame.Clone();
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            } while (Environment.TickCount64 < deadline && IsActive);

            return null;
        }

        /// <summary>Starts recording the same continuous capture stream to an MP4 file.</summary>
        public async Task<StorageFileResult> StartRecordingAsync(
            string? destinationFolder = null,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartPreviewCoreAsync(null, cancellationToken).ConfigureAwait(false);
                if (IsRecording)
                    throw new InvalidOperationException("Camera video recording is already active.");

                return await Task.Run(() => StartRecordingCore(destinationFolder, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private StorageFileResult StartRecordingCore(string? destinationFolder, CancellationToken cancellationToken)
        {
            EnsureAccessEnabled();
            cancellationToken.ThrowIfCancellationRequested();
            var folder = string.IsNullOrWhiteSpace(destinationFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Aurora Camera")
                : destinationFolder;
            Directory.CreateDirectory(folder);

            var outputPath = Path.Combine(folder, $"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss-fff}.mp4");
            var writer = new VideoWriter(
                outputPath,
                FourCC.MP4V,
                30,
                new OpenCvSharp.Size(1280, 720));

            try
            {
                if (!writer.IsOpened())
                    throw new InvalidOperationException("Aurora could not initialize MP4 video recording on this system.");

                cancellationToken.ThrowIfCancellationRequested();
                EnsureAccessEnabled();
                lock (_captureSync)
                    _videoWriter = writer;
                return new StorageFileResult(outputPath);
            }
            catch
            {
                writer.Dispose();
                throw;
            }
        }

        public async Task<StorageFileResult?> StopRecordingAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (_captureSync)
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                }
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task StopAsync() => StopAsync(onlyIfDisabled: false);

        internal Task StopIfDisabledAsync() => StopAsync(onlyIfDisabled: true);

        private async Task StopAsync(bool onlyIfDisabled)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!onlyIfDisabled || !_isEnabled())
                    await StopCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task StopCoreAsync()
        {
            try
            {
                await StopPreviewCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                if (_capture != null || _videoWriter != null || _latestFrame != null)
                    await Task.Run(ReleaseCapture).ConfigureAwait(false);
            }
        }

        private void ReleaseCapture()
        {
            lock (_captureSync)
            {
                _videoWriter?.Dispose();
                _videoWriter = null;
                _capture?.Dispose();
                _capture = null;
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
                        if (!_isEnabled() || _capture?.IsOpened() != true)
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

                    token.WaitHandle.WaitOne(15);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    token.WaitHandle.WaitOne(100);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                _disposed = true;
                await StopCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureAccessEnabled()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_isEnabled())
                throw new UnauthorizedAccessException("Camera permission is disabled in Aurora Settings.");
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
        Error,
        Busy
    }

    public sealed record CameraDeviceInfo(string Id, string Name);
    public sealed record StorageFileResult(string Path);
}
