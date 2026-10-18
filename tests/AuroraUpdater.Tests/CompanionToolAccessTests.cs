using Aurora.App.Models;
using Xunit;

namespace AuroraUpdater.Tests;

public class CompanionToolAccessTests
{
    [Fact]
    public void GeneralCompanion_OnlyGetsGeneralTools()
    {
        var companion = new Companion { ToolAccess = CompanionToolAccess.General };

        Assert.True(CompanionToolAccessExtensions.IsToolAllowed("get_weather", companion.ToolAccess));
        Assert.True(CompanionToolAccessExtensions.IsToolAllowed("generate_image", companion.ToolAccess));
        Assert.True(CompanionToolAccessExtensions.IsToolAllowed("camera_status", companion.ToolAccess));
        Assert.False(CompanionToolAccessExtensions.IsToolAllowed("control_smart_home_device", companion.ToolAccess));

        var toolNames = CompanionToolAccessExtensions.GetAllowedToolDefinitions(companion.ToolAccess)
            .Select(def => def.GetType().GetProperty("name")?.GetValue(def)?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Assert.Contains("get_weather", toolNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("generate_image", toolNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("control_smart_home_device", toolNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeCompanion_AllowedHomeTools_AndDeniedGeneralTools()
    {
        var companion = new Companion { ToolAccess = CompanionToolAccess.HomeAutomation };

        Assert.True(CompanionToolAccessExtensions.IsToolAllowed("control_smart_home_device", companion.ToolAccess));
        Assert.False(CompanionToolAccessExtensions.IsToolAllowed("windows_read_file", companion.ToolAccess));
        Assert.False(CompanionToolAccessExtensions.IsToolAllowed("web_search", companion.ToolAccess));
    }

    [Fact]
    public void DeniedTool_ReportsSafePermissionError()
    {
        var companion = new Companion { ToolAccess = CompanionToolAccess.General };

        Assert.False(CompanionToolAccessExtensions.IsToolAllowed("list_smart_home_devices", companion.ToolAccess));
        var message = CompanionToolAccessExtensions.IsToolAllowed("list_smart_home_devices", companion.ToolAccess)
            ? "unexpectedly allowed"
            : "The 'list_smart_home_devices' tool is not available for this companion because its tool access does not include that capability.";

        Assert.Contains("not available for this companion", message, StringComparison.OrdinalIgnoreCase);
    }
}
