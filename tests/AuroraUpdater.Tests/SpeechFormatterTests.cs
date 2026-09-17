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
        var ssml = SpeechFormatter.BuildAzureSsml(text, "en-US-JennyNeural");

        // The reply's actual words are untouched - never rewritten or corrected.
        Assert.Contains("Sure", ssml);
        Assert.Contains("I can help with that", ssml);
        Assert.Contains("Give me one moment", ssml);

        // Pacing/pause hints are added, not the wording itself.
        Assert.Contains("en-US-JennyNeural", ssml);
        Assert.Contains("<prosody rate='-10%'>", ssml);
        Assert.Contains("<break time=\"150ms\"/>", ssml); // comma pause
        Assert.Contains("<break time=\"350ms\"/>", ssml); // sentence pause
    }

    [Fact]
    public void BuildAzureSsml_escapes_voice_name_and_text_for_xml_safety()
    {
        var ssml = SpeechFormatter.BuildAzureSsml("Tom & Jerry said \"hi\"", "en-US-JennyNeural");

        Assert.DoesNotContain("Tom & Jerry", ssml);
        Assert.Contains("Tom &amp; Jerry", ssml);
    }

    [Fact]
    public void Windows_and_openai_pacing_hints_target_a_slightly_slower_than_default_pace()
    {
        // Both nudge speed down from each engine's own default rather than leaving it
        // unset, aiming for the requested 150-170 wpm natural pace.
        Assert.True(SpeechFormatter.WindowsSapiRate < 0);
        Assert.InRange(SpeechFormatter.OpenAiSpeechSpeed, 0.25, 1.0);
    }
}
