using System;
using System.IO;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class UsageBudgetTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

    [Fact]
    public void Tracks_usage_warns_and_blocks_at_limit()
    {
        var path = TempFile();
        try
        {
            var svc = new UsageBudgetService(path, () => new DateTime(2026, 5, 10));
            var s = new AppSettings { DailyRequestLimit = 5, BudgetWarningPercent = 80 };

            for (var i = 0; i < 3; i++) svc.Record("openai", 100);
            Assert.Null(svc.GetWarning(s, "openai"));

            svc.Record("openai", 100);
            Assert.NotNull(svc.GetWarning(s, "openai"));
            Assert.Null(svc.CheckBlocked(s, "openai"));

            svc.Record("openai", 100);
            Assert.NotNull(svc.CheckBlocked(s, "openai"));
            Assert.Null(svc.CheckBlocked(s, "groq"));

            s.StopAtBudgetLimit = false;
            Assert.Null(svc.CheckBlocked(s, "openai"));
            Assert.Equal((5L, 500L), svc.GetUsage("openai", monthly: true));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Unlimited_by_default_and_corrupt_file_is_safe()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "not json");
            var svc = new UsageBudgetService(path);
            svc.Record("claude", 1_000_000);
            var s = new AppSettings();
            Assert.Null(svc.CheckBlocked(s, "claude"));
            Assert.Null(svc.GetWarning(s, "claude"));
        }
        finally { File.Delete(path); }
    }
}
