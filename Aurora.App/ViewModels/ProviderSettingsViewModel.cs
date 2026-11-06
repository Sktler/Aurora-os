using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Aurora.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aurora.App.ViewModels;

/// <summary>Shared provider configuration for first-run setup and AI Engine settings.</summary>
public partial class ProviderSettingsViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly Func<AIProviderInfo, string, Task<List<string>>> _load;
    private int _generation;
    private bool _loading;
    public IReadOnlyList<AIProviderInfo> Providers => AIProviderCatalog.All;
    public ObservableCollection<string> Models { get; } = new();
    internal Task PendingModelLoad { get; private set; } = Task.CompletedTask;
    [ObservableProperty] private AIProviderInfo? _provider;
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;

    public ProviderSettingsViewModel(AppSettings settings,
        Func<AIProviderInfo, string, Task<List<string>>>? load = null)
    {
        _settings = settings;
        _load = load ?? LoadModelsAsync;
        Provider = AIProviderCatalog.Get(settings.ChatProvider);
    }

    partial void OnProviderChanged(AIProviderInfo? value)
    {
        if (value == null) return;
        _generation++;
        _loading = true;
        ApiKey = GetKey(value.Key);
        Model = GetModel(value.Key);
        Models.Clear();
        foreach (var example in value.ModelExamples.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) Models.Add(example);
        _loading = false;
        Status = "";
        OnPropertyChanged(nameof(KeyHint));
        OnPropertyChanged(nameof(CostNote));
        PendingModelLoad = RefreshAsync();
    }

    partial void OnApiKeyChanged(string value)
    {
        if (_loading) return;
        // Invalidate an in-flight request immediately; focus loss starts the next one.
        _generation++;
        IsLoading = false;
    }

    public string KeyHint => Localizer.GetUiText(Provider?.KeyHint ?? "");
    public string CostNote => Localizer.GetUiText(Provider?.CostNote ?? "");
    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(KeyHint));
        OnPropertyChanged(nameof(CostNote));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        var provider = Provider;
        var key = ApiKey.Trim();
        if (provider == null || string.IsNullOrWhiteSpace(key))
        {
            IsLoading = false;
            Status = Localizer.GetUiText("Enter an API key to load available models. You can also type a model name.");
            return;
        }
        IsLoading = true;
        Status = Localizer.FormatUiText("Loading models from {0}...", provider.DisplayName);
        try
        {
            var models = await _load(provider, key);
            if (generation != _generation) return;
            // Keep the free-text selection even if the API does not list it.
            var selection = Model;
            Models.Clear();
            foreach (var model in models) Models.Add(model);
            Model = selection;
            Status = Localizer.FormatUiText("{0} models loaded live from {1}.", models.Count, provider.DisplayName);
        }
        catch (Exception ex)
        {
            if (generation == _generation)
                Status = Localizer.FormatUiText("Couldn't load models: {0}", ex is InvalidOperationException ? ex.Message : ProviderErrors.Connection(provider.DisplayName, ex));
        }
        finally { if (generation == _generation) IsLoading = false; }
    }

    private static async Task<List<string>> LoadModelsAsync(AIProviderInfo provider, string key)
    {
        if (App.IsLocalOnlyModeEnabled || App.Security?.IsLockedDown == true)
            throw new InvalidOperationException(Localizer.GetUiText("Model discovery is unavailable in local-only mode or during Emergency Stop."));
        IChatEngine engine = provider.Key switch
        {
            "groq" => new GroqClient(key, provider.DefaultModel),
            "openai" => new OpenAIClient(key, provider.DefaultModel),
            "claude" => new ClaudeClient(key, provider.DefaultModel),
            "copilot" => new GitHubCopilotClient(key, provider.DefaultModel),
            _ => new GeminiClient(key, provider.DefaultModel)
        };
        try { return await engine.ListModelsAsync(); }
        finally { engine.RevokeCredentials(); }
    }

    public bool Save(bool refreshClients)
    {
        if (Provider is not { } provider || string.IsNullOrWhiteSpace(ApiKey))
        {
            Status = Localizer.GetUiText("Enter an API key first.");
            return false;
        }
        var model = string.IsNullOrWhiteSpace(Model) ? provider.DefaultModel : Model.Trim();
        if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)) model = model[7..];
        switch (provider.Key)
        {
            case "groq": _settings.GroqApiKey = ApiKey.Trim(); _settings.GroqModel = model; break;
            case "openai": _settings.OpenAIApiKey = ApiKey.Trim(); _settings.OpenAIModel = model; break;
            case "claude": _settings.ClaudeApiKey = ApiKey.Trim(); _settings.ClaudeModel = model; break;
            case "copilot": _settings.GitHubCopilotApiKey = ApiKey.Trim(); _settings.GitHubCopilotModel = model; break;
            default: _settings.GeminiApiKey = ApiKey.Trim(); _settings.GeminiModel = model; break;
        }
        _settings.ChatProvider = provider.Key;
        _settings.Save();
        if (refreshClients) App.RefreshIntegrationClients();
        Status = Localizer.GetUiText("Provider and model saved. Changes apply to the next request.");
        return true;
    }

    private string GetKey(string key) => key switch
    {
        "groq" => _settings.GroqApiKey, "openai" => _settings.OpenAIApiKey,
        "claude" => _settings.ClaudeApiKey, "copilot" => _settings.GitHubCopilotApiKey,
        _ => _settings.GeminiApiKey
    };
    private string GetModel(string key) => key switch
    {
        "groq" => _settings.GroqModel, "openai" => _settings.OpenAIModel,
        "claude" => _settings.ClaudeModel, "copilot" => _settings.GitHubCopilotModel,
        _ => _settings.GeminiModel
    };
    public void Dispose() { _generation++; ApiKey = ""; }
}
