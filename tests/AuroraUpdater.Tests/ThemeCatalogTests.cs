using System.Linq;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests
{
    public class ThemeCatalogTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("does-not-exist")]
        public void UnknownThemeFallsBackToDefault(string? name) =>
            Assert.Equal(ThemeCatalog.DefaultThemeName, ThemeCatalog.Resolve(name).Name);

        [Fact]
        public void ResolveIsCaseInsensitive() =>
            Assert.Equal("Ember", ThemeCatalog.Resolve("ember").Name);

        [Fact]
        public void UnknownOrbSkinFallsBackToDefault() =>
            Assert.Equal(ThemeCatalog.DefaultOrbSkin, ThemeCatalog.NormalizeOrbSkin("bogus"));

        [Fact]
        public void ThemeNamesAreUniqueAndColorsValid()
        {
            Assert.Equal(ThemeCatalog.Themes.Length, ThemeCatalog.Themes.Select(t => t.Name).Distinct().Count());
            foreach (var t in ThemeCatalog.Themes)
                foreach (var hex in new[] { t.BgDeep, t.BgPanel, t.BgCard, t.TextPrimary, t.TextMuted, t.AccentCyan, t.AccentViolet, t.AccentAmber, t.AccentGreen, t.IdleGlow })
                    Assert.Matches("^#[0-9A-Fa-f]{6}$", hex);
        }

        [Fact]
        public void SettingsDefaultsToDefaultAppearance()
        {
            var s = new AppSettings();
            Assert.Equal(ThemeCatalog.DefaultThemeName, s.ThemeName);
            Assert.Equal(ThemeCatalog.DefaultOrbSkin, s.OrbSkin);
        }
    }
}
