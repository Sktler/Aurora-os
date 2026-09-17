using System;
using System.IO;
using System.Text.Json;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

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
        var configDir = AppSettings.ConfigDir;
        var configPath = Path.Combine(configDir, "settings.json");
        var backup = File.Exists(configPath) ? File.ReadAllText(configPath) : null;

        Directory.CreateDirectory(configDir);
        var settings = new AppSettings
        {
            ChatProvider = "copilot",
            GitHubCopilotApiKey = "ghp_test1234567890",
            GitHubCopilotModel = "gpt-4o"
        };

        try
        {
            File.WriteAllText(configPath, JsonSerializer.Serialize(settings));
            Assert.True(AppSettings.HasSavedConfiguration);
        }
        finally
        {
            if (backup is null)
            {
                if (File.Exists(configPath)) File.Delete(configPath);
            }
            else
            {
                File.WriteAllText(configPath, backup);
            }
        }
    }
}
