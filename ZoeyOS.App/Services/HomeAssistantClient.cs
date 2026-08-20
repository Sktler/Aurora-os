using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public record HomeAssistantDevice(string EntityId, string FriendlyName, string State);

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
            using var doc = JsonDocument.Parse(text);
            foreach (var entity in doc.RootElement.EnumerateArray())
            {
                var entityId = entity.GetProperty("entity_id").GetString() ?? "";
                var state = entity.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
                var friendly = entityId;
                if (entity.TryGetProperty("attributes", out var attrs) &&
                    attrs.TryGetProperty("friendly_name", out var fn))
                    friendly = fn.GetString() ?? entityId;

                result.Add(new HomeAssistantDevice(entityId, friendly, state));
            }
            return result;
        }

        /// <summary>Calls a Home Assistant service, e.g. domain "light", service "turn_on".</summary>
        public async Task<bool> CallServiceAsync(string domain, string service, string entityId)
        {
            if (!_configured) return false;

            var body = new { entity_id = entityId };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{_baseUrl}/api/services/{domain}/{service}", content);
            return response.IsSuccessStatusCode;
        }
    }
}
