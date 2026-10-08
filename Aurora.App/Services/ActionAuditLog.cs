using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aurora.App.Services
{
    public sealed record ActionAuditEntry(
        DateTimeOffset TimestampUtc,
        string ToolName,
        string? Companion,
        string Summary,
        string? Details,
        bool Success,
        bool Approved,
        string? GrantScope,
        string? RiskLevel,
        string? Error,
        string Source)
    {
        public string CompanionName => string.IsNullOrWhiteSpace(Companion) ? "Aurora" : Companion;
        public string DisplaySummary => ActionAuditLog.RedactSensitive(Summary);
        public string DisplayDetails => ActionAuditLog.RedactSensitive(Details);
        public string DisplayError => ActionAuditLog.RedactSensitive(Error);
    }

    public static partial class ActionAuditLog
    {
        private const int MaxEntries = 500;
        private static readonly object SyncRoot = new();
        private static readonly List<ActionAuditEntry> Entries = new();

        static ActionAuditLog()
        {
            LoadFromDisk();
        }

        public static IReadOnlyList<ActionAuditEntry> GetRecent(int maxResults = 50)
        {
            lock (SyncRoot)
            {
                return Entries
                    .OrderByDescending(e => e.TimestampUtc)
                    .Take(maxResults)
                    .ToList();
            }
        }

        public static IReadOnlyList<ActionAuditEntry> Search(string? query, int maxResults = 50)
        {
            var search = (query ?? string.Empty).Trim();
            lock (SyncRoot)
            {
                var results = string.IsNullOrWhiteSpace(search)
                    ? Entries.OrderByDescending(e => e.TimestampUtc).Take(maxResults)
                    : Entries.Where(e =>
                            ContainsIgnoreCase(e.ToolName, search)
                            || ContainsIgnoreCase(e.CompanionName, search)
                            || ContainsIgnoreCase(e.Summary, search)
                            || ContainsIgnoreCase(e.Details ?? string.Empty, search)
                            || ContainsIgnoreCase(e.Error ?? string.Empty, search)
                            || ContainsIgnoreCase(e.Source, search))
                        .OrderByDescending(e => e.TimestampUtc)
                        .Take(maxResults);

                return results.ToList();
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Entries.Clear();
            }

            try
            {
                var path = AuditLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static void Record(
            string toolName,
            string? companion,
            string summary,
            string? details = null,
            bool success = true,
            bool approved = false,
            string? grantScope = null,
            string? riskLevel = null,
            string? error = null,
            string source = "system")
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("A tool name is required for audit log entries.", nameof(toolName));

            var entry = new ActionAuditEntry(
                DateTimeOffset.UtcNow,
                toolName,
                companion,
                RedactSensitive(summary),
                RedactSensitive(details),
                success,
                approved,
                grantScope,
                riskLevel,
                RedactSensitive(error),
                source);

            lock (SyncRoot)
            {
                Entries.Insert(0, entry);
                while (Entries.Count > MaxEntries)
                    Entries.RemoveAt(Entries.Count - 1);
            }

            Persist();
        }

        public static string RedactSensitive(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var result = value;
            result = Regex.Replace(result,
                @"(?i)\b(api[_-]?key|token|secret|password|authorization|cookie|refresh[_-]?token|client[_-]?secret|access[_-]?token)\b\s*(?:[:=]|\s+\-\s+)\s*(?:Bearer\s+)?(?:""[^""]*""|'[^']*'|[^\s,;\]\)}]+)",
                m => $"{m.Groups[1].Value}=[REDACTED]");
            result = Regex.Replace(result,
                @"(?i)([?&](?:api[_-]?key|token|secret|password|authorization|cookie|refresh[_-]?token|client[_-]?secret|access[_-]?token))=([^&\s]+)",
                "$1=[REDACTED]");

            return result;
        }

        private static string AuditLogPath => Path.Combine(AppSettings.ConfigDir, "action-audit-log.json");

        private static void Persist()
        {
            try
            {
                var path = AuditLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(Entries.OrderByDescending(e => e.TimestampUtc).ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private static void LoadFromDisk()
        {
            try
            {
                var path = AuditLogPath;
                if (!File.Exists(path)) return;

                var contents = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(contents)) return;

                var loaded = JsonSerializer.Deserialize<List<ActionAuditEntry>>(contents);
                if (loaded == null || loaded.Count == 0) return;

                lock (SyncRoot)
                {
                    Entries.Clear();
                    foreach (var entry in loaded.OrderByDescending(e => e.TimestampUtc))
                        Entries.Add(entry);
                    while (Entries.Count > MaxEntries)
                        Entries.RemoveAt(Entries.Count - 1);
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
