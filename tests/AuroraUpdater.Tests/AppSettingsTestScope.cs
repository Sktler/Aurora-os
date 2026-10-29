using System.IO;
using System.Reflection;
using Aurora.App;
using Aurora.App.Services;

namespace AuroraUpdater.Tests;

// Also used as the collection fixture: install storage before App's static services initialize.
// Individual tests that write settings or credentials use a nested scope for a fresh store.
public sealed class AppSettingsTestScope : IDisposable
{
    private readonly string? _previousConfigDirectory = AppSettings.ConfigDirectoryOverride;
    private readonly IDictionary<string, string>? _previousSecrets = WindowsCredentialVault.SecretsOverride;
    private readonly Dictionary<PropertyInfo, object?> _previousAppState;
    private bool _disposed;

    public string DirectoryPath { get; } = Path.Combine(AppContext.BaseDirectory, "test-settings-" + Guid.NewGuid().ToString("N"));
    internal Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    public AppSettingsTestScope()
    {
        Directory.CreateDirectory(DirectoryPath);
        AppSettings.ConfigDirectoryOverride = DirectoryPath;
        WindowsCredentialVault.SecretsOverride = Secrets;
        try
        {
            // Provider refresh replaces several static clients. Restore their exact references,
            // rather than rebuilding them from credentials after the test.
            _previousAppState = typeof(App).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.GetSetMethod(nonPublic: true) != null)
                .ToDictionary(property => property, property => property.GetValue(null));
            App.Settings = new AppSettings();
            typeof(App).GetProperty(nameof(App.Profiles))!.SetValue(null, null);
            typeof(App).GetProperty(nameof(App.Usage))!.SetValue(null, null);
        }
        catch
        {
            RestoreStorage();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var (property, value) in _previousAppState)
                property.SetValue(null, value);
        }
        finally
        {
            RestoreStorage();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private void RestoreStorage()
    {
        AppSettings.ConfigDirectoryOverride = _previousConfigDirectory;
        WindowsCredentialVault.SecretsOverride = _previousSecrets;
    }
}
