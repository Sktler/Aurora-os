using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class VoiceTranscriptFilterTests
{
    [Theory]
    [InlineData(0.70, 0.70, true)]
    [InlineData(0.85, 0.70, true)]
    [InlineData(0.69, 0.70, false)]
    [InlineData(0.0, 0.70, false)]
    public void MeetsConfidenceThreshold_gates_on_the_configured_threshold(double confidence, double threshold, bool expected)
    {
        Assert.Equal(expected, VoiceTranscriptFilter.MeetsConfidenceThreshold(confidence, threshold));
    }

    [Fact]
    public void Default_confidence_threshold_starts_at_0_70_as_requested()
    {
        Assert.Equal(0.70, VoiceTranscriptFilter.DefaultConfidenceThreshold);
    }

    [Fact]
    public void AppSettings_uses_the_default_confidence_threshold_out_of_the_box()
    {
        var settings = new AppSettings();

        Assert.Equal(VoiceTranscriptFilter.DefaultConfidenceThreshold, settings.VoiceConfidenceThreshold);
    }

    [Fact]
    public void TranscriptEntry_keeps_the_raw_text_separate_from_acceptance()
    {
        var rejected = new TranscriptEntry("mumbled nonsense", 0.4, Accepted: false, System.DateTime.UtcNow);
        var accepted = new TranscriptEntry("turn on the lights", 0.92, Accepted: true, System.DateTime.UtcNow);

        Assert.False(rejected.Accepted);
        Assert.True(accepted.Accepted);
        Assert.Equal("mumbled nonsense", rejected.RawText);
        Assert.Equal("turn on the lights", accepted.RawText);
    }
}
