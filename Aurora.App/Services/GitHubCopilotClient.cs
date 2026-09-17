using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    /// <summary>
    /// Thin wrapper around GitHub Copilot's chat completions API. It speaks the same
    /// OpenAI-shaped JSON payload as the other hosted chat providers, but uses
    /// GitHub's Copilot host and a GitHub bearer token instead of an OpenAI key.
    /// </summary>
    public class GitHubCopilotClient : IChatEngine
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _apiKey;
        private const string Endpoint = "https://api.githubcopilot.com/chat/completions";
        private const string ModelsEndpoint = "https://api.githubcopilot.com/models";

        public GitHubCopilotClient(string apiKey, string model)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;

            _http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("Aurora/1.0");
            }
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<List<string>> ListModelsAsync()
        {
            if (!IsConfigured) return new List<string>();

            var response = await _http.GetAsync(ModelsEndpoint);
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
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(Endpoint, content);
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
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                string responseText;
                HttpResponseMessage response;
                try
                {
                    response = await _http.PostAsync(Endpoint, content);
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
