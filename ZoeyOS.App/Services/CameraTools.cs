using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// First-class Aurora camera tool facade. Keeps AI/tool invocation separate
    /// from the OpenCvSharp device implementation in CameraService.
    /// </summary>
    public static class CameraTools
    {
        public static readonly object[] Definitions =
        {
            new { name = "camera_open", description = "Opens an Aurora webcam by device id.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_close", description = "Closes the active Aurora webcam.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_list", description = "Lists webcams available to Aurora.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_permission", description = "Checks webcam access and device availability.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_status", description = "Gets the current webcam status.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_start_preview", description = "Starts the live webcam preview.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_stop_preview", description = "Stops the live webcam preview.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_capture_photo", description = "Captures a JPEG photo from the active webcam.", input_schema = new { type = "object", properties = new { destination_folder = new { type = "string" } } } },
            new { name = "camera_open_windows_app", description = "Opens the native Windows Camera application.", input_schema = new { type = "object", properties = new { } } }
        };

        public static Task<string> ExecuteAsync(string name, JsonElement input)
        {
            return name switch
            {
                "camera_open" => OpenAsync(input),
                "camera_close" => CloseAsync(),
                "camera_list" => ListAsync(),
                "camera_permission" => PermissionAsync(),
                "camera_status" => Task.FromResult(App.Camera.GetStatus()),
                "camera_start_preview" => StartPreviewAsync(input),
                "camera_stop_preview" => StopPreviewAsync(),
                "camera_capture_photo" => CaptureAsync(input),
                "camera_open_windows_app" => OpenWindowsAppAsync(),
                _ => Task.FromResult($"Unknown camera tool: {name}")
            };
        }

        private static async Task<string> OpenAsync(JsonElement input)
        {
            await App.Camera.InitializeAsync(input.TryGetProperty("device_id", out var id) ? id.GetString() : null);
            return App.Camera.GetStatus();
        }

        private static async Task<string> CloseAsync()
        {
            await App.Camera.StopAsync();
            return "Camera closed.";
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
                CameraPermissionResult.Denied => "Camera access denied. Enable Camera access and desktop-app camera access in Windows Privacy & security settings.",
                CameraPermissionResult.NoCamera => "No camera devices found.",
                _ => "Camera permission check failed."
            };
        }

        private static async Task<string> StartPreviewAsync(JsonElement input)
        {
            await App.Camera.InitializeAsync(input.TryGetProperty("device_id", out var id) ? id.GetString() : null);
            await App.Camera.StartPreviewAsync();
            return App.Camera.GetStatus();
        }

        private static async Task<string> StopPreviewAsync()
        {
            await App.Camera.StopPreviewAsync();
            return "Camera preview stopped.";
        }

        private static async Task<string> CaptureAsync(JsonElement input)
        {
            if (!App.Camera.IsInitialized)
                await App.Camera.InitializeAsync();
            var folder = input.TryGetProperty("destination_folder", out var value) ? value.GetString() : null;
            var result = await App.Camera.CapturePhotoAsync(folder);
            return $"Photo captured: {result.Path}";
        }

        private static Task<string> OpenWindowsAppAsync()
        {
            App.Camera.OpenWindowsCameraApp();
            return Task.FromResult("Windows Camera opened.");
        }
    }
}
