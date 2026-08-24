using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
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
        private readonly string _apiKey;
        private readonly bool _configured;

        // Google retired the older Imagen image models on August 17, 2026.
        // Use the current Gemini image-generation model instead.
        private const string GeminiImageModel = "gemini-3.1-flash-image";
        private const string GeminiEndpointBase = "https://generativelanguage.googleapis.com/v1beta/models/";

        public ImageGenClient(string apiKey, string provider)
        {
            _http = new HttpClient();
            _provider = string.IsNullOrWhiteSpace(provider) ? "gemini" : provider;
            _apiKey = apiKey ?? "";
            _configured = !string.IsNullOrWhiteSpace(_apiKey);

            if (_configured && _provider == "openai")
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public bool IsConfigured => _configured;

        public async Task<string> GenerateImageAsync(string prompt)
        {
            if (!_configured)
                return "[No image generation API key set. Add your Gemini key in Settings to enable Google image generation.]";

            return _provider switch
            {
                "openai" => await GenerateWithOpenAiAsync(prompt),
                _ => await GenerateWithGeminiAsync(prompt)
            };
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
                var response = await _http.PostAsync(endpoint, content);
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[Google image generation error {(int)response.StatusCode}: {text}]";

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
                var response = await _http.PostAsync("https://api.openai.com/v1/images/generations", content);
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[Image gen error {(int)response.StatusCode}: {text}]";

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
