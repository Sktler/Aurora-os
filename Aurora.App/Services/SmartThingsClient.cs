using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    public record SmartThingsDevice(
        string DeviceId,
        string Label,
        string Type,
        string State,
        string Room,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string> SupportedActions,
        string RawMetadata);

    /// <summary>
    /// Talks to the SmartThings Cloud REST API. Generate a Personal Access Token
    /// at https://account.smartthings.com/tokens and drop it in Settings.
    /// The home automation companion calls these methods; Claude decides which
    /// device/command to invoke based on your request, this class just executes it.
    /// Alexa routines can be layered in later via the Alexa Smart Home Skill API
    /// once this SmartThings path is working end to end.
    /// </summary>
    public class SmartThingsClient
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://api.smartthings.com/v1";
        private readonly bool _configured;

        public SmartThingsClient(string token)
        {
            _http = new HttpClient();
            _configured = !string.IsNullOrWhiteSpace(token);
            if (_configured)
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public bool IsConfigured => _configured;

        /// <summary>Actually calls the API and reports whether the token really works,
        /// instead of just checking that a string was typed into the box.</summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (!_configured) return (false, "No token set.");

            try
            {
                var response = await _http.GetAsync($"{BaseUrl}/devices");
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, $"SmartThings rejected the token ({(int)response.StatusCode}): {Truncate(text)}");

                using var doc = JsonDocument.Parse(text);
                var count = doc.RootElement.TryGetProperty("items", out var items) ? items.GetArrayLength() : 0;
                return (true, $"Connected - found {count} device(s).");
            }
            catch (Exception ex)
            {
                return (false, $"Couldn't reach SmartThings: {ex.Message}");
            }
        }

        private static string Truncate(string s) => s.Length > 200 ? s[..200] + "…" : s;

        public async Task<List<SmartThingsDevice>> ListDevicesAsync()
        {
            var result = new List<SmartThingsDevice>();
            if (!_configured) return result;

            var response = await _http.GetAsync($"{BaseUrl}/devices");
            if (!response.IsSuccessStatusCode) return result;

            var text = await response.Content.ReadAsStringAsync();
            return ParseDevicesPayload(text);
        }

        /// <summary>Sends a single-capability command, e.g. component "main", capability "switch", command "on".</summary>
        public async Task<bool> SendCommandAsync(string deviceId, string capability, string command, object[]? args = null)
        {
            if (!_configured) return false;

            var body = new
            {
                commands = new[]
                {
                    new
                    {
                        component = "main",
                        capability,
                        command,
                        arguments = args ?? Array.Empty<object>()
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{BaseUrl}/devices/{deviceId}/commands", content);
            return response.IsSuccessStatusCode;
        }

        public static List<SmartThingsDevice> ParseDevicesPayload(string payload)
        {
            var result = new List<SmartThingsDevice>();
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var device in items.EnumerateArray())
            {
                var deviceId = GetString(device, "deviceId");
                var label = FirstNonEmpty(GetString(device, "label"), GetString(device, "name"), deviceId);
                var type = FirstNonEmpty(GetString(device, "deviceTypeName"), GetString(device, "type"), "device");
                var room = FirstNonEmpty(GetString(device, "roomName"), GetString(device, "locationName"), "");
                var capabilities = ParseCapabilities(device);
                var actions = InferSupportedActions(capabilities);

                result.Add(new SmartThingsDevice(
                    deviceId,
                    label,
                    type,
                    "",
                    room,
                    capabilities,
                    actions,
                    device.GetRawText()));
            }

            return result;
        }

        public static bool TryResolveCommand(
            IReadOnlyList<string> capabilities,
            string action,
            string? value,
            out string capability,
            out string command,
            out object[] args,
            out string error)
        {
            capability = "";
            command = "";
            args = Array.Empty<object>();
            error = "";

            var normalized = (action ?? "").Trim().ToLowerInvariant();
            var caps = new HashSet<string>(capabilities ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            switch (normalized)
            {
                case "on":
                case "off":
                    if (!caps.Contains("switch"))
                    {
                        error = "That SmartThings device doesn't report switch support.";
                        return false;
                    }

                    capability = "switch";
                    command = normalized;
                    return true;

                case "lock":
                case "unlock":
                    if (!caps.Contains("lock"))
                    {
                        error = "That SmartThings device doesn't report lock support.";
                        return false;
                    }

                    capability = "lock";
                    command = normalized;
                    return true;

                case "open":
                case "close":
                    if (caps.Contains("doorControl"))
                    {
                        capability = "doorControl";
                        command = normalized;
                        return true;
                    }
                    if (caps.Contains("windowShade"))
                    {
                        capability = "windowShade";
                        command = normalized;
                        return true;
                    }

                    error = "That SmartThings device doesn't report open/close support.";
                    return false;

                case "stop":
                case "pause":
                    if (!caps.Contains("windowShade"))
                    {
                        error = "That SmartThings device doesn't report pause/stop support.";
                        return false;
                    }

                    capability = "windowShade";
                    command = "pause";
                    return true;

                case "set_level":
                case "setlevel":
                case "level":
                    if (!caps.Contains("switchLevel"))
                    {
                        error = "That SmartThings device doesn't report level control.";
                        return false;
                    }
                    if (!int.TryParse(value, out var level))
                    {
                        error = "Level actions need a numeric value.";
                        return false;
                    }

                    capability = "switchLevel";
                    command = "setLevel";
                    args = new object[] { Math.Clamp(level, 0, 100) };
                    return true;
            }

            error = $"Unsupported SmartThings action: {action}";
            return false;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return "";

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }

        private static string FirstNonEmpty(params string[] values) =>
            values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v)) ?? "";

        private static List<string> ParseCapabilities(JsonElement device)
        {
            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!device.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
                return capabilities.OrderBy(static x => x).ToList();

            foreach (var component in components.EnumerateArray())
            {
                if (!component.TryGetProperty("capabilities", out var caps) || caps.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var cap in caps.EnumerateArray())
                {
                    var id = GetString(cap, "id");
                    if (!string.IsNullOrWhiteSpace(id))
                        capabilities.Add(id);
                }
            }

            return capabilities.OrderBy(static x => x).ToList();
        }

        private static List<string> InferSupportedActions(IReadOnlyList<string> capabilities)
        {
            var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var capability in capabilities)
            {
                switch ((capability ?? "").Trim().ToLowerInvariant())
                {
                    case "switch":
                        actions.Add("on");
                        actions.Add("off");
                        break;
                    case "switchlevel":
                        actions.Add("set_level");
                        break;
                    case "lock":
                        actions.Add("lock");
                        actions.Add("unlock");
                        break;
                    case "doorcontrol":
                    case "windowshade":
                        actions.Add("open");
                        actions.Add("close");
                        actions.Add("stop");
                        break;
                }
            }

            return actions.OrderBy(static x => x).ToList();
        }
    }
}
