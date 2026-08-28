using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace ZoeyOS.App.Services
{
    public sealed class CameraService : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private VideoCapture? _capture;
        private CancellationTokenSource? _previewCts;
        private Task? _previewTask;
        private Mat? _latestFrame;
        private int _selectedIndex;

        public bool IsInitialized => _capture?.IsOpened() == true;
        public bool IsActive => IsInitialized && _previewTask is { IsCompleted: false };
        public IReadOnlyList<CameraDeviceInfo> Devices { get; private set; } = Array.Empty<CameraDeviceInfo>();
        public string? SelectedDeviceId => _selectedIndex.ToString();
        public BitmapSource? LatestFrame { get; private set; }

        public event EventHandler<BitmapSource>? FrameReady;

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
                    probe.Set(VideoCaptureProperties.FrameWidth, 320);
                    probe.Set(VideoCaptureProperties.FrameHeight, 240);
                    devices.Add(new CameraDeviceInfo(index.ToString(), $"Camera {index + 1}"));
                }
                catch
                {
                    // Some indices/drivers throw while probing. Keep scanning the remaining devices.
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

        public async Task StartPreviewAsync()
        {
            if (!IsInitialized)
                await InitializeAsync();
            if (IsActive)
                return;

            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;
            _previewTask = Task.Run(() => PreviewLoop(token), token);
        }

        public async Task StopPreviewAsync()
        {
            _previewCts?.Cancel();
            if (_previewTask != null)
            {
                try { await _previewTask; } catch (OperationCanceledException) { }
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
            return IsActive ? $"Camera active: {name}." : $"Camera initialized: {name}.";
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

            using var frame = new Mat();
            if (!_capture!.Read(frame) || frame.Empty())
                throw new InvalidOperationException("The webcam did not return a frame.");

            var folder = destinationFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Aurora Camera");
            }
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss}.jpg");
            Cv2.ImWrite(path, frame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            return new StorageFileResult(path);
        }

        public async Task StopAsync()
        {
            await StopPreviewAsync();
            if (_capture != null)
            {
                _capture.Release();
                _capture.Dispose();
                _capture = null;
            }

            _latestFrame?.Dispose();
            _latestFrame = null;
            LatestFrame = null;
        }

        private void PreviewLoop(CancellationToken token)
        {
            using var frame = new Mat();
            while (!token.IsCancellationRequested && _capture?.IsOpened() == true)
            {
                try
                {
                    if (!_capture.Read(frame) || frame.Empty())
                    {
                        Thread.Sleep(30);
                        continue;
                    }

                    var bitmap = BitmapSourceConverter.ToBitmapSource(frame);
                    bitmap.Freeze();
                    LatestFrame = bitmap;
                    FrameReady?.Invoke(this, bitmap);
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
