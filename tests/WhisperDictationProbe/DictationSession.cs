using System.Diagnostics;
using System.Threading.Channels;
using Whisper.net;

namespace WhisperDictationProbe;

internal sealed record AudioFrame(float[] Samples, int AudioEndMs, long ReceivedAt);
internal sealed record FinalTranscript(string Text, int SpeechEndMs, int EndpointMs, string Reason,
    double DecodeMs, double FinalAfterSpeechMs);

internal sealed class DictationSession(VoiceActivity activity, WhisperProcessor processor, int silenceMilliseconds)
{
    private const int PrerollFrames = 10;
    private const int TailFrames = 5;
    private const int MaximumUtteranceMs = 30000;
    private const float StartProbability = 0.50f;
    private const float ContinueProbability = 0.35f;

    public async Task<FinalTranscript[]> RunAsync(IAsyncEnumerable<AudioFrame> source, CancellationToken cancellationToken,
        Action<FinalTranscript>? onFinal = null)
    {
        activity.Reset();
        var endings = Channel.CreateUnbounded<Utterance>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = sessionCancellation.Token;
        // Continue reading the microphone while Whisper finishes the previous request.
        var decode = Task.Run(async () =>
        {
            try
            {
                var transcripts = new List<FinalTranscript>();
                await foreach (var ending in endings.Reader.ReadAllAsync(token))
                {
                    long decodeStart = Stopwatch.GetTimestamp();
                    var segments = new List<string>();
                    await foreach (var segment in processor.ProcessAsync(ending.Samples, token))
                    {
                        if (!string.IsNullOrWhiteSpace(segment.Text)) segments.Add(segment.Text.Trim());
                    }
                    long finished = Stopwatch.GetTimestamp();
                    var transcript = new FinalTranscript(string.Join(' ', segments), ending.SpeechEnd.AudioEndMs,
                        ending.EndpointMs, ending.Reason, Stopwatch.GetElapsedTime(decodeStart, finished).TotalMilliseconds,
                        Stopwatch.GetElapsedTime(ending.SpeechEnd.ReceivedAt, finished).TotalMilliseconds);
                    transcripts.Add(transcript);
                    onFinal?.Invoke(transcript);
                }
                return transcripts.ToArray();
            }
            catch
            {
                await sessionCancellation.CancelAsync();
                throw;
            }
        });

        FinalTranscript[] transcripts;
        try
        {
            await CaptureAsync(source, endings.Writer, token);
        }
        finally
        {
            endings.Writer.TryComplete();
            transcripts = await decode;
        }
        return transcripts;
    }

    private async Task CaptureAsync(IAsyncEnumerable<AudioFrame> source, ChannelWriter<Utterance> endings,
        CancellationToken cancellationToken)
    {
        var preroll = new Queue<AudioFrame>(PrerollFrames);
        var utterance = new List<AudioFrame>();
        int consecutiveSpeech = 0;
        int speechEnd = 0;
        await foreach (var frame in source.WithCancellation(cancellationToken))
        {
            float speech = activity.Detect(frame.Samples);
            if (utterance.Count == 0)
            {
                if (preroll.Count == PrerollFrames) preroll.Dequeue();
                preroll.Enqueue(frame);
                consecutiveSpeech = speech >= StartProbability ? consecutiveSpeech + 1 : 0;
                if (consecutiveSpeech < 2) continue;

                utterance.AddRange(preroll);
                preroll.Clear();
                speechEnd = utterance.Count - 1;
                continue;
            }

            utterance.Add(frame);
            if (speech >= ContinueProbability) speechEnd = utterance.Count - 1;
            int silence = frame.AudioEndMs - utterance[speechEnd].AudioEndMs;
            int elapsed = frame.AudioEndMs - utterance[0].AudioEndMs + VoiceActivity.FrameMilliseconds;
            if (silence < silenceMilliseconds && elapsed < MaximumUtteranceMs) continue;

            var reason = silence >= silenceMilliseconds ? "Silence" : "Duration limit";
            await endings.WriteAsync(CreateUtterance(utterance, speechEnd, reason), cancellationToken);
            utterance.Clear();
            consecutiveSpeech = 0;
        }

        if (utterance.Count > 0)
            await endings.WriteAsync(CreateUtterance(utterance, speechEnd, "End of input"), cancellationToken);
    }

    private static Utterance CreateUtterance(List<AudioFrame> frames, int speechEnd, string reason)
    {
        int retained = Math.Min(frames.Count, speechEnd + 1 + TailFrames);
        var samples = new float[retained * VoiceActivity.FrameSamples];
        for (int i = 0; i < retained; i++) frames[i].Samples.CopyTo(samples, i * VoiceActivity.FrameSamples);
        return new Utterance(samples, frames[speechEnd], frames[^1].AudioEndMs, reason);
    }

    private sealed record Utterance(float[] Samples, AudioFrame SpeechEnd, int EndpointMs, string Reason);
}
