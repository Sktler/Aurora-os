using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public static class SystemTools
    {
        private static readonly MediaControlService Media = new();
        public static List<object> Definitions => new()
        {
            new { name = "get_weather", description = "Gets current real-time weather conditions for a place.", input_schema = new { type = "object", properties = new { location = new { type = "string" } }, required = new[] { "location" } } },
            new { name = "web_search", description = "Searches the web for a query.", input_schema = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" } } },
            new { name = "get_now_playing", description = "Gets the Windows media currently playing.", input_schema = new { type = "object", properties = new { } } },
            new { name = "list_media_sessions", description = "Lists Windows media sessions.", input_schema = new { type = "object", properties = new { } } },
            new { name = "media_play", description = "Starts Windows media playback.", input_schema = new { type = "object", properties = new { app = new { type = "string" } } } },
            new { name = "media_pause", description = "Pauses Windows media playback.", input_schema = new { type = "object", properties = new { app = new { type = "string" } } } },
            new { name = "media_toggle_play_pause", description = "Toggles Windows media playback.", input_schema = new { type = "object", properties = new { app = new { type = "string" } } } },
            new { name = "media_next", description = "Skips Windows media forward.", input_schema = new { type = "object", properties = new { app = new { type = "string" } } } },
            new { name = "media_previous", description = "Goes to the previous Windows media track.", input_schema = new { type = "object", properties = new { app = new { type = "string" } } } },
            new { name = "set_system_volume", description = "Sets Windows master volume from 0 to 100.", input_schema = new { type = "object", properties = new { percent = new { type = "number" } }, required = new[] { "percent" } } },
            new { name = "toggle_system_mute", description = "Mutes or unmutes Windows system audio.", input_schema = new { type = "object", properties = new { mute = new { type = "boolean" } }, required = new[] { "mute" } } },
            new { name = "windows_list_applications", description = "Lists running Windows applications.", input_schema = new { type = "object", properties = new { } } },
            new { name = "windows_launch_application", description = "Launches a Windows application or URI.", input_schema = new { type = "object", properties = new { target = new { type = "string" } }, required = new[] { "target" } } },
            new { name = "windows_open_path", description = "Opens a Windows file or folder.", input_schema = new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } } },
            new { name = "windows_read_file", description = "Reads a text file.", input_schema = new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } } },
            new { name = "windows_write_file", description = "Writes a text file.", input_schema = new { type = "object", properties = new { path = new { type = "string" }, content = new { type = "string" } }, required = new[] { "path", "content" } } },
            new { name = "windows_get_clipboard", description = "Reads Windows clipboard text.", input_schema = new { type = "object", properties = new { } } },
            new { name = "windows_set_clipboard", description = "Writes Windows clipboard text.", input_schema = new { type = "object", properties = new { text = new { type = "string" } }, required = new[] { "text" } } },
            new { name = "windows_run_command", description = "Runs an approved Windows command.", input_schema = new { type = "object", properties = new { command = new { type = "string" }, arguments = new { type = "string" } }, required = new[] { "command" } } },
            new { name = "windows_capture_screen", description = "Captures the primary Windows display.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera", description = "Primary Aurora webcam tool. Reports camera availability and status.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_open_windows_app", description = "Opens the native Windows Camera app.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_list_devices", description = "Lists Windows cameras.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_check_permission", description = "Checks whether Windows currently allows Aurora to access a webcam.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_status", description = "Reports the current Aurora camera status.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_start_preview", description = "Initializes and starts the Windows webcam capture stream.", input_schema = new { type = "object", properties = new { device_id = new { type = "string" } } } },
            new { name = "camera_stop_preview", description = "Stops the active Windows webcam preview stream.", input_schema = new { type = "object", properties = new { } } },
            new { name = "camera_capture_photo", description = "Captures a photo with the Windows camera.", input_schema = new { type = "object", properties = new { } } },
            new { name = "mcp_list_servers", description = "Lists connected MCP servers.", input_schema = new { type = "object", properties = new { } } },
            new { name = "mcp_list_tools", description = "Lists tools exposed by a connected MCP server.", input_schema = new { type = "object", properties = new { server = new { type = "string" } }, required = new[] { "server" } } },
            new { name = "mcp_call_tool", description = "Calls a tool on a connected MCP server.", input_schema = new { type = "object", properties = new { server = new { type = "string" }, tool = new { type = "string" }, arguments = new { type = "object" } }, required = new[] { "server", "tool" } } }
        };

        public static async Task<string> ExecuteAsync(string toolName, JsonElement input)
        {
            switch (toolName)
            {
                case "camera": return await CameraTools.ExecuteAsync("camera", input);
                case "camera_open": return await CameraTools.ExecuteAsync("camera_open", input);
                case "camera_close": return await CameraTools.ExecuteAsync("camera_close", input);
                case "camera_list": return await CameraTools.ExecuteAsync("camera_list", input);
                case "camera_permission": return await CameraTools.ExecuteAsync("camera_permission", input);
                case "get_weather": return await App.Weather.GetCurrentWeatherAsync(input.GetProperty("location").GetString() ?? "");
                case "web_search": { var q = input.GetProperty("query").GetString() ?? ""; return await App.WebSearch.TryInstantAnswerAsync(q) ?? App.WebSearch.OpenSearchInBrowser(q); }
                case "get_now_playing": { var x = await Media.GetNowPlayingAsync(); return x == null ? "Nothing is currently playing." : $"App: {x.AppName}\nTitle: {x.Title}\nArtist: {x.Artist}\nPlayback: {x.PlaybackStatus}"; }
                case "list_media_sessions": { var xs = await Media.ListSessionsAsync(); return xs.Count == 0 ? "No Windows media sessions." : string.Join("\n", xs.ConvertAll(x => $"{x.AppName}: {x.Title} — {x.Artist} — {x.PlaybackStatus}")); }
                case "media_play": return (await Media.ControlAsync("play", GetApp(input))).Message;
                case "media_pause": return (await Media.ControlAsync("pause", GetApp(input))).Message;
                case "media_toggle_play_pause": return (await Media.ControlAsync("toggle", GetApp(input))).Message;
                case "media_next": return (await Media.ControlAsync("next", GetApp(input))).Message;
                case "media_previous": return (await Media.ControlAsync("previous", GetApp(input))).Message;
                case "set_system_volume": { var p = input.GetProperty("percent").GetDouble(); if (p < 0 || p > 100) return "Volume must be between 0 and 100."; SystemVolumeControl.SetVolume((float)(p / 100)); return $"System volume set to {p:0}%."; }
                case "toggle_system_mute": { var m = input.GetProperty("mute").GetBoolean(); SystemVolumeControl.SetMute(m); return m ? "System audio muted." : "System audio unmuted."; }
                case "windows_list_applications": return FormatProcesses(App.WindowsAutomation.GetProcesses());
                case "windows_launch_application": App.WindowsAutomation.Launch(input.GetProperty("target").GetString() ?? ""); return "Application launched.";
                case "windows_open_path": App.WindowsAutomation.OpenPath(input.GetProperty("path").GetString() ?? ""); return "Opened.";
                case "windows_read_file": return App.WindowsAutomation.ReadText(input.GetProperty("path").GetString() ?? "");
                case "windows_write_file": App.WindowsAutomation.WriteText(input.GetProperty("path").GetString() ?? "", input.GetProperty("content").GetString() ?? ""); return "File written.";
                case "windows_get_clipboard": return App.WindowsAutomation.GetClipboardText();
                case "windows_set_clipboard": App.WindowsAutomation.SetClipboardText(input.GetProperty("text").GetString() ?? ""); return "Clipboard updated.";
                case "windows_run_command": { var exit = await App.WindowsAutomation.RunApprovedCommandAsync(input.GetProperty("command").GetString() ?? "", input.TryGetProperty("arguments", out var a) ? a.GetString() ?? "" : ""); return $"Command finished with exit code {exit}."; }
                case "windows_capture_screen": return SaveScreen();
                case "camera_open_windows_app": return await CameraTools.ExecuteAsync(toolName, input);
                case "camera_list_devices": return await CameraTools.ExecuteAsync("camera_list", input);
                case "camera_check_permission": return await CameraTools.ExecuteAsync("camera_permission", input);
                case "camera_status": return await CameraTools.ExecuteAsync("camera_status", input);
                case "camera_start_preview": return await CameraTools.ExecuteAsync("camera_start_preview", input);
                case "camera_stop_preview": return await CameraTools.ExecuteAsync("camera_stop_preview", input);
                case "camera_capture_photo": return await CameraTools.ExecuteAsync("camera_capture_photo", input);
                case "mcp_list_servers": return App.Settings.WindowsMcpEnabled ? (App.Mcp.Servers.Count == 0 ? "No MCP servers connected." : string.Join("\n", App.Mcp.Servers.Select(s => $"{s.Name} — {s.Command}"))) : "MCP permission is disabled in Aurora Settings.";
                case "mcp_list_tools": return await ListMcpToolsAsync(input.GetProperty("server").GetString() ?? "");
                case "mcp_call_tool": return await App.Mcp.CallToolAsync(input.GetProperty("server").GetString() ?? "", input.GetProperty("tool").GetString() ?? "", input.TryGetProperty("arguments", out var args) ? args : default);
                default: return $"Unknown tool: {toolName}";
            }
        }

        private static string? GetApp(JsonElement input) => input.TryGetProperty("app", out var a) ? a.GetString() : null;
        private static string FormatProcesses(IReadOnlyList<ProcessInfo> xs) => xs.Count == 0 ? "No running applications found." : string.Join("\n", xs.Select(x => $"{x.Name} (PID {x.Id}) — {x.WindowTitle}"));
        private static string SaveScreen()
        {
            var image = App.WindowsAutomation.CaptureScreen(); var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aurora-screen-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            using var stream = System.IO.File.Create(path); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image)); encoder.Save(stream); return $"Screenshot saved: {path}";
        }
        private static async Task<string> ListMcpToolsAsync(string serverName)
        {
            if (!App.Settings.WindowsMcpEnabled) return "MCP permission is disabled in Aurora Settings.";
            var server = App.Mcp.Servers.FirstOrDefault(s => string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));
            if (server == null) return $"MCP server '{serverName}' is not connected.";
            var tools = await App.Mcp.DiscoverToolsAsync(server); return tools.Count == 0 ? "No tools exposed by that server." : string.Join("\n", tools.Select(t => $"{t.Name}: {t.Description}"));
        }
    }
}