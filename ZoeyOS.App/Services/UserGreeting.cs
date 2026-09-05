using System;

namespace ZoeyOS.App.Services
{
    public static class UserGreeting
    {
        public static string NormalizeName(string? name)
        {
            var trimmed = name?.Trim() ?? "";
            return trimmed.Length > 40 ? trimmed.Substring(0, 40) : trimmed;
        }

        public static string Build(DateTime localTime, string? name)
        {
            var part = localTime.Hour < 12 ? "morning" : localTime.Hour < 17 ? "afternoon" : "evening";
            var normalized = NormalizeName(name);
            return string.IsNullOrWhiteSpace(normalized) ? $"Good {part}" : $"Good {part}, {normalized}";
        }
    }
}
