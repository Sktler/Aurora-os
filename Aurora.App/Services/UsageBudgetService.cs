using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    public sealed class UsageRecord
    {
        public string Provider { get; set; } = "";
        public string Day { get; set; } = "";
        public long Requests { get; set; }
        public long Tokens { get; set; }
    }

    /// <summary>Tracks per-provider daily/monthly usage on disk and evaluates optional limits. Never throws.</summary>
    public sealed class UsageBudgetService
    {
        private readonly string _path;
        private readonly Func<DateTime> _now;
        private readonly object _gate = new();
        private List<UsageRecord> _records = new();

        public UsageBudgetService(string path, Func<DateTime>? now = null)
        {
            _path = path;
            _now = now ?? (() => DateTime.Now);
            try
            {
                if (File.Exists(path))
                    _records = JsonSerializer.Deserialize<List<UsageRecord>>(File.ReadAllText(path)) ?? new();
            }
            catch { _records = new(); }
        }

        public static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

        public void Record(string provider, long tokens)
        {
            lock (_gate)
            {
                var day = _now().ToString("yyyy-MM-dd");
                var rec = _records.FirstOrDefault(r => r.Provider == provider && r.Day == day);
                if (rec == null) _records.Add(rec = new UsageRecord { Provider = provider, Day = day });
                rec.Requests++;
                rec.Tokens += Math.Max(0, tokens);
                var cutoff = _now().AddMonths(-2).ToString("yyyy-MM-dd");
                _records.RemoveAll(r => string.CompareOrdinal(r.Day, cutoff) < 0);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                    File.WriteAllText(_path, JsonSerializer.Serialize(_records));
                }
                catch { }
            }
        }

        public (long Requests, long Tokens) GetUsage(string provider, bool monthly)
        {
            lock (_gate)
            {
                var now = _now();
                var prefix = monthly ? now.ToString("yyyy-MM") : now.ToString("yyyy-MM-dd");
                var matching = _records.Where(r => r.Provider == provider && r.Day.StartsWith(prefix, StringComparison.Ordinal));
                return (matching.Sum(r => r.Requests), matching.Sum(r => r.Tokens));
            }
        }

        private static decimal Cost(long tokens, decimal per1K) => tokens / 1000m * per1K;

        /// <summary>Returns the highest usage ratio (0 if no limits) and a description of the limiting metric.</summary>
        private (double Ratio, string Label) Worst(AppSettings s, string provider)
        {
            double best = 0; var label = "";
            void Check(double used, double limit, string name)
            {
                if (limit <= 0) return;
                var r = used / limit;
                if (r > best) { best = r; label = name; }
            }
            foreach (var monthly in new[] { false, true })
            {
                var (req, tok) = GetUsage(provider, monthly);
                var p = monthly ? "monthly" : "daily";
                Check(req, monthly ? s.MonthlyRequestLimit : s.DailyRequestLimit, $"{p} request");
                Check(tok, monthly ? s.MonthlyTokenLimit : s.DailyTokenLimit, $"{p} token");
                Check((double)Cost(tok, s.CostPer1KTokens), (double)(monthly ? s.MonthlyCostLimit : s.DailyCostLimit), $"{p} cost");
            }
            return (best, label);
        }

        /// <summary>Message if requests must be blocked, else null.</summary>
        public string? CheckBlocked(AppSettings s, string provider)
        {
            if (!s.StopAtBudgetLimit) return null;
            var (ratio, label) = Worst(s, provider);
            return ratio >= 1 ? $"[Aurora {label} limit reached for {provider}. Raise or clear the budget in settings to continue.]" : null;
        }

        /// <summary>Warning text when approaching or at a limit, else null.</summary>
        public string? GetWarning(AppSettings s, string provider)
        {
            var (ratio, label) = Worst(s, provider);
            var threshold = Math.Clamp(s.BudgetWarningPercent, 1, 100) / 100.0;
            if (ratio < threshold) return null;
            return $"[Usage warning: {ratio:P0} of the {label} budget for {provider} used.]";
        }

        public string Summary(AppSettings s, string provider)
        {
            var d = GetUsage(provider, false);
            var m = GetUsage(provider, true);
            return $"{provider}: today {d.Requests} requests / ~{d.Tokens} tokens; this month {m.Requests} requests / ~{m.Tokens} tokens (estimated)";
        }
    }

    /// <summary>Decorator enforcing usage budgets around any IChatEngine.</summary>
    public sealed class BudgetedChatEngine : IChatEngine
    {
        private readonly IChatEngine _inner;
        private readonly UsageBudgetService _usage;
        private readonly Func<AppSettings?> _settings;
        private readonly string _provider;

        public BudgetedChatEngine(IChatEngine inner, UsageBudgetService usage, Func<AppSettings?> settings, string provider)
        {
            _inner = inner; _usage = usage; _settings = settings; _provider = provider;
        }

        public bool IsConfigured => _inner.IsConfigured;
        public void RevokeCredentials() => _inner.RevokeCredentials();
        public Task<List<string>> ListModelsAsync() => _inner.ListModelsAsync();

        public Task<string> SendAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage)
            => Run(systemPrompt, history, newUserMessage, () => _inner.SendAsync(systemPrompt, history, newUserMessage));

        public Task<string> SendWithToolsAsync(string systemPrompt, IEnumerable<ChatMessage> history, string newUserMessage, List<object> toolDefinitions, Func<string, JsonElement, Task<string>> executeTool)
            => Run(systemPrompt, history, newUserMessage, () => _inner.SendWithToolsAsync(systemPrompt, history, newUserMessage, toolDefinitions, executeTool));

        private async Task<string> Run(string systemPrompt, IEnumerable<ChatMessage> history, string message, Func<Task<string>> call)
        {
            AppSettings? s = null;
            try
            {
                s = _settings();
                var blocked = s == null ? null : _usage.CheckBlocked(s, _provider);
                if (blocked != null) return blocked;
            }
            catch { }

            var reply = await call();
            if (!_inner.IsConfigured || reply.StartsWith("[No ", StringComparison.Ordinal))
                return reply;

            try
            {
                var tokens = UsageBudgetService.EstimateTokens(systemPrompt) + UsageBudgetService.EstimateTokens(message)
                    + history.Sum(h => UsageBudgetService.EstimateTokens(h.Content)) + UsageBudgetService.EstimateTokens(reply);
                _usage.Record(_provider, tokens);
                var warning = s == null ? null : _usage.GetWarning(s, _provider);
                if (warning != null) reply += "\n\n" + warning;
            }
            catch { }
            return reply;
        }
    }
}
