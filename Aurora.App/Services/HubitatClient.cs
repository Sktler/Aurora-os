using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    public record HubitatDevice(
        string DeviceId,
        string Label,
        string Type,
        string State,
        string Room,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string> SupportedActions,
        string RawMetadata);

    /// <summary>
    /// Talks to Hubitat's Maker API. The base URL should point at a specific Maker API
    /// app, e.g. http://hubitat.local/apps/api/1234, with the access token stored separately.
    /// </summary>
    public class HubitatClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _token;
        private readonly bool _configured;

        public HubitatClient(string baseUrl, string token)
        {
            _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
            _token = (token ?? "").Trim();
            _configured = !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_token);
            _http = new HttpClient();
        }

        public bool IsConfigured => _configured;

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (!_configured) return (false, "Maker API URL and access token are both required.");

            try
            {
                var devices = await ListDevicesAsync();
                return (true, $"Connected - found {devices.Count} device(s).");
            }
            catch (Exception ex)
            {
                return (false, $"Couldn't reach Hubitat: {ex.Message}");
            }
        }

        public async Task<List<HubitatDevice>> ListDevicesAsync()
        {
            var result = new List<HubitatDevice>();
            if (!_configured) return result;

            var response = await _http.GetAsync(BuildUrl("devices/all"));
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Hubitat rejected the request ({(int)response.StatusCode}): {Truncate(text)}");

            return ParseDevicesPayload(text);
        }

        public async Task<bool> SendCommandAsync(string deviceId, string command, params string[] args)
        {
            if (!_configured) return false;
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(command)) return false;

            var segments = new List<string> { "devices", Uri.EscapeDataString(deviceId), Uri.EscapeDataString(command) };
            segments.AddRange(args.Where(static a => !string.IsNullOrWhiteSpace(a)).Select(Uri.EscapeDataString));
            var response = await _http.GetAsync(BuildUrl(string.Join("/", segments)));
            return response.IsSuccessStatusCode;
        }

        public static List<HubitatDevice> ParseDevicesPayload(string payload)
        {
            var result = new List<HubitatDevice>();
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var device in doc.RootElement.EnumerateArray())
            {
                var deviceId = GetString(device, "id");
                var label = FirstNonEmpty(
                    GetString(device, "label"),
                    GetString(device, "name"),
                    deviceId);
                var type = FirstNonEmpty(
                    GetString(device, "type"),
                    GetString(device, "deviceTypeName"),
                    "device");
                var room = FirstNonEmpty(
                    GetString(device, "roomName"),
                    GetString(device, "room"),
                    "");
                var capabilities = ParseCapabilities(device);
                var commands = ParseCommands(device);
                var supportedActions = commands.Count > 0 ? commands : InferActionsFromCapabilities(capabilities);
                var state = ParsePrimaryState(device);

                result.Add(new HubitatDevice(
                    deviceId,
                    label,
                    type,
                    state,
                    room,
                    capabilities,
                    supportedActions,
                    device.GetRawText()));
            }

            return result;
        }

        private string BuildUrl(string relativePath)
        {
            var separator = relativePath.Contains('?') ? "&" : "?";
            return $"{_baseUrl}/{relativePath}{separator}access_token={Uri.EscapeDataString(_token)}";
        }

        private static string Truncate(string s) => s.Length > 200 ? s[..200] + "…" : s;

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
            var capabilities = new List<string>();
            if (!device.TryGetProperty("capabilities", out var caps) || caps.ValueKind != JsonValueKind.Array)
                return capabilities;

            foreach (var capability in caps.EnumerateArray())
            {
                var name = capability.ValueKind switch
                {
                    JsonValueKind.String => capability.GetString() ?? "",
                    JsonValueKind.Object => FirstNonEmpty(GetString(capability, "name"), GetString(capability, "id")),
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(name))
                    capabilities.Add(name);
            }

            return capabilities.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static x => x).ToList();
        }

        private static List<string> ParseCommands(JsonElement device)
        {
            var commands = new List<string>();
            if (!device.TryGetProperty("commands", out var commandArray) || commandArray.ValueKind != JsonValueKind.Array)
                return commands;

            foreach (var command in commandArray.EnumerateArray())
            {
                var name = command.ValueKind switch
                {
                    JsonValueKind.String => command.GetString() ?? "",
                    JsonValueKind.Object => FirstNonEmpty(GetString(command, "command"), GetString(command, "name")),
                    _ => ""
                };
                if (!string.IsNullOrWhiteSpace(name))
                    commands.Add(name);
            }

            return commands.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static x => x).ToList();
        }

        private static string ParsePrimaryState(JsonElement device)
        {
            if (!device.TryGetProperty("attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Array)
                return "";

            var attributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in attributes.EnumerateArray())
            {
                var name = FirstNonEmpty(GetString(attribute, "name"), GetString(attribute, "key"));
                var value = FirstNonEmpty(GetString(attribute, "currentValue"), GetString(attribute, "value"));
                if (!string.IsNullOrWhiteSpace(name) && !attributeMap.ContainsKey(name))
                    attributeMap[name] = value;
            }

            foreach (var preferred in new[] { "switch", "lock", "contact", "motion", "presence", "door", "windowShade", "thermostatMode", "temperature", "humidity" })
            {
                if (attributeMap.TryGetValue(preferred, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return attributeMap.Values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static List<string> InferActionsFromCapabilities(IReadOnlyList<string> capabilities)
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
                        actions.Add("setLevel");
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
