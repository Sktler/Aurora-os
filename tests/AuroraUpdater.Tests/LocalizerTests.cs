using System.IO;
using System.Linq;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class LocalizerTests
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("xx", "en")]
    [InlineData("es", "es")]
    [InlineData("FR-ca", "fr")]
    [InlineData("de_DE", "de")]
    public void Normalize_maps_to_supported_language_or_english(string? input, string expected)
        => Assert.Equal(expected, Localizer.NormalizeLanguage(input));

    [Fact]
    public void Get_returns_translation_and_falls_back_to_english_then_key()
    {
        Assert.Equal("Configuración", Localizer.Get("Settings.Title", "es"));
        Assert.Equal("Settings", Localizer.Get("Settings.Title", "xx"));
        // Present in English only, so Spanish falls back cleanly.
        Assert.Equal(Localizer.Get("Settings.Music.Notice", "en"), Localizer.Get("Settings.Music.Notice", "es"));
        Assert.Equal("Missing.Key", Localizer.Get("Missing.Key", "de"));
    }

    [Fact]
    public void Every_translation_key_exists_in_english()
    {
        var english = Localizer.AllStrings["en"].Keys.ToHashSet();
        foreach (var table in Localizer.AllStrings.Values)
            Assert.All(table.Keys, k => Assert.Contains(k, english));
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("es", "Spanish")]
    [InlineData("fr", "French")]
    [InlineData("de", "German")]
    [InlineData("unsupported", "English")]
    public void Response_language_instruction_uses_selected_supported_language(string language, string expectedLanguage)
    {
        Assert.Contains($"in {expectedLanguage},", Localizer.GetResponseLanguageInstruction(language));
        Assert.Contains("chat text that may be read aloud", Localizer.GetResponseLanguageInstruction(language));
    }

    [Fact]
    public void Settings_defaults_to_english_and_view_offers_language_dropdown()
    {
        Assert.Equal("en", new AppSettings().Language);
        var xaml = File.ReadAllText(Path.Combine(FindRoot(), "Aurora.App", "Views", "SettingsView.xaml"));
        Assert.Contains("x:Name=\"LanguageBox\"", xaml);
    }

    [Fact]
    public void Language_setting_survives_settings_serialization_and_translates_setup_and_chat_labels()
    {
        var serialized = System.Text.Json.JsonSerializer.Serialize(new AppSettings { Language = "es" });
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(serialized);

        Assert.Equal("es", restored?.Language);
        Assert.Equal("Configuración del proveedor de IA de Aurora", Localizer.GetUiText("Aurora AI Provider Setup", "es"));
        Assert.Equal("Chatea con Aurora", Localizer.GetUiText("Chat with Aurora", "es"));
        Assert.Equal("Mit Aurora chatten", Localizer.GetUiText("Chat with Aurora", "de"));
        Assert.Equal("Almacenamiento de recuperación:", Localizer.GetUiText("Recovery storage:", "es"));
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aurora.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
}
