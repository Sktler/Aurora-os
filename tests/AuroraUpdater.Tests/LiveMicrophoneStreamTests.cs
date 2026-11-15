using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class LiveMicrophoneStreamTests
{
    [Fact]
    public void Speech_engine_accepts_and_reads_live_pcm_input_without_opening_a_microphone()
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        if (installed.Count == 0) Assert.Skip("A Windows speech recognizer is required.");
        using var engine = new SpeechRecognitionEngine(installed[0]);
        using var stream = new LiveMicrophoneStream();
        try
        {
            stream.Push(new byte[32000]);
            stream.Complete();
            engine.SetInputToAudioStream(stream,
                new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            engine.LoadGrammar(new Grammar(new GrammarBuilder("hey aurora")
            {
                Culture = engine.RecognizerInfo.Culture
            }));
            Assert.Null(engine.Recognize(TimeSpan.FromSeconds(2)));
            Assert.True(stream.Position > 0, "SAPI did not consume the queued PCM audio.");
        }
        finally
        {
            stream.Complete();
            engine.SetInputToNull();
        }
    }

    [Fact]
    public void Position_tracks_consumed_bytes_while_length_stays_open_ended()
    {
        using var stream = new LiveMicrophoneStream();
        Assert.False(stream.CanSeek);
        Assert.Equal(long.MaxValue, stream.Length);
        Assert.Equal(0, stream.Position);
        stream.Push([1, 2, 3, 4, 5]);
        stream.Complete();
        var buffer = new byte[3];
        Assert.Equal(3, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(3, stream.Position);
        Assert.Equal(2, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(5, stream.Position);
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(5, stream.Position);
        Assert.Equal(long.MaxValue, stream.Length);
    }

    [Fact]
    public void Position_queries_do_not_allow_rewinding_or_skipping_live_audio()
    {
        using var stream = new LiveMicrophoneStream();
        Assert.Equal(0, stream.Seek(0, SeekOrigin.Current));
        stream.Push([1, 2]);
        stream.Complete();
        Assert.Equal(2, stream.Read(new byte[2], 0, 2));
        Assert.Equal(2, stream.Seek(0, SeekOrigin.Current));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.Seek(1, SeekOrigin.Current));
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }
}
