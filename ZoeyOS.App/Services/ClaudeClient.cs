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
    /// Thin wrapper around Anthropic's Claude Messages API (api.anthropic.com). Like OpenAI,
    /// this is a paid, metered API - Anthropic doesn't offer a permanent free API tier (new
    /// accounts sometimes get limited trial credit). Notably simpler to wire up than the other
    /// three engines here: our tool definitions (Services/SystemTools.cs etc.) are already
    /// written in Anthropic's own "name/description/input_schema" shape, since that's the
    /// format this whole app's tool-calling convention was modeled on - so no schema
    /// conversion is needed at all, unlike Gemini/Groq/OpenAI.
    /// </summary>
    public class ClaudeClient : IChatEngine
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _apiKey;
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const int MaxTokens = 4096;

        public ClaudeClient(string apiKey, string model)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-5" : model;

            _http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                _http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
            }
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        private static List<object> BuildMessages(IEnumerable<ChatMessage> history, string newUserMessage)
        {
            var messages = new List<object>();
            foreach (var m in history)
                messages.Add(new { role = m.Role, content = m.Content }); // "user" / "assistant" match Claude's roles already
            messages.Add(new { role = "user", content = newUserMessage });
            return messages;
        }

        /// <summary>Sends the companion's full history plus its system prompt, returns Claude's reply text.</summary>
        public async Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
        {
            if (!IsConfigured)
                return "[No Claude API key set. Add one in Settings to bring this companion online.]";

            var body = new
            {
                model = _model,
                max_tokens = MaxTokens,
                system = systemPrompt,
                messages = BuildMessages(history, newUserMessage)
            };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(Endpoint, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[Claude API error {(int)response.StatusCode}: {responseText}]";

                using var doc = JsonDocument.Parse(responseText);
                if (!doc.RootElement.TryGetProperty("content", out var contentBlocks))
                    return "[Claude returned an empty response.]";

                var sb = new StringBuilder();
                foreach (var block in contentBlocks.EnumerateArray())
                    if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                        block.TryGetProperty("text", out var txt))
                        sb.Append(txt.GetString());

                return sb.Length > 0 ? sb.ToString() : "[Claude returned an empty response.]";
            }
            catch (Exception ex)
            {
                return $"[Connection error reaching Claude: {ex.Message}]";
            }
        }

        /// <summary>
        /// Same as SendAsync, but lets Claude call tools (e.g. smart home control) mid-turn.
        /// Loops: send -> if Claude asks for a tool (stop_reason "tool_use"), run every
        /// requested tool via executeTool and feed all the results back in one user turn ->
        /// repeat until Claude gives a final text answer or the turn limit is hit.
        /// </summary>
        public async Task<string> SendWithToolsAsync(
            string systemPrompt,
            IEnumerable<ChatMessage> history,
            string newUserMessage,
            List<object> toolDefinitions,
            Func<string, JsonElement, Task<string>> executeTool)
        {
            if (!IsConfigured)
                return "[No Claude API key set. Add one in Settings to bring this companion online.]";

            var messages = BuildMessages(history, newUserMessage);

            const int maxToolTurns = 5;
            for (int turn = 0; turn < maxToolTurns; turn++)
            {
                var body = new
                {
                    model = _model,
                    max_tokens = MaxTokens,
                    system = systemPrompt,
                    messages,
                    tools = toolDefinitions // already in Claude's own schema shape - no conversion needed
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
                    return $"[Connection error reaching Claude: {ex.Message}]";
                }

                if (!response.IsSuccessStatusCode)
                    return $"[Claude API error {(int)response.StatusCode}: {responseText}]";

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (!root.TryGetProperty("content", out var contentBlocksEl))
                    return "[Claude returned an empty response.]";

                // Clone so the blocks survive past doc's disposal at the end of this iteration.
                var blocks = new List<JsonElement>();
                foreach (var block in contentBlocksEl.EnumerateArray())
                    blocks.Add(block.Clone());

                var toolUseBlocks = blocks.Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "tool_use").ToList();

                if (toolUseBlocks.Count == 0)
                {
                    var sb = new StringBuilder();
                    foreach (var block in blocks)
                        if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                            block.TryGetProperty("text", out var txt))
                            sb.Append(txt.GetString());
                    return sb.Length > 0 ? sb.ToString() : "[Claude returned an empty response.]";
                }

                // Feed the assistant's tool-call turn back into the conversation, exactly as returned.
                messages.Add(new { role = "assistant", content = blocks });

                var toolResults = new List<object>();
                foreach (var block in toolUseBlocks)
                {
                    var toolUseId = block.GetProperty("id").GetString() ?? "";
                    var toolName = block.GetProperty("name").GetString() ?? "";
                    var input = block.TryGetProperty("input", out var i) ? i : default;

                    string result;
                    try { result = await executeTool(toolName, input); }
                    catch (Exception ex) { result = $"Tool error: {ex.Message}"; }

                    toolResults.Add(new { type = "tool_result", tool_use_id = toolUseId, content = result });
                }

                // Claude expects every tool result from one turn bundled into a single user message.
                messages.Add(new { role = "user", content = toolResults });
            }

            return "[Reached the tool-call limit for this turn - try asking for one action at a time.]";
        }
    }
}
