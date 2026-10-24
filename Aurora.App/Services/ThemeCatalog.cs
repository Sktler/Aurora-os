using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Aurora.App.Services
{
    public sealed record AppTheme(string Name, string BgDeep, string BgPanel, string BgCard, string TextPrimary,
        string TextMuted, string AccentCyan, string AccentViolet, string AccentAmber, string AccentGreen, string IdleGlow);

    /// <summary>Built-in themes and orb skins. Unknown names always fall back to the defaults.</summary>
    public static class ThemeCatalog
    {
        public const string DefaultThemeName = "Aurora Dark";
        public const string DefaultOrbSkin = "Tinted";
        public const string OriginalOrbSkin = "Original";
        public const string VividOrbSkin = "Vivid";

        public static readonly AppTheme[] Themes =
        [
            new(DefaultThemeName, "#0B0E14", "#12161F", "#171C28", "#FFFFFF", "#D0D7E2", "#4FD8E8", "#8C6FF0", "#F0B65C", "#5BE0A0", "#3A4356"),
            new("Midnight Blue", "#080D1A", "#0E1630", "#142042", "#FFFFFF", "#C9D6F2", "#5AA9FF", "#A08CFF", "#F2C26B", "#5FE3B0", "#33426B"),
            new("Ember", "#120C0C", "#1C1212", "#271919", "#FFFFFF", "#E6D3CC", "#FF8A5B", "#E06C9F", "#F5C04E", "#8FDB7A", "#4D3535"),
            new("Forest", "#0A110D", "#111B15", "#18271E", "#FFFFFF", "#D0E2D6", "#4FE0B0", "#9AD66B", "#E8C15A", "#5BE0A0", "#34483B"),
        ];

        public static readonly string[] OrbSkins = [DefaultOrbSkin, OriginalOrbSkin, VividOrbSkin];

        public static AppTheme Resolve(string? name) =>
            Themes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];

        public static string NormalizeOrbSkin(string? name) =>
            OrbSkins.FirstOrDefault(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) ?? DefaultOrbSkin;

        /// <summary>Updates the shared brushes, replacing frozen ones for dynamic resource consumers.
        /// Failures leave the current look untouched.</summary>
        public static void Apply(string? name, ResourceDictionary? resources = null)
        {
            resources ??= Application.Current?.Resources;
            if (resources == null) return;
            var theme = Resolve(name);
            try
            {
                Set(resources, "BgDeep", theme.BgDeep);
                Set(resources, "BgPanel", theme.BgPanel);
                Set(resources, "BgCard", theme.BgCard);
                Set(resources, "TextPrimary", theme.TextPrimary);
                Set(resources, "TextMuted", theme.TextMuted);
                Set(resources, "AccentCyan", theme.AccentCyan);
                Set(resources, "AccentViolet", theme.AccentViolet);
                Set(resources, "AccentAmber", theme.AccentAmber);
                Set(resources, "AccentGreen", theme.AccentGreen);
                Set(resources, "IdleGlow", theme.IdleGlow);
                if (resources["BgDeepTranslucentBrush"] is SolidColorBrush { IsFrozen: false } translucent)
                {
                    var c = (Color)ColorConverter.ConvertFromString(theme.BgDeep);
                    translucent.Color = Color.FromArgb(0xE6, c.R, c.G, c.B);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Set(ResourceDictionary resources, string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            if (resources[key + "Brush"] is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                    resources[key + "Brush"] = new SolidColorBrush(color);
                else
                    brush.Color = color;
            }
        }
    }
}
