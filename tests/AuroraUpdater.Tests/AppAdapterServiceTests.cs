using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class AppAdapterServiceTests
{
    [Fact]
    public void GetAdapters_DeclaresCapabilitiesAndPermissions()
    {
        var service = new AppAdapterService(new WindowsAutomationService { ApplicationsEnabled = true, FilesEnabled = true });

        var vscode = service.GetAdapters().Single(x => x.Id == "vscode");
        var word = service.GetAdapters().Single(x => x.Id == "word");

        Assert.Contains("open_file", vscode.Capabilities);
        Assert.Contains("preview_write", vscode.Capabilities);
        Assert.Contains("Applications", vscode.Permissions);
        Assert.Contains("new_document", word.Capabilities);
        Assert.Contains("Applications", word.Permissions);
    }

    [Fact]
    public void BuildWritePreview_DoesNotModifyTheTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurora-adapter-{Guid.NewGuid():N}.txt");
        var preview = AppAdapterService.BuildWritePreview("VS Code", path, "hello");

        Assert.Contains("Preview only", preview);
        Assert.Contains(path, preview);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownAdapterSafely()
    {
        var service = new AppAdapterService(new WindowsAutomationService { ApplicationsEnabled = true });

        var result = await service.ExecuteAsync("does-not-exist", "open_file", "x.txt");

        Assert.False(result.Success);
        Assert.Contains("Unknown app adapter", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsActionsWhenApplicationsPermissionIsDisabled()
    {
        var service = new AppAdapterService(new WindowsAutomationService { ApplicationsEnabled = false });

        var result = await service.ExecuteAsync("vscode", "open_file", "x.txt");

        Assert.False(result.Success);
        Assert.Contains("Application access is disabled", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NewFileReturnsPreviewBeforeChangingContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurora-adapter-{Guid.NewGuid():N}.txt");
        var service = new AppAdapterService(new WindowsAutomationService { ApplicationsEnabled = true, FilesEnabled = true });

        try
        {
            var result = await service.ExecuteAsync("vscode", "new_file", path, "hello Aurora");
            Assert.True(result.Success);
            Assert.True(result.Preview);
            Assert.Contains("Preview only", result.Message);
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
