using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;

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
            if (SelectedDeviceId == null || !Devices.Any(d => d.Id == SelectedDeviceId))
                SelectedDeviceId = Devices.FirstOrDefault()?.Id;
            return Devices;
        }

        public async Task<CameraPermissionResult> CheckPermissionAsync()
        {
            try
            {
                if (Devices.Count == 0) await RefreshDevicesAsync();
                if (Devices.Count == 0) return CameraPermissionResult.NoCamera;
                var capture = new MediaCapture();
                try
                {
                    await capture.InitializeAsync(new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = SelectedDeviceId ?? Devices[0].Id,
                        StreamingCaptureMode = StreamingCaptureMode.Video,
                        SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                        MemoryPreference = MediaCaptureMemoryPreference.Auto
                    });
                    return CameraPermissionResult.Allowed;
                }
                catch (UnauthorizedAccessException)
                {
                    return CameraPermissionResult.Denied;
                }
                finally { capture.Dispose(); }
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
                if (Devices.Count == 0) await RefreshDevicesAsync();
                SelectedDeviceId = deviceId ?? SelectedDeviceId ?? Devices.FirstOrDefault()?.Id;
                if (string.IsNullOrWhiteSpace(SelectedDeviceId)) throw new InvalidOperationException("No camera devices were found.");

                await StopAsync();
                var capture = new MediaCapture();
                try
                {
                    await capture.InitializeAsync(new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = SelectedDeviceId,
                        StreamingCaptureMode = StreamingCaptureMode.Video,
                        SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                        MemoryPreference = MediaCaptureMemoryPreference.Auto
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    capture.Dispose();
                    throw new UnauthorizedAccessException("Windows denied camera access. Turn on Camera access and 'Let desktop apps access your camera' in Windows Settings > Privacy & security > Camera.");
                }

                _capture = capture;
                _capture.Failed += OnCaptureFailed;
                _frameSource = _capture.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
            }
            finally { _gate.Release(); }
        }

        public async Task StartPreviewAsync()
        {
            if (_capture == null) await InitializeAsync();
            await _capture!.StartPreviewAsync();
        }

        public async Task StopPreviewAsync()
        {
            if (_capture == null) return;
            try { await _capture.StopPreviewAsync(); } catch { }
        }

        public string GetStatus()
        {
            if (Devices.Count == 0) return "No camera devices found.";
            if (_capture == null) return $"Camera ready: {Devices.FirstOrDefault(d => d.Id == SelectedDeviceId)?.Name ?? "default camera"}.";
            return IsActive ? "Camera active." : "Camera initialized.";
        }

        public void OpenWindowsCameraApp()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "microsoft.windows.camera:",
                UseShellExecute = true
            });
        }

        public async Task<StorageFile> CapturePhotoAsync(string? destinationFolder = null)
        {
            if (_capture == null) throw new InvalidOperationException("Camera is not initialized.");
            var folder = destinationFolder == null ? ApplicationData.Current.LocalFolder : await StorageFolder.GetFolderFromPathAsync(destinationFolder);
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

    public enum CameraPermissionResult
    {
        Allowed,
        Denied,
        NoCamera,
        Error
    }

    public sealed record CameraDeviceInfo(string Id, string Name);
}
