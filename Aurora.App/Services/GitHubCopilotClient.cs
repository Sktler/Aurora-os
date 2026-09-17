using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    /// <summary>
    /// Thin wrapper around GitHub Copilot's chat completions API. It speaks the same
    /// OpenAI-shaped JSON payload as the other hosted chat providers, but authentication
    /// is not a simple bearer key: api.githubcopilot.com only accepts a short-lived
    /// Copilot session token, not a raw GitHub personal access token. That session token
    /// is obtained by exchanging the configured GitHub token against GitHub's internal
    /// (undocumented) token endpoint, the same mechanism official editor extensions use.
    /// This is unofficial and unsupported - GitHub can change or block it at any time,
    /// and it only works for accounts with an active Copilot subscription/entitlement.
    /// </summary>
    public class GitHubCopilotClient : IChatEngine
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _apiKey; // GitHub token supplied by the user; exchanged for a session token below.
        private const string Endpoint = "https://api.githubcopilot.com/chat/completions";
        private const string ModelsEndpoint = "https://api.githubcopilot.com/models";
        private const string TokenExchangeEndpoint = "https://api.github.com/copilot_internal/v2/token";

        // Spoofed client identification - api.githubcopilot.com and the token-exchange
        // endpoint both reject requests that don't look like they came from a real editor.
        private const string EditorVersion = "vscode/1.85.1";
        private const string EditorPluginVersion = "copilot-chat/0.11.1";
        private const string CopilotUserAgent = "GithubCopilot/1.155.0";
        private const string IntegrationId = "vscode-chat";

        private readonly SemaphoreSlim _tokenLock = new(1, 1);
        private string? _sessionToken;
        private DateTimeOffset _sessionTokenExpiresAt = DateTimeOffset.MinValue;

        public GitHubCopilotClient(string apiKey, string model)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;
            _http = new HttpClient();
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>Exchanges the configured GitHub token for a short-lived Copilot session
        /// token (valid ~25 minutes), refreshing a minute before expiry. Throws if the
        /// exchange fails, e.g. the token lacks Copilot access.</summary>
        private async Task<string> GetSessionTokenAsync()
        {
            if (_sessionToken != null && DateTimeOffset.UtcNow < _sessionTokenExpiresAt.AddMinutes(-1))
                return _sessionToken;

            await _tokenLock.WaitAsync();
            try
            {
                if (_sessionToken != null && DateTimeOffset.UtcNow < _sessionTokenExpiresAt.AddMinutes(-1))
                    return _sessionToken;

                using var request = new HttpRequestMessage(HttpMethod.Get, TokenExchangeEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("token", _apiKey);
                request.Headers.TryAddWithoutValidation("Editor-Version", EditorVersion);
                request.Headers.TryAddWithoutValidation("Editor-Plugin-Version", EditorPluginVersion);
                request.Headers.UserAgent.ParseAdd(CopilotUserAgent);

                var response = await _http.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"GitHub Copilot token exchange failed ({(int)response.StatusCode}): {text}. This account may not have an active Copilot subscription.");

                using var doc = JsonDocument.Parse(text);
                var token = doc.RootElement.GetProperty("token").GetString()
                    ?? throw new InvalidOperationException("GitHub Copilot token exchange response had no token.");
                var expiresAtUnix = doc.RootElement.TryGetProperty("expires_at", out var expEl) && expEl.TryGetInt64(out var exp)
                    ? exp
                    : DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();

                _sessionToken = token;
                _sessionTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
                return _sessionToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        /// <summary>Builds a request against api.githubcopilot.com carrying a fresh session
        /// token and the editor-identification headers it requires.</summary>
        private async Task<HttpRequestMessage> BuildCopilotRequestAsync(HttpMethod method, string url, HttpContent? content = null)
        {
            var sessionToken = await GetSessionTokenAsync();
            var request = new HttpRequestMessage(method, url) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            request.Headers.TryAddWithoutValidation("Editor-Version", EditorVersion);
            request.Headers.TryAddWithoutValidation("Editor-Plugin-Version", EditorPluginVersion);
            request.Headers.TryAddWithoutValidation("Copilot-Integration-Id", IntegrationId);
            request.Headers.TryAddWithoutValidation("OpenAI-Intent", "conversation-panel");
            request.Headers.UserAgent.ParseAdd(CopilotUserAgent);
            return request;
        }

        public async Task<List<string>> ListModelsAsync()
        {
            if (!IsConfigured) return new List<string>();

            using var request = await BuildCopilotRequestAsync(HttpMethod.Get, ModelsEndpoint);
            var response = await _http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"GitHub Copilot returned {(int)response.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var ids = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var m in data.EnumerateArray())
                    if (m.TryGetProperty("id", out var idEl))
                        ids.Add(idEl.GetString() ?? "");
            }
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        private static List<object> BuildMessages(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
        {
            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var m in history)
                messages.Add(new { role = m.Role, content = m.Content });
            messages.Add(new { role = "user", content = newUserMessage });
            return messages;
        }

        public async Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
        {
            if (!IsConfigured)
                return "[No GitHub Copilot token set. Add one in Settings to bring this companion online.]";

            var body = new { model = _model, messages = BuildMessages(systemPrompt, history, newUserMessage) };
            var json = JsonSerializer.Serialize(body);

            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = await BuildCopilotRequestAsync(HttpMethod.Post, Endpoint, content);
                var response = await _http.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[GitHub Copilot API error {(int)response.StatusCode}: {responseText}]";

                using var doc = JsonDocument.Parse(responseText);
                var choices = doc.RootElement.GetProperty("choices");
                foreach (var choice in choices.EnumerateArray())
                {
                    var message = choice.GetProperty("message");
                    if (message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        return c.GetString() ?? "[GitHub Copilot returned an empty response.]";
                }
                return "[GitHub Copilot returned an empty response.]";
            }
            catch (Exception ex)
            {
                return $"[Connection error reaching GitHub Copilot: {ex.Message}]";
            }
        }

        private static List<object> ConvertTools(List<object> toolDefinitions)
        {
            var tools = new List<object>();
            foreach (var tool in toolDefinitions)
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(tool));
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString() ?? "";
                var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                object parameters = root.TryGetProperty("input_schema", out var p)
                    ? p.Clone()
                    : new { type = "object", properties = new { } };

                tools.Add(new { type = "function", function = new { name, description, parameters } });
            }
            return tools;
        }

        public async Task<string> SendWithToolsAsync(
            string systemPrompt,
            IEnumerable<ChatMessage> history,
            string newUserMessage,
            List<object> toolDefinitions,
            Func<string, JsonElement, Task<string>> executeTool)
        {
            if (!IsConfigured)
                return "[No GitHub Copilot token set. Add one in Settings to bring this companion online.]";

            var messages = BuildMessages(systemPrompt, history, newUserMessage);
            var tools = ConvertTools(toolDefinitions);

            const int maxToolTurns = 5;
            for (int turn = 0; turn < maxToolTurns; turn++)
            {
                var body = new { model = _model, messages, tools };
                var json = JsonSerializer.Serialize(body);

                string responseText;
                HttpResponseMessage response;
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var request = await BuildCopilotRequestAsync(HttpMethod.Post, Endpoint, content);
                    response = await _http.SendAsync(request);
                    responseText = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return $"[Connection error reaching GitHub Copilot: {ex.Message}]";
                }

                if (!response.IsSuccessStatusCode)
                    return $"[GitHub Copilot API error {(int)response.StatusCode}: {responseText}]";

                using var doc = JsonDocument.Parse(responseText);
                var choices = doc.RootElement.GetProperty("choices");

                JsonElement firstChoice = default;
                foreach (var c in choices.EnumerateArray()) { firstChoice = c; break; }

                var message = firstChoice.GetProperty("message");

                if (!message.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.GetArrayLength() == 0)
                {
                    var text = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString()
                        : null;
                    return string.IsNullOrEmpty(text) ? "[GitHub Copilot returned an empty response.]" : text;
                }

                var clonedCalls = new List<JsonElement>();
                foreach (var call in toolCalls.EnumerateArray())
                    clonedCalls.Add(call.Clone());

                var assistantContent = message.TryGetProperty("content", out var ac) && ac.ValueKind == JsonValueKind.String
                    ? ac.GetString()
                    : null;
                messages.Add(new { role = "assistant", content = assistantContent, tool_calls = clonedCalls });

                foreach (var call in clonedCalls)
                {
                    var callId = call.GetProperty("id").GetString() ?? "";
                    var fn = call.GetProperty("function");
                    var toolName = fn.GetProperty("name").GetString() ?? "";
                    var argsJson = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";

                    JsonElement args;
                    try
                    {
                        using var argsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
                        args = argsDoc.RootElement.Clone();
                    }
                    catch
                    {
                        args = default;
                    }

                    string result;
                    try { result = await executeTool(toolName, args); }
                    catch (Exception ex) { result = $"Tool error: {ex.Message}"; }

                    messages.Add(new { role = "tool", tool_call_id = callId, content = result });
                }
            }

            return "[Reached the tool-call limit for this turn - try asking for one action at a time.]";
        }
    }
}
