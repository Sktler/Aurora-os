using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// First-class Aurora camera tool facade. The primary camera command turns on
    /// the continuous webcam stream; photo capture remains available while streaming.
    /// </summary>
    public static class CameraTools
    {
        public static readonly object[] Definitions =
        {
            new { name = "camera", description = "Turns on the primary Aurora webcam and keeps a continuous live stream running until stopped.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_open", description = "Opens a webcam and starts its continuous live stream.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_close", description = "Closes the active Aurora webcam and stops its live stream.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_list", description = "Lists webcams available to Aurora.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_permission", description = "Checks webcam access and device availability.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_status", description = "Gets the current webcam status.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_start_preview", description = "Starts and maintains the continuous live webcam preview until explicitly stopped.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_stop_preview", description = "Stops the live webcam preview without closing the camera device.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_capture_photo", description = "Captures a JPEG photo from the active webcam without stopping the live stream.", input_schema = new { type = "object", properties = new { destination_folder = new { type = "string" } } } },
            new { name = "camera_open_windows_app", description = "Opens the native Windows Camera application.", input_schema = new { type = "object", properties = new { } } }
        };

        public static bool IsCameraTool(string name) => name switch
        {
            "camera" or "camera_open" or "camera_close" or "camera_list" or "camera_permission" or
            "camera_status" or "camera_start_preview" or "camera_stop_preview" or
            "camera_capture_photo" or "camera_open_windows_app" => true,
            _ => false
        };

        public static Task<string> ExecuteAsync(string name, JsonElement input)
        {
            return name switch
            {
                "camera" => StartPreviewAsync(input),
                "camera_open" => OpenAsync(input),
                "camera_close" => CloseAsync(),
                "camera_list" => ListAsync(),
                "camera_permission" => PermissionAsync(),
                "camera_status" => StatusAsync(),
                "camera_start_preview" => StartPreviewAsync(input),
                "camera_stop_preview" => StopPreviewAsync(),
                "camera_capture_photo" => CaptureAsync(input),
                "camera_open_windows_app" => OpenWindowsAppAsync(),
                _ => Task.FromResult($"Unknown camera tool: {name}")
            };
        }

        private static async Task<string> StatusAsync() => App.Camera.GetStatus();

        private static async Task<string> OpenAsync(JsonElement input)
        {
            var deviceId = input.TryGetProperty("device_id", out var id) ? id.GetString() : null;
            if (!App.Camera.IsInitialized)
                await App.Camera.InitializeAsync(deviceId);
            await App.Camera.StartPreviewAsync();
            return App.Camera.GetStatus();
        }

        private static async Task<string> CloseAsync()
        {
            await App.Camera.StopAsync();
            return "Camera closed and live stream stopped.";
        }

        private static async Task<string> ListAsync()
        {
            var devices = await App.Camera.RefreshDevicesAsync();
            return devices.Count == 0 ? "No cameras found." : string.Join("\n", devices.Select(d => $"{d.Name} ({d.Id})"));
        }

        private static async Task<string> PermissionAsync()
        {
            return (await App.Camera.CheckPermissionAsync()) switch
            {
                CameraPermissionResult.Allowed => "Camera access allowed.",
                CameraPermissionResult.Denied => "Camera access denied. Enable Camera access and desktop-app camera access in Windows Privacy & security > Camera.",
                CameraPermissionResult.NoCamera => "No camera devices found.",
                _ => "Camera permission check failed."
            };
        }

        private static async Task<string> StartPreviewAsync(JsonElement input)
        {
            var deviceId = input.TryGetProperty("device_id", out var id) ? id.GetString() : null;
            if (!App.Camera.IsInitialized)
                await App.Camera.InitializeAsync(deviceId);
            await App.Camera.StartPreviewAsync();
            return App.Camera.GetStatus();
        }

        private static async Task<string> StopPreviewAsync()
        {
            await App.Camera.StopPreviewAsync();
            return "Camera live stream stopped.";
        }

        private static async Task<string> CaptureAsync(JsonElement input)
        {
            if (!App.Camera.IsInitialized)
                await App.Camera.InitializeAsync();
            var folder = input.TryGetProperty("destination_folder", out var value) ? value.GetString() : null;
            var result = await App.Camera.CapturePhotoAsync(folder);
            return $"Photo captured without stopping the live stream: {result.Path}";
        }

        private static Task<string> OpenWindowsAppAsync()
        {
            App.Camera.OpenWindowsCameraApp();
            return Task.FromResult("Windows Camera opened.");
        }
    }
}
