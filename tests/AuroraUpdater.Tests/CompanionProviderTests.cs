using System.IO;
using System.Linq;
using Aurora.App;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class CompanionProviderTests
{
    [Fact]
    public void ResolveCompanionModel_FollowsGlobalUnlessOverridden()
    {
        var previous = App.Settings;
        try
        {
            App.Settings = new AppSettings { ChatProvider = "groq", GroqModel = "g-model", ClaudeModel = "c-default" };

            Assert.Equal(("groq", "g-model"), App.ResolveCompanionModel(new Companion()));
            Assert.Equal(("claude", "c-default"), App.ResolveCompanionModel(new Companion { ProviderOverride = "claude" }));
            Assert.Equal(("claude", "custom"), App.ResolveCompanionModel(new Companion { ProviderOverride = "claude", ModelOverride = " custom " }));
            Assert.Equal(("groq", "g-model"), App.ResolveCompanionModel(new Companion { ProviderOverride = "bogus" }));
        }
        finally { App.Settings = previous; }
    }

    [Fact]
    public void ProviderOverride_PersistsPerCompanion_WithoutAffectingOthers()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.db");
        using var store = new MemoryStore(dbPath);
        store.Initialize();
        store.SaveCompanion(new Companion { Id = "a", Name = "A", ProviderOverride = "claude", ModelOverride = "m1" });
        store.SaveCompanion(new Companion { Id = "b", Name = "B" });

        var loaded = store.LoadCompanions().ToDictionary(c => c.Id);
        Assert.Equal("claude", loaded["a"].ProviderOverride);
        Assert.Equal("m1", loaded["a"].ModelOverride);
        Assert.Equal("", loaded["b"].ProviderOverride);
        Assert.Equal("", loaded["b"].ModelOverride);
    }

    [Fact]
    public void GetChatEngineFor_UsesGlobalEngineWithoutOverride_AndSeparateUnconfiguredEngineWithOverride()
    {
        using var storage = new AppSettingsTestScope();
        var previous = App.Settings;
        var aiProp = typeof(App).GetProperty("AI")!;
        var previousAi = aiProp.GetValue(null);
        var engines = (System.Collections.IDictionary)typeof(App)
            .GetField("CompanionEngines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        try
        {
            lock (engines) engines.Clear();
            App.Settings = new AppSettings { ChatProvider = "gemini" };
            var global = new FakeEngine();
            aiProp.SetValue(null, global);

            Assert.Same(global, App.GetChatEngineFor(new Companion()));

            var overridden = App.GetChatEngineFor(new Companion { ProviderOverride = "claude" });
            Assert.NotSame(global, overridden);
            Assert.False(overridden.IsConfigured);
        }
        finally
        {
            lock (engines) engines.Clear();
            App.Settings = previous;
            aiProp.SetValue(null, previousAi);
        }
    }

    private sealed class FakeEngine : IChatEngine
    {
        public bool IsConfigured => true;
        public void RevokeCredentials() { }
        public System.Threading.Tasks.Task<string> SendAsync(string systemPrompt, System.Collections.Generic.IEnumerable<ChatMessage> history, string newUserMessage) => System.Threading.Tasks.Task.FromResult("");
        public System.Threading.Tasks.Task<string> SendWithToolsAsync(string systemPrompt, System.Collections.Generic.IEnumerable<ChatMessage> history, string newUserMessage, System.Collections.Generic.List<object> toolDefinitions, System.Func<string, System.Text.Json.JsonElement, System.Threading.Tasks.Task<string>> executeTool) => System.Threading.Tasks.Task.FromResult("");
        public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> ListModelsAsync() => System.Threading.Tasks.Task.FromResult(new System.Collections.Generic.List<string>());
    }
}
