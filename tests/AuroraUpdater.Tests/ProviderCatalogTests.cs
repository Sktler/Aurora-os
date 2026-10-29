using System;
using Aurora.App;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public class ProviderCatalogTests
{
    [Fact]
    public void Catalog_includes_github_copilot_provider()
    {
        var provider = AIProviderCatalog.Get("copilot");

        Assert.Equal("copilot", provider.Key);
        Assert.Contains("GitHub Copilot", provider.DisplayName);
        Assert.Equal("gpt-4o", provider.DefaultModel);
    }

    [Fact]
    public void AppSettings_recognizes_github_copilot_configuration()
    {
        using var storage = new AppSettingsTestScope();
        var settings = new AppSettings
        {
            ChatProvider = "copilot",
            GitHubCopilotApiKey = "ghp_test1234567890",
            GitHubCopilotModel = "gpt-4o"
        };

        settings.Save();
        Assert.True(AppSettings.HasSavedConfiguration);
    }

    [Fact]
    public void LocalOnlyMode_block_message_raises_clear_privacy_notice()
    {
        var message = App.LocalOnlyModeBlockedMessage("web search");

        Assert.Contains("local-only mode is enabled", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("web search", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationsViewModel_refreshes_provider_fields_when_the_active_provider_changes()
    {
        using var storage = new AppSettingsTestScope();
        App.Settings = new AppSettings
        {
            ChatProvider = "gemini",
            GeminiApiKey = "gemini-key",
            GeminiModel = "gemini-3.6-flash",
            GroqApiKey = "groq-key",
            GroqModel = "llama-3.3-70b-versatile",
            GitHubCopilotApiKey = "ghp-key",
            GitHubCopilotModel = "gpt-4o"
        };

        App.RefreshIntegrationClients();

        using var vm = new IntegrationsViewModel(Array.Empty<Companion>());
        Assert.Equal("Gemini (Google) model", vm.ModelFieldLabel);
        Assert.Equal("gemini-3.6-flash", vm.ModelName);
        Assert.Contains("Gemini", vm.CurrentEngineDescription, StringComparison.OrdinalIgnoreCase);

        App.Settings.ChatProvider = "groq";
        App.RefreshIntegrationClients();

        Assert.Equal("Groq model", vm.ModelFieldLabel);
        Assert.Equal("llama-3.3-70b-versatile", vm.ModelName);
        Assert.Contains("Groq", vm.CurrentEngineDescription, StringComparison.OrdinalIgnoreCase);

        App.Settings.ChatProvider = "copilot";
        App.RefreshIntegrationClients();

        Assert.Equal("GitHub Copilot (unofficial) model", vm.ModelFieldLabel);
        Assert.Equal("gpt-4o", vm.ModelName);
        vm.ModelName = "gpt-4o-mini";
        vm.SaveModelCommand.Execute(null);
        Assert.Equal("gpt-4o-mini", App.Settings.GitHubCopilotModel);
        Assert.Equal("gpt-4o-mini", AppSettings.LoadOrCreate().GitHubCopilotModel);
    }
}
