using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Media.Capture;

namespace ZoeyOS.App.Services
{
    public enum PermissionResult
    {
        Allowed,
        Denied,
        Unavailable
    }

    /// <summary>
    /// Requests OS-level consent for location, microphone, and camera. Windows
    /// owns the native consent UI; these methods complete only after Windows has
    /// resolved the request or the request fails immediately.
    /// </summary>
    public sealed class WindowsPermissionService
    {
        public async Task<PermissionResult> RequestLocationAsync()
        {
            try
            {
                var status = await Geolocator.RequestAccessAsync();
                return status switch
                {
                    GeolocationAccessStatus.Allowed => PermissionResult.Allowed,
                    GeolocationAccessStatus.Denied => PermissionResult.Denied,
                    _ => PermissionResult.Unavailable
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowsPermissionService] Location request failed: {ex}");
                return PermissionResult.Unavailable;
            }
        }

        public Task<PermissionResult> RequestMicrophoneAsync() =>
            RequestMediaCaptureAsync(StreamingCaptureMode.Audio);

        public Task<PermissionResult> RequestCameraAsync() =>
            RequestMediaCaptureAsync(StreamingCaptureMode.Video);

        private static async Task<PermissionResult> RequestMediaCaptureAsync(StreamingCaptureMode mode)
        {
            MediaCapture? capture = null;
            try
            {
                capture = new MediaCapture();
                await capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = mode,
                    MediaCategory = MediaCategory.Other
                });
                return PermissionResult.Allowed;
            }
            catch (UnauthorizedAccessException)
            {
                return PermissionResult.Denied;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowsPermissionService] Media request failed: {ex}");
                return PermissionResult.Unavailable;
            }
            finally
            {
                try { capture?.Dispose(); } catch { }
            }
        }
    }
}