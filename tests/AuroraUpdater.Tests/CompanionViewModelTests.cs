using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Aurora.App;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class CompanionViewModelTests
{
    [Fact]
    public async Task SendCommand_NormalizesChatInput_AndIncludesSystemContext()
    {
        var previousSettings = App.Settings;
        var previousMemory = GetStaticProperty<MemoryStore?>("Memory");
        var previousAi = GetStaticProperty<IChatEngine?>("AI");
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.db");
        MemoryStore? store = null;

        try
        {
            App.Settings = new AppSettings
            {
                SpeakRepliesByDefault = false
            };

            store = new MemoryStore(dbPath);
            store.Initialize();
            SetStaticProperty("Memory", store);

            var engine = new FakeChatEngine("assistant reply");
            SetStaticProperty("AI", engine);

            var companion = new Companion
            {
                Id = "companion-1",
                Name = "Aurora",
                ToolAccess = CompanionToolAccess.General
            };

            var viewModel = new CompanionViewModel(companion)
            {
                DraftMessage = "  Hello,\n   system!  "
            };

            await viewModel.SendCommand.ExecuteAsync(null);

            Assert.Equal(string.Empty, viewModel.DraftMessage);
            Assert.Equal(2, viewModel.Messages.Count);
            Assert.Equal("user", viewModel.Messages[0].Role);
            Assert.Equal("Hello, system!", viewModel.Messages[0].Content);
            Assert.Equal("assistant", viewModel.Messages[1].Role);
            Assert.Equal("assistant reply", viewModel.Messages[1].Content);

            Assert.Equal("Hello, system!", engine.NewUserMessage);
            Assert.Empty(engine.History);
            Assert.Contains("AUTHORITATIVE AURORA TOOL INVENTORY FOR THIS REQUEST:", engine.SystemPrompt);
            Assert.Contains("AUTHORITATIVE WINDOWS SYSTEM DATE/TIME:", engine.SystemPrompt);
            Assert.Contains("get_weather", engine.ToolNames);
            Assert.Contains("windows_run_powershell", engine.ToolNames);
            Assert.Contains("camera_status", engine.ToolNames);
            Assert.DoesNotContain("control_smart_home_device", engine.ToolNames);

            var storedHistory = store.LoadHistory(companion.Id);
            Assert.Equal(2, storedHistory.Count);
            Assert.Equal("Hello, system!", storedHistory[0].Content);
            Assert.Equal("assistant reply", storedHistory[1].Content);
        }
        finally
        {
            App.Settings = previousSettings ?? new AppSettings();
            SetStaticProperty("Memory", previousMemory);
            SetStaticProperty("AI", previousAi);
            store?.Dispose();
        }
    }

    private static T? GetStaticProperty<T>(string name)
    {
        var property = typeof(App).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return property == null ? default : (T?)property.GetValue(null);
    }

    private static void SetStaticProperty<T>(string name, T value)
    {
        var property = typeof(App).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        setter?.Invoke(null, new object?[] { value });
    }

    private sealed class FakeChatEngine : IChatEngine
    {
        private readonly string _reply;

        public FakeChatEngine(string reply)
        {
            _reply = reply;
        }

        public bool IsConfigured => true;

        public string SystemPrompt { get; private set; } = "";
        public List<ChatMessage> History { get; private set; } = new();
        public string NewUserMessage { get; private set; } = "";
        public List<string> ToolNames { get; private set; } = new();

        public void RevokeCredentials()
        {
        }

        public Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
        {
            SystemPrompt = systemPrompt;
            History = history.ToList();
            NewUserMessage = newUserMessage;
            return Task.FromResult(_reply);
        }

        public Task<string> SendWithToolsAsync(
            string systemPrompt,
            IEnumerable<ChatMessage> history,
            string newUserMessage,
            List<object> toolDefinitions,
            System.Func<string, System.Text.Json.JsonElement, Task<string>> executeTool)
        {
            SystemPrompt = systemPrompt;
            History = history.ToList();
            NewUserMessage = newUserMessage;
            ToolNames = toolDefinitions
                .Select(definition => definition.GetType().GetProperty("name")?.GetValue(definition)?.ToString() ?? "")
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            return Task.FromResult(_reply);
        }

        public Task<List<string>> ListModelsAsync() => Task.FromResult(new List<string>());
    }
}
