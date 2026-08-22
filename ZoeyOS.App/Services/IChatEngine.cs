using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ZoeyOS.App.Models;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Common shape for a chat engine (Gemini, Groq, ...) so the rest of the app -
    /// CompanionViewModel, HomeTools - doesn't care which provider is actually
    /// wired up behind App.AI. Swapping providers is just building a different
    /// implementation of this interface in App.xaml.cs.
    /// </summary>
    public interface IChatEngine
    {
        bool IsConfigured { get; }

        Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage);

        Task<string> SendWithToolsAsync(
            string systemPrompt,
            IEnumerable<ChatMessage> history,
            string newUserMessage,
            List<object> toolDefinitions,
            Func<string, JsonElement, Task<string>> executeTool);

        /// <summary>Live model catalog from this provider's own API - never hard-coded, since
        /// providers add and retire models on their own schedule. Empty list if not configured
        /// (no API key yet); throws on a real API error so the caller can show why.</summary>
        Task<List<string>> ListModelsAsync();
    }
}
