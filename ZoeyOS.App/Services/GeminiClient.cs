using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZoeyOS.App.Models;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Thin wrapper around Google's free-tier Gemini API (generativelanguage.googleapis.com).
    /// Every companion calls into this with its own system prompt and history. The same
    /// API key also powers ImageGenClient's image generation - one free key, no billing,
    /// covers both chat and images.
    /// </summary>
    public class GeminiClient : IChatEngine
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _apiKey;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        public GeminiClient(string apiKey, string model)
        {
            _http = new HttpClient();
            _apiKey = apiKey ?? "";

            var trimmedModel = model?.Trim() ?? "";
            // Defense in depth: AppSettings already normalizes a "models/" prefix on load,
            // but strip it here too so this client can never send a malformed model path
            // to the REST endpoint below, regardless of how the model name got to it.
            if (trimmedModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                trimmedModel = trimmedModel.Substring("models/".Length);

            _model = string.IsNullOrWhiteSpace(trimmedModel) ? "gemini-3.6-flash" : trimmedModel;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        private string Endpoint => $"{BaseUrl}{_model}:generateContent?key={_apiKey}";

        private static List<object> BuildContents(IEnumerable<ChatMessage> history, string newUserMessage)
        {
            var contents = new List<object>();
            foreach (var m in history)
            {
                // Gemini calls the assistant role "model" instead of "assistant".
                var role = m.Role == "assistant" ? "model" : "user";
                contents.Add(new { role, parts = new[] { new { text = m.Content } } });
            }
            contents.Add(new { role = "user", parts = new[] { new { text = newUserMessage } } });
            return contents;
        }

        /// <summary>Sends the companion's full history plus its system prompt, returns Gemini's reply text.</summary>
        public async Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
        {
            if (!IsConfigured)
                return "[No Gemini API key set. Add one in Settings to bring this companion online.]";

            var body = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = BuildContents(history, newUserMessage)
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(Endpoint, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[Gemini API error {(int)response.StatusCode}: {responseText}]";

                return ExtractText(responseText);
            }
            catch (Exception ex)
            {
                return $"[Connection error reaching Gemini: {ex.Message}]";
            }
        }

        private static string ExtractText(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates)) return "[Gemini returned an empty response.]";

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var contentEl)) continue;
                if (!contentEl.TryGetProperty("parts", out var parts)) continue;

                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var txt))
                        sb.Append(txt.GetString());

                if (sb.Length > 0) return sb.ToString();
            }

            return "[Gemini returned an empty response.]";
        }

        /// <summary>Converts our Anthropic-shaped tool schema (name/description/input_schema) into
        /// Gemini's function-declaration shape (name/description/parameters). Gemini's schema is a
        /// stricter subset of JSON Schema than Anthropic's - notably it rejects "additionalProperties"
        /// outright (400 INVALID_ARGUMENT) - so that key is stripped recursively no matter how deep
        /// it appears, rather than relying on every tool definition to omit it correctly.</summary>
        private static List<object> ConvertTools(List<object> toolDefinitions)
        {
            var declarations = new List<object>();
            foreach (var tool in toolDefinitions)
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(tool));
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString() ?? "";
                var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                System.Text.Json.Nodes.JsonNode parameters = root.TryGetProperty("input_schema", out var p)
                    ? System.Text.Json.Nodes.JsonNode.Parse(p.GetRawText())!
                    : new System.Text.Json.Nodes.JsonObject { ["type"] = "object", ["properties"] = new System.Text.Json.Nodes.JsonObject() };

                StripUnsupportedSchemaKeys(parameters);

                declarations.Add(new { name, description, parameters });
            }
            return new List<object> { new { function_declarations = declarations } };
        }

        /// <summary>Removes JSON Schema keys Gemini's function-declaration parser doesn't accept
        /// (currently just "additionalProperties") at every nesting level - object properties,
        /// array items, and anyOf/oneOf branches alike.</summary>
        private static void StripUnsupportedSchemaKeys(System.Text.Json.Nodes.JsonNode? node)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                obj.Remove("additionalProperties");
                foreach (var kvp in obj.ToList())
                    StripUnsupportedSchemaKeys(kvp.Value);
            }
            else if (node is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                    StripUnsupportedSchemaKeys(item);
            }
        }

        /// <summary>
        /// Same as SendAsync, but lets Gemini call tools (e.g. smart home control) mid-turn.
        /// Loops: send -> if Gemini asks for a tool, run it via executeTool and feed the result
        /// back -> repeat until Gemini gives a final text answer or the turn limit is hit.
        /// </summary>
        public async Task<string> SendWithToolsAsync(
            string systemPrompt,
            IEnumerable<ChatMessage> history,
            string newUserMessage,
            List<object> toolDefinitions,
            Func<string, JsonElement, Task<string>> executeTool)
        {
            if (!IsConfigured)
                return "[No Gemini API key set. Add one in Settings to bring this companion online.]";

            var contents = BuildContents(history, newUserMessage);
            var tools = ConvertTools(toolDefinitions);

            const int maxToolTurns = 5;
            for (int turn = 0; turn < maxToolTurns; turn++)
            {
                var body = new
                {
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                    contents,
                    tools
                };

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
                    return $"[Connection error reaching Gemini: {ex.Message}]";
                }

                if (!response.IsSuccessStatusCode)
                    return $"[Gemini API error {(int)response.StatusCode}: {responseText}]";

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    return "[Gemini returned an empty response.]";

                JsonElement firstCandidate = default;
                foreach (var c in candidates.EnumerateArray()) { firstCandidate = c; break; }

                if (!firstCandidate.TryGetProperty("content", out var contentEl) ||
                    !contentEl.TryGetProperty("parts", out var partsEl))
                    return "[Gemini returned an empty response.]";

                // Clone so the parts survive past doc's disposal at the end of this iteration.
                var parts = new List<JsonElement>();
                foreach (var part in partsEl.EnumerateArray())
                    parts.Add(part.Clone());

                var functionCalls = parts.Where(p => p.TryGetProperty("functionCall", out _)).ToList();

                if (functionCalls.Count == 0)
                {
                    var sb = new StringBuilder();
                    foreach (var part in parts)
                        if (part.TryGetProperty("text", out var txt))
                            sb.Append(txt.GetString());
                    return sb.Length > 0 ? sb.ToString() : "[Gemini returned an empty response.]";
                }

                // Feed the model's function-call turn back into the conversation.
                contents.Add(new { role = "model", parts });

                var responseParts = new List<object>();
                foreach (var call in functionCalls)
                {
                    var fc = call.GetProperty("functionCall");
                    var toolName = fc.GetProperty("name").GetString() ?? "";
                    var args = fc.TryGetProperty("args", out var a) ? a : default;

                    string result;
                    try { result = await executeTool(toolName, args); }
                    catch (Exception ex) { result = $"Tool error: {ex.Message}"; }

                    responseParts.Add(new { functionResponse = new { name = toolName, response = new { content = result } } });
                }

                // Gemini's current API doesn't accept a "function" role at all (only
                // user/model/system-family roles) - a function-response turn has to be
                // sent back as a "user" turn containing functionResponse parts, the same
                // way a plain text reply from the user would be. Using "function" here
                // fails every tool call with a 400 (role 'function' is not supported).
                contents.Add(new { role = "user", parts = responseParts });
            }

            return "[Reached the tool-call limit for this turn - try asking for one action at a time.]";
        }
    }
}
