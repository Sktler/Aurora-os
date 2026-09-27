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
    private static string? _directoryPath;

    public static string DirectoryPath
    {
        get
        {
            lock (Sync)
            {
                if (_directoryPath != null) return _directoryPath;
                var defaultPath = Path.Combine(AppSettings.ConfigDir, "recovery");
                try
                {
                    var path = Path.Combine(AppSettings.ConfigDir, "recovery-storage.json");
                    if (File.Exists(path))
                    {
                        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
                        if (!string.IsNullOrWhiteSpace(document?.DirectoryPath))
                            _directoryPath = Path.GetFullPath(document.DirectoryPath);
                    }
                }
                catch { }

                _directoryPath ??= defaultPath;
                return _directoryPath;
            }
        }
    }

    public static void SetDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("A storage folder is required.", nameof(directoryPath));
        var normalized = Path.GetFullPath(directoryPath.Trim());
        Directory.CreateDirectory(normalized);
        lock (Sync)
        {
            Directory.CreateDirectory(AppSettings.ConfigDir);
            var path = Path.Combine(AppSettings.ConfigDir, "recovery-storage.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new Document { DirectoryPath = normalized }));
            _directoryPath = normalized;
        }
    }
}
