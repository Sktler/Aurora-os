using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class ActionApprovalCenterTests
{
    [Fact]
    public void GrantAction_SessionScopeAllowsToolUntilSessionEnds()
    {
        var settings = new AppSettings();
        ActionApprovalCenter.ClearSessionGrants();

        try
        {
            Assert.True(ActionApprovalCenter.GrantAction("windows_run_command", ActionGrantScope.Session, settings));
            Assert.False(ActionApprovalCenter.RequiresConfirmation("windows_run_command", settings));
            Assert.Equal(ActionGrantScope.Session, ActionApprovalCenter.GetGrantScope("windows_run_command", settings));

            ActionApprovalCenter.ClearSessionGrants();
            Assert.True(ActionApprovalCenter.RequiresConfirmation("windows_run_command", settings));
        }
        finally
        {
            ActionApprovalCenter.ClearSessionGrants();
        }
    }

    [Fact]
    public void GrantAction_OneTimeScopeIsConsumedAfterUse()
    {
        var settings = new AppSettings();
        ActionApprovalCenter.ClearSessionGrants();

        try
        {
            Assert.True(ActionApprovalCenter.GrantAction("mcp_call_tool", ActionGrantScope.OneTime, settings));
            Assert.False(ActionApprovalCenter.RequiresConfirmation("mcp_call_tool", settings));
            Assert.Equal(ActionGrantScope.OneTime, ActionApprovalCenter.GetGrantScope("mcp_call_tool", settings));

            Assert.True(ActionApprovalCenter.ConsumeOneTimeGrant("mcp_call_tool"));
            Assert.True(ActionApprovalCenter.RequiresConfirmation("mcp_call_tool", settings));
        }
        finally
        {
            ActionApprovalCenter.ClearSessionGrants();
        }
    }

    [Fact]
    public void GrantAction_PersistentScopePersistsAcrossChecksAndCanBeRevoked()
    {
        var settings = new AppSettings();
        ActionApprovalCenter.ClearSessionGrants();

        try
        {
            Assert.True(ActionApprovalCenter.GrantAction("windows_write_file", ActionGrantScope.Persistent, settings));
            Assert.False(ActionApprovalCenter.RequiresConfirmation("windows_write_file", settings));
            Assert.Contains("windows_write_file", settings.PersistentGrantedTools);

            ActionApprovalCenter.RevokeAction("windows_write_file", settings);
            Assert.True(ActionApprovalCenter.RequiresConfirmation("windows_write_file", settings));
            Assert.DoesNotContain("windows_write_file", settings.PersistentGrantedTools);
        }
        finally
        {
            ActionApprovalCenter.ClearSessionGrants();
        }
    }
}
