using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>
    /// Calls out to an image generation provider alongside the chat engine.
    /// Google Gemini image generation uses the current Nano Banana image model
    /// and shares the Gemini API key with the chat engine.
    /// </summary>
    public class ImageGenClient
    {
        private readonly HttpClient _http;
        private readonly string _provider;
        private string _apiKey;
        private readonly bool _configured;
        private int _credentialsRevoked;

        // Google retired the older Imagen image models on August 17, 2026.
        // Use the current Gemini image-generation model instead.
        private const string GeminiImageModel = "gemini-3.1-flash-image";
        private const string GeminiEndpointBase = "https://generativelanguage.googleapis.com/v1beta/models/";

        private const int MaxAttempts = 4;
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
        private readonly Func<TimeSpan, Task> _delay;

        public ImageGenClient(string apiKey, string provider)
            : this(apiKey, provider, new HttpClient(), null)
        {
        }

        public ImageGenClient(string apiKey, string provider, HttpClient http, Func<TimeSpan, Task>? delay)
        {
            _http = http;
            _delay = delay ?? (t => Task.Delay(t));
            _provider = string.IsNullOrWhiteSpace(provider) ? "gemini" : provider;
            _apiKey = apiKey ?? "";
            _configured = !string.IsNullOrWhiteSpace(_apiKey);

            if (_configured && _provider == "openai")
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public bool IsConfigured => _configured && Volatile.Read(ref _credentialsRevoked) == 0;

        public void RevokeCredentials()
        {
            if (Interlocked.Exchange(ref _credentialsRevoked, 1) != 0) return;
            Interlocked.Exchange(ref _apiKey, "");
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.CancelPendingRequests();
        }

        public async Task<string> GenerateImageAsync(string prompt)
        {
            if (App.IsLocalOnlyModeEnabled)
                return App.LocalOnlyModeBlockedMessage("image generation");

            if (!IsConfigured)
                return "[No image generation API key set. Add your Gemini key in Settings to enable Google image generation.]";

            return _provider switch
            {
                "openai" => await GenerateWithOpenAiAsync(prompt),
                _ => await GenerateWithGeminiAsync(prompt)
            };
        }

        private async Task<HttpResponseMessage> PostWithRetryAsync(string url, string json)
        {
            for (var attempt = 1; ; attempt++)
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content);
                if ((int)response.StatusCode != 429 || attempt >= MaxAttempts)
                    return response;

                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
                    ?? TimeSpan.FromSeconds(2 * Math.Pow(2, attempt - 1));
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                if (delay > MaxRetryDelay) delay = MaxRetryDelay;
                response.Dispose();
                await _delay(delay);
            }
        }

        private static string FormatError(string service, HttpResponseMessage response, string body)
        {
            if ((int)response.StatusCode == 429)
                return $"[{service} is rate limited (HTTP 429 / RESOURCE_EXHAUSTED) even after retrying. Wait a minute and try again, or check your provider quota and billing.]";
            return $"[{service} error {(int)response.StatusCode}: {body}]";
        }

        private async Task<string> GenerateWithGeminiAsync(string prompt)
        {
            var body = new
            {
                contents = new object[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var endpoint = $"{GeminiEndpointBase}{GeminiImageModel}:generateContent?key={_apiKey}";

            try
            {
                using var response = await PostWithRetryAsync(endpoint, json);
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return FormatError("Google image generation", response, text);

                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("candidates", out var candidates))
                    return "[Google returned no image.]";

                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (!candidate.TryGetProperty("content", out var contentEl)) continue;
                    if (!contentEl.TryGetProperty("parts", out var parts)) continue;

                    foreach (var part in parts.EnumerateArray())
                    {
                        if (!part.TryGetProperty("inlineData", out var inlineData)) continue;

                        var mimeType = inlineData.TryGetProperty("mimeType", out var mt) ? mt.GetString() : "image/png";
                        var data = inlineData.TryGetProperty("data", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrEmpty(data))
                            return $"data:{mimeType};base64,{data}";
                    }
                }

                return "[Google returned no image data.]";
            }
            catch (Exception ex)
            {
                return $"[Connection error reaching Google image generation: {ex.Message}]";
            }
        }

        private async Task<string> GenerateWithOpenAiAsync(string prompt)
        {
            var body = new
            {
                model = "gpt-image-1",
                prompt,
                size = "1024x1024"
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var response = await PostWithRetryAsync("https://api.openai.com/v1/images/generations", json);
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return FormatError("Image generation", response, text);

                using var doc = JsonDocument.Parse(text);
                var url = doc.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
                return url ?? "[No image URL returned.]";
            }
            catch (Exception ex)
            {
                return $"[Connection error reaching image provider: {ex.Message}]";
            }
        }
    }
}
