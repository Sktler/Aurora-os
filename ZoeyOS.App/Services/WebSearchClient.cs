using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Web search kept genuinely free: a quick instant-answer lookup via DuckDuckGo's
    /// free API (no key, no account) for simple factual queries the model can read
    /// directly, plus a fallback that just opens full results in the user's browser -
    /// no paid search API, no rate-limited quota to manage.
    /// </summary>
    public class WebSearchClient
    {
        private readonly HttpClient _http = new();

        /// <summary>Tries DuckDuckGo's Instant Answer API for a quick factual snippet.
        /// Returns null if it doesn't have a good answer (common for anything beyond
        /// simple facts/definitions) - callers should fall back to OpenSearchInBrowser.</summary>
        public async Task<string?> TryInstantAnswerAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            try
            {
                var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
                var text = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                var abstractText = root.TryGetProperty("AbstractText", out var a) ? a.GetString() : null;
                if (!string.IsNullOrWhiteSpace(abstractText))
                {
                    var source = root.TryGetProperty("AbstractSource", out var s) ? s.GetString() : null;
                    return string.IsNullOrEmpty(source) ? abstractText : $"{abstractText} (via {source})";
                }

                var answer = root.TryGetProperty("Answer", out var ans) ? ans.GetString() : null;
                if (!string.IsNullOrWhiteSpace(answer)) return answer;

                return null;
            }
            catch
            {
                return null; // Non-fatal - caller falls back to opening a browser search.
            }
        }

        /// <summary>Opens a full search results page in the user's default browser.
        /// Always works, never costs anything, no rate limit.</summary>
        public string OpenSearchInBrowser(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return "No search query given.";

            try
            {
                var url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return $"Opened a browser search for \"{query}\".";
            }
            catch (Exception ex)
            {
                return $"Couldn't open the browser: {ex.Message}";
            }
        }
    }
}
