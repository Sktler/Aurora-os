using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Foundation;

namespace ZoeyOS.App.Services
{
    public sealed class CameraService : IAsyncDisposable
    {
        private MediaCapture? _capture;
        private MediaFrameSource? _frameSource;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public bool IsInitialized => _capture != null;
        public bool IsActive => _capture != null && _frameSource != null;
        public IReadOnlyList<CameraDeviceInfo> Devices { get; private set; } = Array.Empty<CameraDeviceInfo>();
        public string? SelectedDeviceId { get; private set; }

        public async Task<IReadOnlyList<CameraDeviceInfo>> RefreshDevicesAsync()
        {
            var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetVideoCaptureSelector());
            Devices = devices.Select(d => new CameraDeviceInfo(d.Id, d.Name)).ToArray();
            if (SelectedDeviceId == null) SelectedDeviceId = Devices.FirstOrDefault()?.Id;
            return Devices;
        }

        public async Task<bool> CheckPermissionAsync()
        {
            try
            {
                return AppCapability.Create("Webcam").CheckAccess() == AppCapabilityAccessStatus.Allowed;
            }
            catch { return false; }
        }

        public async Task InitializeAsync(string? deviceId = null)
        {
            await _gate.WaitAsync();
            try
            {
                if (!await CheckPermissionAsync())
                    throw new UnauthorizedAccessException("Camera access is blocked in Windows Privacy & Security settings.");

                if (Devices.Count == 0) await RefreshDevicesAsync();
                SelectedDeviceId = deviceId ?? SelectedDeviceId ?? Devices.FirstOrDefault()?.Id;
                if (string.IsNullOrWhiteSpace(SelectedDeviceId)) throw new InvalidOperationException("No camera devices were found.");

                await StopAsync();
                _capture = new MediaCapture();
                await _capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = SelectedDeviceId,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = MediaCaptureMemoryPreference.Auto
                });
                _capture.Failed += OnCaptureFailed;
                _frameSource = _capture.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
            }
            finally { _gate.Release(); }
        }

        public async Task<StorageFile> CapturePhotoAsync(string? destinationFolder = null)
        {
            if (_capture == null) throw new InvalidOperationException("Camera is not initialized.");
            var folder = destinationFolder == null
                ? ApplicationData.Current.LocalFolder
                : await StorageFolder.GetFolderFromPathAsync(destinationFolder);
            var file = await folder.CreateFileAsync($"aurora-camera-{DateTime.Now:yyyyMMdd-HHmmss}.jpg", CreationCollisionOption.GenerateUniqueName);
            await _capture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
            return file;
        }

        public async Task StopAsync()
        {
            if (_capture != null)
            {
                try { await _capture.StopPreviewAsync(); } catch { }
                _capture.Failed -= OnCaptureFailed;
                _capture.Dispose();
                _capture = null;
            }
            _frameSource = null;
        }

        private async void OnCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            await StopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _gate.Dispose();
        }
    }

    public sealed record CameraDeviceInfo(string Id, string Name);
}