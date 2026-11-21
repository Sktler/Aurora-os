using System.Text.Json;
using System.Text.RegularExpressions;

namespace WhisperDictationProbe;

internal static class ReplayBenchmark
{
    private const int SampleRate = VoiceActivity.SampleRate;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static async Task RunAsync(DictationSession session, string fixtureDirectory, string resultPath,
        int silenceMs, bool realtime, CancellationToken token)
    {
        var manifest = JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(Path.Combine(fixtureDirectory, "manifest.json")), Json)
            ?? throw new InvalidDataException("No benchmark recordings listed.");
        var clips = manifest.ToDictionary(item => item.Id, item => AudioInput.Read(Path.Combine(fixtureDirectory,
            item.Id + (item.Group == "Read speech" ? ".flac" : ".wav"))));

        var cases = manifest.Select(clip => new ReplayCase(clip.Id, clip.Group, [clip.Text], clips[clip.Id])).ToList();
        string[] pauseFixtures = ["librispeech-260-123288-0008", "librispeech-2961-960-0003"];
        foreach (string id in pauseFixtures)
        {
            var clip = manifest.Single(item => item.Id == id);
            var samples = clips[id];
            int splitAt = FindQuietPoint(samples);
            foreach (int pauseMs in new[] { 450, 950, 1400 })
            {
                var withPause = samples[..splitAt].Concat(new float[pauseMs * SampleRate / 1000]).Concat(samples[splitAt..]);
                cases.Add(new ReplayCase($"{id}-pause-{pauseMs}", $"{pauseMs} ms added pause", [clip.Text], withPause.ToArray()));
            }
        }
        var first = manifest.Single(clip => clip.Id == "command-04");
        var second = manifest.Single(clip => clip.Id == "command-09");
        var separated = clips[first.Id].Concat(new float[2400 * SampleRate / 1000]).Concat(clips[second.Id]).ToArray();
        cases.Add(new ReplayCase("two-commands", "Controls", [first.Text, second.Text], separated));
        cases.Add(new ReplayCase("silence", "Controls", [], new float[SampleRate]));

        var trials = new List<Trial>();
        foreach (var item in cases)
        {
            // Pad the recording so the silence threshold can expire before end of input.
            var padded = new float[SampleRate * 32 / 100 + item.Audio.Length + SampleRate * 22 / 10];
            item.Audio.CopyTo(padded, SampleRate * 32 / 100);
            var finals = await session.RunAsync(AudioInput.Replay(padded, realtime, token), token);
            var transcripts = finals.Select(final => final.Text).ToArray();
            bool correctCount = transcripts.Length == item.Expected.Length;
            bool exact = transcripts.Select(Normalize).SequenceEqual(item.Expected.Select(Normalize));
            trials.Add(new Trial(item.Id, item.Group, item.Expected, finals, exact, correctCount));
            Console.WriteLine($"{item.Id}: requests {finals.Length}/{item.Expected.Length}, exact {exact}");
        }

        var report = new Report(silenceMs, realtime, trials.ToArray());
        File.WriteAllText(resultPath, JsonSerializer.Serialize(report, Json));
        foreach (var group in trials.GroupBy(trial => trial.Group))
            Console.WriteLine($"{group.Key}: exact={group.Count(trial => trial.Exact)}/{group.Count()}, expected request count={group.Count(trial => trial.CorrectCount)}/{group.Count()}");

        if (realtime)
        {
            var finals = trials.SelectMany(trial => trial.Finals).ToArray();
            var latency = finals.Select(final => final.FinalAfterSpeechMs).Order().ToArray();
            var decoding = finals.Select(final => final.DecodeMs).Order().ToArray();
            Console.WriteLine($"End-of-speech to final ms: median={Percentile(latency, .50):F0}, p95={Percentile(latency, .95):F0}");
            Console.WriteLine($"Whisper decode ms: median={Percentile(decoding, .50):F0}, p95={Percentile(decoding, .95):F0}");
        }
    }

    private static int FindQuietPoint(float[] samples)
    {
        const int window = 30 * SampleRate / 1000;
        const int step = 10 * SampleRate / 1000;
        int start = Math.Max(0, samples.Length / 2 - 450 * SampleRate / 1000);
        int end = Math.Min(samples.Length - window, samples.Length / 2 + 450 * SampleRate / 1000);
        double quietest = double.PositiveInfinity;
        int best = start;
        for (int i = start; i <= end; i += step)
        {
            double energy = 0;
            for (int j = i; j < i + window; j++) energy += samples[j] * samples[j];
            if (energy >= quietest) continue;
            quietest = energy;
            best = i + window / 2;
        }
        return best;
    }

    private static string Normalize(string text) => string.Join(' ', Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*")
        .Select(match => match.Value.Replace('’', '\'')));

    private static double Percentile(double[] sorted, double fraction) => sorted.Length == 0
        ? double.NaN
        : sorted[(int)Math.Ceiling(sorted.Length * fraction) - 1];

    private sealed record Fixture(string Id, string Group, string Text);
    private sealed record ReplayCase(string Id, string Group, string[] Expected, float[] Audio);
    private sealed record Trial(string Id, string Group, string[] Expected, FinalTranscript[] Finals, bool Exact, bool CorrectCount);
    private sealed record Report(int SilenceMs, bool Realtime, Trial[] Trials);
}
