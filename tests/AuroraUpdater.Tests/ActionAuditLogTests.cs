using System;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class ActionAuditLogTests
{
    [Fact]
    public void Search_ReturnsEntriesForToolOrCompanion_AndRedactsSecrets()
    {
        ActionAuditLog.Clear();

        try
        {
            ActionAuditLog.Record(
                "windows_run_command",
                "Scout",
                "Run command: powershell -Command \"$token='super-secret'; echo $token\"",
                "Authorization: Bearer abc123xyz",
                true,
                true,
                "OneTime",
                "High",
                "",
                "terminal");

            var matches = ActionAuditLog.Search("Scout");
            Assert.NotEmpty(matches);
            Assert.Contains(matches, entry => entry.ToolName == "windows_run_command");
            Assert.DoesNotContain("super-secret", matches[0].DisplaySummary);
            Assert.DoesNotContain("abc123xyz", matches[0].DisplayDetails);

            var redacted = ActionAuditLog.RedactSensitive("token=super-secret and Authorization: Bearer abc123xyz");
            Assert.Contains("[REDACTED]", redacted);
            Assert.DoesNotContain("super-secret", redacted);
            Assert.DoesNotContain("abc123xyz", redacted);
        }
        finally
        {
            ActionAuditLog.Clear();
        }
    }

    [Fact]
    public void Record_StoresDeniedActionsWithoutCrashing()
    {
        ActionAuditLog.Clear();

        try
        {
            var ex = Record.Exception(() => ActionAuditLog.Record(
                "windows_open_path",
                "Nova",
                "Open path: C:\\Users\\Test\\secret.txt",
                "Denied by user.",
                false,
                false,
                "None",
                "Low",
                "User denied the requested action.",
                "approval"));

            Assert.Null(ex);
            var matches = ActionAuditLog.Search("denied");
            Assert.NotEmpty(matches);
            Assert.Contains(matches, entry => entry.Success == false && entry.Companion == "Nova");
        }
        finally
        {
            ActionAuditLog.Clear();
        }
    }
}
