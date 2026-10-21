using System;

namespace Aurora.App.Services
{
    public enum DashboardLayoutPreset
    {
        Default,
        Compact,
        ChatFocused,
        CompanionFocused,
        Custom
    }

    /// <summary>Panel visibility and sizes for the dashboard window.</summary>
    public sealed record DashboardLayout(bool LeftVisible, bool RightVisible, double LeftWidth, double RightWidth)
    {
        public const double DefaultLeftWidth = 230;
        public const double DefaultRightWidth = 350;
        public const double MinPanelWidth = 160;
        public const double MaxPanelWidth = 600;

        public static DashboardLayout ForPreset(DashboardLayoutPreset preset) => preset switch
        {
            DashboardLayoutPreset.Compact => new(true, false, 190, DefaultRightWidth),
            DashboardLayoutPreset.ChatFocused => new(false, false, DefaultLeftWidth, DefaultRightWidth),
            DashboardLayoutPreset.CompanionFocused => new(true, false, 300, DefaultRightWidth),
            _ => new(true, true, DefaultLeftWidth, DefaultRightWidth)
        };

        /// <summary>Returns a copy with sizes clamped so corrupt or hand-edited settings cannot break the window.</summary>
        public DashboardLayout Normalize() => this with
        {
            LeftWidth = Clamp(LeftWidth, DefaultLeftWidth),
            RightWidth = Clamp(RightWidth, DefaultRightWidth)
        };

        public static DashboardLayoutPreset ParsePreset(string? value) =>
            Enum.TryParse<DashboardLayoutPreset>(value, true, out var preset) && Enum.IsDefined(preset)
                ? preset
                : DashboardLayoutPreset.Default;

        private static double Clamp(double value, double fallback) =>
            double.IsNaN(value) || double.IsInfinity(value)
                ? fallback
                : Math.Min(MaxPanelWidth, Math.Max(MinPanelWidth, value));
    }
}
