using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>
    /// Looks up instant answers and provides search results links. Opening a results
    /// page in the user's browser is a separate, approval-gated action.
    /// </summary>
    public class WebSearchClient
    {
        internal const string OpenBrowserTool = "web_search_open_browser";
        private readonly HttpClient _http;

        public WebSearchClient() : this(new HttpClient()) { }

        public WebSearchClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return "No search query given.";

            return await TryInstantAnswerAsync(query)
                ?? $"No instant answer found. Full search results: {GetResultsUrl(query)}";
        }

        /// <summary>Tries DuckDuckGo's Instant Answer API for a quick factual snippet.
        /// Returns null if the lookup is unavailable or has no answer.</summary>
        public async Task<string?> TryInstantAnswerAsync(string query)
        {
            if (App.IsLocalOnlyModeEnabled)
                return App.LocalOnlyModeBlockedMessage("web search");

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
                return null;
            }
        }

        /// <summary>Opens full results in the user's default browser after approval.</summary>
        public string OpenSearchInBrowser(string query)
        {
            if (App.IsLocalOnlyModeEnabled)
                return App.LocalOnlyModeBlockedMessage("web search");

            if (string.IsNullOrWhiteSpace(query)) return "No search query given.";

            var url = GetResultsUrl(query);
            if (!ActionApprovalCenter.ConfirmAction(OpenBrowserTool, $"Open Google search results in your browser for \"{query}\".", requireOneTimeApproval: true))
                return $"Browser opening was not approved. Full search results: {url}";

            try
            {
                using var process = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return $"Opened a browser search for \"{query}\".";
            }
            catch (Exception ex)
            {
                return $"Couldn't open the browser: {ex.Message}";
            }
        }

        private static string GetResultsUrl(string query) => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
    }
}
