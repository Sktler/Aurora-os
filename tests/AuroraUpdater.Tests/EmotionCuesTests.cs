using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class EmotionCuesTests
{
    [Theory]
    [InlineData("I'm sorry to hear that.", EmotionCue.Empathetic)]
    [InlineData("Congratulations on the launch!", EmotionCue.Excited)]
    [InlineData("I'm glad it worked.", EmotionCue.Positive)]
    [InlineData("What would you like to do next?", EmotionCue.Curious)]
    [InlineData("The file is 4 KB.", EmotionCue.Neutral)]
    public void Detect_maps_reply_tone_to_cue(string reply, EmotionCue expected) =>
        Assert.Equal(expected, EmotionCues.Detect(reply, EmotionCues.ModeSubtle));

    [Fact]
    public void Detect_is_neutral_when_off_or_empty()
    {
        Assert.Equal(EmotionCue.Neutral, EmotionCues.Detect("I'm sorry!", EmotionCues.ModeOff));
        Assert.Equal(EmotionCue.Neutral, EmotionCues.Detect(null, EmotionCues.ModeSubtle));
    }

    [Fact]
    public void Reduced_mode_is_weaker_than_subtle_and_unknown_modes_default_to_subtle()
    {
        Assert.True(EmotionCues.Intensity(EmotionCues.ModeReduced) < EmotionCues.Intensity(EmotionCues.ModeSubtle));
        Assert.Equal(0, EmotionCues.Intensity(EmotionCues.ModeOff));
        Assert.Equal(EmotionCues.ModeSubtle, EmotionCues.NormalizeMode("garbage"));
    }
}
