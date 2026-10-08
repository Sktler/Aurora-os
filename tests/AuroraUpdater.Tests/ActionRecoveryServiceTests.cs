using System;
using System.IO;
using System.Threading.Tasks;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class ActionRecoveryServiceTests
{
    [Fact]
    public async Task File_writes_can_be_restored_to_the_previous_contents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"aurora-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "note.txt");
        await File.WriteAllTextAsync(path, "before", cancellationToken);
        var service = new ActionRecoveryService(_ => Task.CompletedTask);

        try
        {
            service.RecordFileWrite(path, existedBefore: true, previousContent: "before");
            await File.WriteAllTextAsync(path, "after", cancellationToken);

            var message = await service.UndoLastAsync();

            Assert.Contains("Restored", message);
            Assert.Equal("before", await File.ReadAllTextAsync(path, cancellationToken));
            Assert.False(service.CanUndo);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Clipboard_writes_restore_the_previous_value_or_clear_it()
    {
        string? restoredClipboard = "initial";
        var service = new ActionRecoveryService(text =>
        {
            restoredClipboard = text;
            return Task.CompletedTask;
        });

        service.RecordClipboardChange(hadTextBefore: true, previousText: "before");

        var restoreMessage = await service.UndoLastAsync();

        Assert.Contains("Restored", restoreMessage);
        Assert.Equal("before", restoredClipboard);

        service.RecordClipboardChange(hadTextBefore: false, previousText: null);
        var clearMessage = await service.UndoLastAsync();

        Assert.Contains("Cleared", clearMessage);
        Assert.Null(restoredClipboard);
    }

    [Fact]
    public async Task WindowsAutomationService_records_and_restores_file_writes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"aurora-automation-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "note.txt");
        await File.WriteAllTextAsync(path, "before", cancellationToken);
        var previousSettings = App.Settings;
        ActionApprovalCenter.ClearSessionGrants();
        App.Settings = new AppSettings
        {
            ActionApprovalMode = "AllowTrusted",
            TrustedTools = ["windows_write_file"]
        };
        var service = new WindowsAutomationService { FilesEnabled = true };

        try
        {
            await service.WriteTextAsync(path, "after", cancellationToken);

            Assert.Equal("after", await File.ReadAllTextAsync(path, cancellationToken));

            var undoMessage = await service.UndoLastReversibleActionAsync(cancellationToken);

            Assert.Contains("Restored", undoMessage);
            Assert.Equal("before", await File.ReadAllTextAsync(path, cancellationToken));
        }
        finally
        {
            App.Settings = previousSettings;
            ActionApprovalCenter.ClearSessionGrants();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
