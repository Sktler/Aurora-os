using System.Collections.Generic;
using System.Threading.Tasks;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class EmergencyStopTests
{
    [Fact]
    public async Task Revoking_each_provider_disables_it_and_blocks_future_requests()
    {
        var engines = new IChatEngine[]
        {
            new GeminiClient("test-key", "gemini-3.6-flash"),
            new GroqClient("test-key", "llama-3.3-70b-versatile"),
            new OpenAIClient("test-key", "gpt-4o-mini"),
            new ClaudeClient("test-key", "claude-sonnet-5"),
            new GitHubCopilotClient("test-key", "gpt-4o")
        };

        foreach (var engine in engines)
        {
            Assert.True(engine.IsConfigured);

            engine.RevokeCredentials();

            Assert.False(engine.IsConfigured);
            Assert.Empty(await engine.ListModelsAsync());
            Assert.StartsWith("[No ", await engine.SendAsync("", new List<ChatMessage>(), "test"));
        }
    }

    [Fact]
    public async Task Revoking_gemini_also_disables_shared_image_generation()
    {
        var imageGenerator = new ImageGenClient("test-key", "gemini");

        Assert.True(imageGenerator.IsConfigured);

        imageGenerator.RevokeCredentials();

        Assert.False(imageGenerator.IsConfigured);
        Assert.StartsWith("[No ", await imageGenerator.GenerateImageAsync("test"));
    }

    [Fact]
    public void Clearing_active_chat_provider_credential_preserves_other_provider_keys()
    {
        var providers = new[]
        {
            ("gemini", "Gemini"),
            ("groq", "Groq"),
            ("openai", "OpenAI"),
            ("claude", "Claude"),
            ("copilot", "GitHubCopilot")
        };

        foreach (var (provider, expectedCleared) in providers)
        {
            var settings = new AppSettings
            {
                ChatProvider = provider,
                GeminiApiKey = "gemini-key",
                GroqApiKey = "groq-key",
                OpenAIApiKey = "openai-key",
                ClaudeApiKey = "claude-key",
                GitHubCopilotApiKey = "copilot-key"
            };

            settings.ClearActiveChatProviderCredential();

            Assert.Equal(expectedCleared == "Gemini" ? "" : "gemini-key", settings.GeminiApiKey);
            Assert.Equal(expectedCleared == "Groq" ? "" : "groq-key", settings.GroqApiKey);
            Assert.Equal(expectedCleared == "OpenAI" ? "" : "openai-key", settings.OpenAIApiKey);
            Assert.Equal(expectedCleared == "Claude" ? "" : "claude-key", settings.ClaudeApiKey);
            Assert.Equal(expectedCleared == "GitHubCopilot" ? "" : "copilot-key", settings.GitHubCopilotApiKey);
        }
    }
}
