using System;
using System.IO;
using System.Text.Json;

namespace Aurora.App.Services;

public static class RecoveryStorageSettings
{
    private sealed class Document
    {
        public string DirectoryPath { get; set; } = "";
    }

    private static readonly object Sync = new();
    private static string? _configDirectory;
    private static string? _directoryPath;

    public static string DirectoryPath
    {
        get
        {
            lock (Sync)
            {
                var configDirectory = AppSettings.ConfigDir;
                if (_directoryPath != null && _configDirectory == configDirectory) return _directoryPath;
                var directoryPath = Path.Combine(configDirectory, "recovery");
                try
                {
                    var path = Path.Combine(configDirectory, "recovery-storage.json");
                    if (File.Exists(path))
                    {
                        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
                        if (!string.IsNullOrWhiteSpace(document?.DirectoryPath))
                            directoryPath = Path.GetFullPath(document.DirectoryPath);
                    }
                }
                catch { }

                _configDirectory = configDirectory;
                return _directoryPath = directoryPath;
            }
        }
    }

    public static void ChangeDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("A storage folder is required.", nameof(directoryPath));
        var normalized = Path.GetFullPath(directoryPath.Trim());
        var current = DirectoryPath;
        if (string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase)) return;

        Directory.CreateDirectory(normalized);
        ProfileRecoveryCodeStore.MoveExistingData(current, normalized);
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppSettings.ConfigDir);
                var path = Path.Combine(AppSettings.ConfigDir, "recovery-storage.json");
                File.WriteAllText(path, JsonSerializer.Serialize(new Document { DirectoryPath = normalized }));
                _directoryPath = normalized;
            }
        }
        catch
        {
            try { ProfileRecoveryCodeStore.MoveExistingData(normalized, current); } catch { }
            throw;
        }
    }
}
