using System;
using System.Threading.Tasks;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class TerminalToolsTests
{
    [Fact]
    public async Task PowerAsync_RequiresPowerPermission()
    {
        var previousSettings = App.Settings;
        App.Settings = new AppSettings
        {
            WindowsTerminalEnabled = true,
            WindowsPowerEnabled = false
        };

        try
        {
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TerminalTools.PowerAsync("shutdown"));
            Assert.Equal("Power controls are disabled.", ex.Message);
        }
        finally
        {
            App.Settings = previousSettings;
        }
    }
}
