using System.Speech.Recognition;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class WakeWordRecognizerTests
{
    [Theory]
    [InlineData("hey aurora", "AuroraWakeWord")]
    [InlineData("hi aurora", "AuroraWakeWord")]
    [InlineData("okay aurora", "AuroraWakeWord")]
    [InlineData("hey aurora tell me a story about the sea", "AuroraInlineCommand")]
    public void Wake_grammars_accept_bare_activation_and_complete_inline_dictation(string text, string grammar)
    {
        using var engine = CreateEngine();
        engine.LoadGrammar(WakeWordRecognizer.CreateWakeGrammar(engine.RecognizerInfo.Culture));
        engine.LoadGrammar(WakeWordRecognizer.CreateInlineCommandGrammar(engine.RecognizerInfo.Culture));
        var result = engine.EmulateRecognize(text);
        Assert.NotNull(result);
        Assert.Equal(text, result.Text, ignoreCase: true);
        Assert.Equal(grammar, result.Grammar?.Name);
    }

    [Theory]
    [InlineData("tell me a story about the sea")]
    [InlineData("you will")]
    public void Wake_grammar_rejects_dictation_without_a_wake_prefix(string text)
    {
        using var engine = CreateEngine();
        engine.LoadGrammar(WakeWordRecognizer.CreateWakeGrammar(engine.RecognizerInfo.Culture));
        engine.LoadGrammar(WakeWordRecognizer.CreateInlineCommandGrammar(engine.RecognizerInfo.Culture));
        Assert.Null(engine.EmulateRecognize(text));
    }

    private static SpeechRecognitionEngine CreateEngine()
    {
        var info = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.Name == "en-US");
        if (info == null) Assert.Skip("An en-US Windows speech recognizer is required.");
        return new SpeechRecognitionEngine(info);
    }
}
