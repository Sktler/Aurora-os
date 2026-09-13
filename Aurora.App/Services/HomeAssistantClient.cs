using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    public record HomeAssistantDevice(
        string EntityId,
        string FriendlyName,
        string State,
        string Domain,
        string Room,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string> SupportedActions,
        string RawMetadata);

    /// <summary>
    /// Talks to a Home Assistant instance's REST API. Needs the base URL
    /// (e.g. http://homeassistant.local:8123) and a long-lived access token,
    /// generated from the user's HA profile page. Once connected, every entity
    /// HA already knows about shows up automatically - no per-device setup here.
    /// </summary>
    public class HomeAssistantClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly bool _configured;

        public HomeAssistantClient(string baseUrl, string token)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _configured = !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(token);
            _http = new HttpClient();
            if (_configured)
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public bool IsConfigured => _configured;

        /// <summary>Actually calls the API and reports whether the URL/token really work,
        /// instead of just checking that both boxes were filled in.</summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (!_configured) return (false, "URL and token are both required.");

            try
            {
                var response = await _http.GetAsync($"{_baseUrl}/api/states");
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, $"Home Assistant rejected the request ({(int)response.StatusCode}): {Truncate(text)}");

                using var doc = JsonDocument.Parse(text);
                var count = doc.RootElement.GetArrayLength();
                return (true, $"Connected - found {count} entities.");
            }
            catch (Exception ex)
            {
                return (false, $"Couldn't reach {_baseUrl}: {ex.Message}");
            }
        }

        private static string Truncate(string s) => s.Length > 200 ? s[..200] + "…" : s;

        public async Task<List<HomeAssistantDevice>> ListDevicesAsync()
        {
            var result = new List<HomeAssistantDevice>();
            if (!_configured) return result;

            var response = await _http.GetAsync($"{_baseUrl}/api/states");
            if (!response.IsSuccessStatusCode) return result;

            var text = await response.Content.ReadAsStringAsync();
            return ParseStatesPayload(text);
        }

        /// <summary>Calls a Home Assistant service, e.g. domain "light", service "turn_on".</summary>
        public async Task<bool> CallServiceAsync(string domain, string service, object body)
        {
            if (!_configured) return false;

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{_baseUrl}/api/services/{domain}/{service}", content);
            return response.IsSuccessStatusCode;
        }

        public static List<HomeAssistantDevice> ParseStatesPayload(string payload)
        {
            var result = new List<HomeAssistantDevice>();
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entity in doc.RootElement.EnumerateArray())
            {
                var entityId = GetString(entity, "entity_id");
                var state = GetString(entity, "state");
                var domain = entityId.Contains('.') ? entityId.Split('.')[0] : "entity";
                var friendly = entityId;
                var room = "";
                var capabilities = new List<string> { domain };
                var actions = InferSupportedActions(domain, entity);

                if (entity.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                {
                    if (attrs.TryGetProperty("friendly_name", out var fn))
                        friendly = fn.GetString() ?? entityId;
                    room = FirstNonEmpty(GetAttributeString(attrs, "room"), GetAttributeString(attrs, "area"), GetAttributeString(attrs, "area_id"));
                    capabilities.AddRange(ParseCapabilities(attrs));
                }

                result.Add(new HomeAssistantDevice(
                    entityId,
                    friendly,
                    state,
                    domain,
                    room,
                    capabilities.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static x => x).ToList(),
                    actions,
                    entity.GetRawText()));
            }

            return result;
        }

        public static bool TryResolveService(
            string entityId,
            IReadOnlyList<string> capabilities,
            string action,
            string? value,
            out string domain,
            out string service,
            out Dictionary<string, object> serviceData,
            out string error)
        {
            domain = entityId.Contains('.') ? entityId.Split('.')[0] : "homeassistant";
            service = "";
            serviceData = new Dictionary<string, object> { ["entity_id"] = entityId };
            error = "";

            var normalized = (action ?? "").Trim().ToLowerInvariant();
            var caps = new HashSet<string>(capabilities ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            switch (normalized)
            {
                case "on":
                case "turn_on":
                    service = "turn_on";
                    return true;

                case "off":
                case "turn_off":
                    service = "turn_off";
                    return true;

                case "open":
                case "open_cover":
                    if (domain != "cover")
                    {
                        error = "Open is only supported for Home Assistant cover entities.";
                        return false;
                    }
                    service = "open_cover";
                    return true;

                case "close":
                case "close_cover":
                    if (domain != "cover")
                    {
                        error = "Close is only supported for Home Assistant cover entities.";
                        return false;
                    }
                    service = "close_cover";
                    return true;

                case "stop":
                case "stop_cover":
                    if (domain != "cover")
                    {
                        error = "Stop is only supported for Home Assistant cover entities.";
                        return false;
                    }
                    service = "stop_cover";
                    return true;

                case "lock":
                case "unlock":
                    if (domain != "lock")
                    {
                        error = "Lock actions are only supported for Home Assistant lock entities.";
                        return false;
                    }
                    service = normalized;
                    return true;

                case "press":
                    if (domain is not ("button" or "input_button"))
                    {
                        error = "Press is only supported for Home Assistant button entities.";
                        return false;
                    }
                    service = "press";
                    return true;

                case "trigger":
                    if (domain != "automation")
                    {
                        error = "Trigger is only supported for Home Assistant automations.";
                        return false;
                    }
                    service = "trigger";
                    return true;

                case "activate":
                    if (domain != "scene")
                    {
                        error = "Activate is only supported for Home Assistant scenes.";
                        return false;
                    }
                    service = "turn_on";
                    return true;

                case "set_level":
                case "setlevel":
                case "level":
                    if (!caps.Contains("brightness"))
                    {
                        error = "That Home Assistant entity doesn't report brightness support.";
                        return false;
                    }
                    if (!int.TryParse(value, out var level))
                    {
                        error = "Level actions need a numeric value.";
                        return false;
                    }
                    service = "turn_on";
                    serviceData["brightness_pct"] = Math.Clamp(level, 0, 100);
                    return true;

                case "set_position":
                case "position":
                    if (!caps.Contains("position"))
                    {
                        error = "That Home Assistant entity doesn't report position support.";
                        return false;
                    }
                    if (!int.TryParse(value, out var position))
                    {
                        error = "Position actions need a numeric value.";
                        return false;
                    }
                    service = "set_cover_position";
                    serviceData["position"] = Math.Clamp(position, 0, 100);
                    return true;

                case "set_temperature":
                case "temperature":
                    if (!caps.Contains("temperature"))
                    {
                        error = "That Home Assistant entity doesn't report temperature support.";
                        return false;
                    }
                    if (!double.TryParse(value, out var temperature))
                    {
                        error = "Temperature actions need a numeric value.";
                        return false;
                    }
                    service = "set_temperature";
                    serviceData["temperature"] = temperature;
                    return true;

                case "set_volume":
                case "volume":
                    if (!caps.Contains("volume"))
                    {
                        error = "That Home Assistant entity doesn't report volume support.";
                        return false;
                    }
                    if (!double.TryParse(value, out var volume))
                    {
                        error = "Volume actions need a numeric value.";
                        return false;
                    }
                    service = "volume_set";
                    serviceData["volume_level"] = volume > 1 ? Math.Clamp(volume / 100d, 0, 1) : Math.Clamp(volume, 0, 1);
                    return true;
            }

            error = $"Unsupported Home Assistant action: {action}";
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

        private static string GetAttributeString(JsonElement attributes, string propertyName)
        {
            if (!attributes.TryGetProperty(propertyName, out var value))
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

        private static List<string> ParseCapabilities(JsonElement attributes)
        {
            var capabilities = new List<string>();

            if (attributes.TryGetProperty("brightness", out _)) capabilities.Add("brightness");
            if (attributes.TryGetProperty("current_position", out _) || attributes.TryGetProperty("position", out _)) capabilities.Add("position");
            if (attributes.TryGetProperty("temperature", out _) || attributes.TryGetProperty("target_temp_high", out _) || attributes.TryGetProperty("target_temp_low", out _)) capabilities.Add("temperature");
            if (attributes.TryGetProperty("volume_level", out _)) capabilities.Add("volume");
            if (attributes.TryGetProperty("supported_color_modes", out _)) capabilities.Add("color");
            if (attributes.TryGetProperty("effect_list", out _)) capabilities.Add("effects");
            if (attributes.TryGetProperty("hvac_modes", out _)) capabilities.Add("climate");

            return capabilities;
        }

        private static List<string> InferSupportedActions(string domain, JsonElement entity)
        {
            var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            switch ((domain ?? "").Trim().ToLowerInvariant())
            {
                case "light":
                case "switch":
                case "fan":
                case "media_player":
                case "input_boolean":
                    actions.Add("on");
                    actions.Add("off");
                    break;
                case "automation":
                    actions.Add("on");
                    actions.Add("off");
                    actions.Add("trigger");
                    break;
                case "cover":
                    actions.Add("open");
                    actions.Add("close");
                    actions.Add("stop");
                    break;
                case "lock":
                    actions.Add("lock");
                    actions.Add("unlock");
                    break;
                case "scene":
                    actions.Add("activate");
                    break;
                case "script":
                    actions.Add("on");
                    break;
                case "button":
                case "input_button":
                    actions.Add("press");
                    break;
            }

            if (entity.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            {
                if (attrs.TryGetProperty("brightness", out _)) actions.Add("set_level");
                if (attrs.TryGetProperty("current_position", out _) || attrs.TryGetProperty("position", out _)) actions.Add("set_position");
                if (attrs.TryGetProperty("temperature", out _) || attrs.TryGetProperty("target_temp_high", out _) || attrs.TryGetProperty("target_temp_low", out _)) actions.Add("set_temperature");
                if (attrs.TryGetProperty("volume_level", out _)) actions.Add("set_volume");
            }

            return actions.OrderBy(static x => x).ToList();
        }
    }
}
