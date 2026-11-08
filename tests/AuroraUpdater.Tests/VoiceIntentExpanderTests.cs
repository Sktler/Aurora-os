using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class VoiceIntentExpanderTests
{
    [Theory]
    [InlineData("weather", "get_weather")]
    [InlineData("Weather.", "get_weather")]
    [InlineData("what's the weather", "get_weather")]
    [InlineData("forecast", "get_weather")]
    [InlineData("news", "news headlines")]
    [InlineData("next song", "next media track")]
    [InlineData("pause", "Pause")]
    public void Single_words_and_short_phrases_expand_to_explicit_requests(string heard, string expectedFragment)
    {
        Assert.Contains(expectedFragment, VoiceIntentExpander.Expand(heard));
    }

    [Theory]
    [InlineData("what is the capital of France")]
    [InlineData("remind me to call mom about the weather report tomorrow")]
    public void Longer_sentences_pass_through_unchanged(string heard)
    {
        Assert.Equal(heard, VoiceIntentExpander.Expand(heard));
    }

    [Fact]
    public void Partial_phrases_keep_the_users_words()
    {
        Assert.Contains("weather tomorrow", VoiceIntentExpander.Expand("weather tomorrow"));
    }
}
