using System.Linq;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class SpeechFormatterTests
{
    [Fact]
    public void SplitIntoSentences_splits_on_sentence_terminators_without_altering_wording()
    {
        var sentences = SpeechFormatter.SplitIntoSentences("Hello there. How are you today? I'm doing great!");

        Assert.Equal(new[] { "Hello there.", "How are you today?", "I'm doing great!" }, sentences);
    }

    [Fact]
    public void SplitIntoSentences_returns_whole_text_when_there_is_no_terminator()
    {
        var sentences = SpeechFormatter.SplitIntoSentences("just one clause with no ending punctuation");

        Assert.Single(sentences);
        Assert.Equal("just one clause with no ending punctuation", sentences[0]);
    }

    [Fact]
    public void SplitIntoSentences_returns_empty_for_blank_input()
    {
        Assert.Empty(SpeechFormatter.SplitIntoSentences(""));
        Assert.Empty(SpeechFormatter.SplitIntoSentences("   "));
    }

    [Fact]
    public void BuildAzureSsml_preserves_every_word_and_adds_pacing_markup_only()
    {
        var text = "Sure, I can help with that. Give me one moment.";
        var ssml = SpeechFormatter.BuildAzureSsml(text, "en-US-JennyNeural", SpeechFormatter.TargetWordsPerMinute);

        // The reply's actual words are untouched - never rewritten or corrected.
        Assert.Contains("Sure", ssml);
        Assert.Contains("I can help with that", ssml);
        Assert.Contains("Give me one moment", ssml);

        // Pacing/pause hints are added, not the wording itself.
        Assert.Contains("en-US-JennyNeural", ssml);
        Assert.Contains("<prosody rate=", ssml);
        Assert.Contains("<break time=\"150ms\"/>", ssml); // comma pause
        Assert.Contains("<break time=\"350ms\"/>", ssml); // sentence pause
    }

    [Fact]
    public void BuildAzureSsml_escapes_voice_name_and_text_for_xml_safety()
    {
        var ssml = SpeechFormatter.BuildAzureSsml("Tom & Jerry said \"hi\"", "en-US-JennyNeural", SpeechFormatter.TargetWordsPerMinute);

        Assert.DoesNotContain("Tom & Jerry", ssml);
        Assert.Contains("Tom &amp; Jerry", ssml);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(160)]
    [InlineData(220)]
    public void Pace_conversions_stay_within_each_providers_valid_range_across_the_whole_wpm_range(double wpm)
    {
        Assert.InRange(SpeechFormatter.WindowsSapiRateFor(wpm), -10, 10);
        Assert.InRange(SpeechFormatter.OpenAiSpeechSpeedFor(wpm), 0.25, 4.0);
        Assert.Matches(@"^[+-]\d+%$", SpeechFormatter.AzureProsodyRateFor(wpm));
    }

    [Fact]
    public void Faster_wpm_target_never_produces_a_slower_pace_conversion()
    {
        var slow = SpeechFormatter.WindowsSapiRateFor(100);
        var fast = SpeechFormatter.WindowsSapiRateFor(200);
        Assert.True(fast > slow);

        Assert.True(SpeechFormatter.OpenAiSpeechSpeedFor(200) > SpeechFormatter.OpenAiSpeechSpeedFor(100));
    }

    [Fact]
    public void ClampWordsPerMinute_keeps_out_of_range_values_within_bounds()
    {
        Assert.Equal(SpeechFormatter.MinWordsPerMinute, SpeechFormatter.ClampWordsPerMinute(0));
        Assert.Equal(SpeechFormatter.MaxWordsPerMinute, SpeechFormatter.ClampWordsPerMinute(9999));
        Assert.Equal(160, SpeechFormatter.ClampWordsPerMinute(160));
    }

    [Fact]
    public void AppSettings_defaults_the_speech_pace_to_the_natural_target()
    {
        var settings = new AppSettings();
        Assert.Equal(SpeechFormatter.TargetWordsPerMinute, settings.SpeechPaceWpm);
    }
}
