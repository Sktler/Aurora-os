using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class VoicePipelineTests
{
    [Fact]
    public void Continuous_listening_only_wires_final_results_never_partial_hypotheses()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Services", "VoiceService.cs"));

        Assert.Contains("SpeechRecognized +=", code);
        Assert.DoesNotContain("SpeechHypothesized +=", code);
    }

    [Fact]
    public void Recognized_utterances_are_gated_on_confidence_and_recorded_for_debugging()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Services", "VoiceService.cs"));

        Assert.Contains("VoiceTranscriptFilter.MeetsConfidenceThreshold", code);
        Assert.Contains("RecordTranscript", code);
        Assert.Contains("RecentTranscripts", code);
    }

    [Fact]
    public void Reply_speech_is_chunked_by_sentence_before_being_spoken()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Services", "VoiceService.cs"));

        Assert.Contains("SpeechFormatter.SplitIntoSentences(text)", code);
    }

    [Fact]
    public void Voice_input_normalizer_only_removes_transcription_noise_never_corrects_words()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Services", "VoiceInputNormalizer.cs"));

        // Guards against a future change quietly turning this into a spell-checker: only
        // whitespace/punctuation cleanup and duplicate detection are allowed here, never a
        // word dictionary/correction step.
        Assert.DoesNotContain("SpellCheck", code);
        Assert.DoesNotContain("Correct(", code);
        Assert.DoesNotContain("Dictionary", code);
    }

    [Fact]
    public void Settings_ui_exposes_adjustable_voice_confidence_and_speaking_pace()
    {
        var repoRoot = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "ViewModels", "IntegrationsViewModel.cs"));
        var xaml = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Views", "IntegrationsWindow.xaml"));

        Assert.Contains("_voiceConfidenceThreshold = App.Settings.VoiceConfidenceThreshold", viewModel);
        Assert.Contains("_speechPaceWpm = App.Settings.SpeechPaceWpm", viewModel);
        Assert.Contains("App.Settings.VoiceConfidenceThreshold = Services.VoiceTranscriptFilter", viewModel);
        Assert.Contains("App.Settings.SpeechPaceWpm = SpeechFormatter.ClampWordsPerMinute(SpeechPaceWpm)", viewModel);

        Assert.Contains("Binding VoiceConfidenceThreshold", xaml);
        Assert.Contains("Binding SpeechPaceWpm", xaml);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aurora.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Aurora.App")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Aurora repository root.");
    }
}
