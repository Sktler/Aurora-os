using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Media.Capture;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Explicitly requests Windows privacy permissions before Aurora first uses
    /// location, microphone, or camera features. Windows owns the consent UI.
    /// </summary>
    public sealed class WindowsPermissionService
    {
        public async Task<bool> RequestLocationAsync()
        {
            var access = await Geolocator.RequestAccessAsync();
            return access == GeolocationAccessStatus.Allowed;
        }

        public Task<bool> RequestMicrophoneAsync() => RequestMediaCaptureAsync(StreamingCaptureMode.Audio);

        public Task<bool> RequestCameraAsync() => RequestMediaCaptureAsync(StreamingCaptureMode.Video);

        private static async Task<bool> RequestMediaCaptureAsync(StreamingCaptureMode mode)
        {
            MediaCapture? capture = null;
            try
            {
                capture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = mode,
                    MediaCategory = MediaCategory.Other
                };

                // Windows displays its native consent prompt here when permission
                // has not yet been decided. This must run on the foreground STA/UI thread.
                await capture.InitializeAsync(settings);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (capture != null)
                {
                    try { capture.Dispose(); } catch { }
                }
            }
        }
    }
}
