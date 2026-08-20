using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public record SmartThingsDevice(string DeviceId, string Label, string Type);

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
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var d in items.EnumerateArray())
                {
                    result.Add(new SmartThingsDevice(
                        d.GetProperty("deviceId").GetString() ?? "",
                        d.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "",
                        d.TryGetProperty("type", out var t) ? t.GetString() ?? "" : ""
                    ));
                }
            }
            return result;
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
    }
}
