using Aurora.App;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class ProviderSettingsViewModelTests
{
    [Fact]
    public async Task Switching_provider_loads_its_saved_key_and_ignores_a_late_previous_response()
    {
        using var scope = new AppSettingsTestScope();
        App.Settings.GeminiApiKey = "fake-gemini";
        App.Settings.GroqApiKey = "fake-groq";
        App.Settings.GroqModel = "custom-groq";
        var first = new TaskCompletionSource<List<string>>();
        var second = new TaskCompletionSource<List<string>>();
        var requests = new List<(string Provider, string Key)>();
        using var vm = new ProviderSettingsViewModel(App.Settings, (provider, key) =>
        {
            requests.Add((provider.Key, key));
            return provider.Key == "gemini" ? first.Task : second.Task;
        });
        var firstLoad = vm.PendingModelLoad;
        vm.Provider = AIProviderCatalog.Get("groq");
        var secondLoad = vm.PendingModelLoad;
        Assert.Equal("custom-groq", vm.Model);
        second.SetResult(["groq-model"]);
        await secondLoad;
        first.SetResult(["stale-gemini"]);
        await firstLoad;
        Assert.Equal(new[] { ("gemini", "fake-gemini"), ("groq", "fake-groq") }, requests);
        Assert.Equal(new[] { "groq-model" }, vm.Models);
        Assert.Equal("custom-groq", vm.Model);
    }

    [Fact]
    public void Saving_provider_preserves_other_keys_and_free_text_model()
    {
        using var scope = new AppSettingsTestScope();
        App.Settings.GeminiApiKey = "keep-gemini";
        using var vm = new ProviderSettingsViewModel(App.Settings, (_, _) => Task.FromResult(new List<string>()));
        vm.Provider = AIProviderCatalog.Get("groq");
        vm.ApiKey = "new-groq";
        vm.Model = "custom-future-model";
        Assert.True(vm.Save(refreshClients: false));
        var restored = AppSettings.LoadOrCreate();
        Assert.Equal("groq", restored.ChatProvider);
        Assert.Equal("custom-future-model", restored.GroqModel);
        Assert.Equal("keep-gemini", restored.GeminiApiKey);
        Assert.Equal("new-groq", restored.GroqApiKey);
    }

    [Fact]
    public void Legacy_Models_pin_maps_to_AI_Engine()
    {
        var settings = new AppSettings { PinnedSettingsSections = ["Models"] };
        var navigation = new SettingsNavigationViewModel(settings, () => { });
        Assert.Equal(Aurora.App.Models.SettingsSection.Engine, Assert.Single(navigation.PinnedEntries).Section);
    }
}
