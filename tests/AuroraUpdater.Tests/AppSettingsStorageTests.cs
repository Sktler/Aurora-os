using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class AppSettingsStorageTests
{
    [Fact]
    public void Scope_restores_production_defaults_without_accessing_production_storage()
    {
        var previousDirectory = AppSettings.ConfigDirectoryOverride;
        var previousSecrets = WindowsCredentialVault.SecretsOverride;
        try
        {
            AppSettings.ConfigDirectoryOverride = null;
            WindowsCredentialVault.SecretsOverride = null;
            using (var storage = new AppSettingsTestScope())
            {
                Assert.Equal(storage.DirectoryPath, AppSettings.ConfigDir);
                Assert.Same(storage.Secrets, WindowsCredentialVault.SecretsOverride);
            }

            Assert.Null(AppSettings.ConfigDirectoryOverride);
            Assert.Null(WindowsCredentialVault.SecretsOverride);
            // Compare the path string only; never inspect the user's directory or vault.
            Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora"), AppSettings.ConfigDir);
        }
        finally
        {
            AppSettings.ConfigDirectoryOverride = previousDirectory;
            WindowsCredentialVault.SecretsOverride = previousSecrets;
        }
    }

    [Fact]
    public void Settings_and_credentials_round_trip_only_through_the_isolated_store()
    {
        using var storage = new AppSettingsTestScope();
        Assert.Equal(storage.DirectoryPath, AppSettings.ConfigDir);
        Assert.StartsWith(AppContext.BaseDirectory, storage.DirectoryPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(AppSettings.HasSavedConfiguration);

        var settings = AppSettings.LoadOrCreate();
        Assert.Equal(Path.Combine(storage.DirectoryPath, "aurora.db"), settings.DatabasePath);
        settings.ChatProvider = "copilot";
        settings.GitHubCopilotApiKey = "isolated-test-key";
        settings.UserName = "Isolated user";
        settings.SaveGlobalOnly();

        var loaded = AppSettings.LoadOrCreate();
        Assert.Equal("Isolated user", loaded.UserName);
        Assert.Equal("isolated-test-key", loaded.GitHubCopilotApiKey);
        Assert.True(AppSettings.HasSavedConfiguration);
        loaded.GitHubCopilotModel = "test-model";
        loaded.Save();
        Assert.Equal("test-model", AppSettings.LoadOrCreate().GitHubCopilotModel);

        var json = File.ReadAllText(Path.Combine(storage.DirectoryPath, "settings.json"));
        Assert.DoesNotContain("isolated-test-key", json);
        Assert.DoesNotContain(nameof(AppSettings.GitHubCopilotApiKey), json);
    }

    [Fact]
    public void Legacy_secrets_migrate_to_the_isolated_store_and_stay_out_of_saved_json()
    {
        using var storage = new AppSettingsTestScope();
        var secretProperties = typeof(AppSettings).GetProperties()
            .Where(property => property.IsDefined(typeof(JsonIgnoreAttribute), inherit: false)).ToArray();
        Assert.Equal(15, secretProperties.Length);
        var legacySecrets = secretProperties.ToDictionary(property => property.Name, property => "legacy-" + property.Name);
        var path = Path.Combine(storage.DirectoryPath, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(legacySecrets));

        // First-run detection and loading both perform the existing migration.
        Assert.True(AppSettings.HasSavedConfiguration);
        Assert.Equal(legacySecrets.Count, storage.Secrets.Count);
        storage.Secrets.Clear();
        var loaded = AppSettings.LoadOrCreate();
        var freshSettings = new AppSettings();
        foreach (var property in secretProperties)
        {
            Assert.Equal(legacySecrets[property.Name], storage.Secrets[property.Name]);
            Assert.Equal(legacySecrets[property.Name], property.GetValue(loaded));
            Assert.Equal(legacySecrets[property.Name], property.GetValue(freshSettings));
        }

        loaded.Save();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var property in secretProperties)
            Assert.False(document.RootElement.TryGetProperty(property.Name, out _));
    }

    [Fact]
    public void Credential_operations_use_the_override_for_missing_keys_removal_and_clear()
    {
        using var storage = new AppSettingsTestScope();
        Assert.Null(WindowsCredentialVault.GetSecret("missing"));
        Assert.Equal("fallback", WindowsCredentialVault.GetSecret("missing", "fallback"));
        Assert.Equal("fallback", WindowsCredentialVault.GetSecret(" ", "fallback"));
        WindowsCredentialVault.SetSecret(" ", "ignored");
        Assert.Empty(storage.Secrets);

        WindowsCredentialVault.SetSecret("first", "test-value");
        Assert.Equal("test-value", WindowsCredentialVault.GetSecret("first"));
        WindowsCredentialVault.SetSecret("first", " ");
        Assert.Null(WindowsCredentialVault.GetSecret("first"));
        WindowsCredentialVault.SetSecret("first", "test-value");
        WindowsCredentialVault.RemoveSecret("first");
        Assert.Null(WindowsCredentialVault.GetSecret("first"));
        WindowsCredentialVault.SetSecret("first", "one");
        WindowsCredentialVault.SetSecret("second", "two");
        WindowsCredentialVault.ClearAllSecrets();
        Assert.Empty(storage.Secrets);
    }

    [Fact]
    public void Scope_restores_overrides_and_app_state_and_deletes_files_after_an_assertion_fails()
    {
        var previousDirectory = AppSettings.ConfigDirectoryOverride;
        var previousSecrets = WindowsCredentialVault.SecretsOverride;
        var previousSettings = App.Settings;
        var previousUsage = App.Usage;
        string? outerDirectory = null;
        string? innerDirectory = null;
        using (var outer = new AppSettingsTestScope())
        {
            outerDirectory = outer.DirectoryPath;
            App.Settings.UserName = "Outer user";
            App.Settings.GeminiApiKey = "outer-test-key";
            App.Settings.Save();
            var outerSettings = App.Settings;

            Assert.Throws<Xunit.Sdk.FailException>(() =>
            {
                using var inner = new AppSettingsTestScope();
                innerDirectory = inner.DirectoryPath;
                Assert.False(AppSettings.HasSavedConfiguration);
                Assert.Empty(new AppSettings().GeminiApiKey);
                App.Settings.GeminiApiKey = "inner-test-key";
                App.Settings.UserName = "Inner user";
                App.Settings.Save();
                Assert.Fail("Simulated assertion failure to exercise scope cleanup.");
            });

            Assert.False(Directory.Exists(innerDirectory));
            Assert.Equal(outerDirectory, AppSettings.ConfigDir);
            Assert.Same(outer.Secrets, WindowsCredentialVault.SecretsOverride);
            Assert.Same(outerSettings, App.Settings);
            Assert.Equal("outer-test-key", new AppSettings().GeminiApiKey);
            Assert.Equal("Outer user", AppSettings.LoadOrCreate().UserName);
        }

        Assert.False(Directory.Exists(outerDirectory));
        Assert.Equal(previousDirectory, AppSettings.ConfigDirectoryOverride);
        Assert.Same(previousSecrets, WindowsCredentialVault.SecretsOverride);
        Assert.Same(previousSettings, App.Settings);
        Assert.Same(previousUsage, App.Usage);

        using var next = new AppSettingsTestScope();
        Assert.False(AppSettings.HasSavedConfiguration);
        Assert.Empty(new AppSettings().GeminiApiKey);
    }

    [Fact]
    public void Reset_uses_the_isolated_directory_and_clears_only_its_credentials()
    {
        using var outer = new AppSettingsTestScope();
        WindowsCredentialVault.SetSecret(nameof(AppSettings.GeminiApiKey), "outer-test-key");
        using (var inner = new AppSettingsTestScope())
        {
            var settings = AppSettings.LoadOrCreate();
            settings.GeminiApiKey = "reset-test-key";
            AppSettings.ResetAll();

            Assert.False(Directory.Exists(inner.DirectoryPath));
            Assert.Empty(inner.Secrets);
        }
        Assert.Equal("outer-test-key", new AppSettings().GeminiApiKey);
    }
}
