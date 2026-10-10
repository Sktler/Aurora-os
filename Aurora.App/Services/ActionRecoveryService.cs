using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Aurora.App.Services;

public sealed class ActionRecoveryService
{
    private readonly Func<string?, Task> _restoreClipboardAsync;
    private readonly Stack<IRecoveryEntry> _entries = new();
    private readonly object _sync = new();

    public ActionRecoveryService(Func<string?, Task> restoreClipboardAsync)
    {
        _restoreClipboardAsync = restoreClipboardAsync ?? throw new ArgumentNullException(nameof(restoreClipboardAsync));
    }

    public bool CanUndo
    {
        get
        {
            lock (_sync)
                return _entries.Count > 0;
        }
    }

    public string DescribeNextUndo()
    {
        lock (_sync)
        {
            if (_entries.Count == 0)
                return "No reversible action is available to undo.";

            return _entries.Peek().Description;
        }
    }

    public void RecordFileWrite(string path, bool existedBefore, string? previousContent)
    {
        var fullPath = Path.GetFullPath(path);
        lock (_sync)
            _entries.Push(new FileWriteEntry(fullPath, existedBefore, previousContent));
    }

    public void RecordClipboardChange(bool hadTextBefore, string? previousText)
    {
        lock (_sync)
            _entries.Push(new ClipboardEntry(hadTextBefore, previousText));
    }

    public async Task<string> UndoLastAsync()
    {
        IRecoveryEntry? entry;
        lock (_sync)
        {
            entry = _entries.Count > 0 ? _entries.Pop() : null;
        }

        if (entry == null)
            return "No reversible action is available to undo.";

        return await entry.UndoAsync(_restoreClipboardAsync);
    }

    private interface IRecoveryEntry
    {
        string Description { get; }
        Task<string> UndoAsync(Func<string?, Task> restoreClipboardAsync);
    }

    private sealed record FileWriteEntry(string Path, bool ExistedBefore, string? PreviousContent) : IRecoveryEntry
    {
        public string Description => ExistedBefore
            ? $"Restore the previous contents of {Path}."
            : $"Delete {Path}.";

        public async Task<string> UndoAsync(Func<string?, Task> restoreClipboardAsync)
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (ExistedBefore)
            {
                await File.WriteAllTextAsync(Path, PreviousContent ?? string.Empty);
                return $"Restored the previous contents of {Path}.";
            }

            if (File.Exists(Path))
                File.Delete(Path);

            return $"Deleted {Path}.";
        }
    }

    private sealed record ClipboardEntry(bool HadTextBefore, string? PreviousText) : IRecoveryEntry
    {
        public string Description => HadTextBefore
            ? "Restore the previous clipboard text."
            : "Clear the clipboard.";

        public async Task<string> UndoAsync(Func<string?, Task> restoreClipboardAsync)
        {
            await restoreClipboardAsync(HadTextBefore ? PreviousText : null);
            return HadTextBefore
                ? "Restored the previous clipboard text."
                : "Cleared the clipboard.";
        }
    }
}
