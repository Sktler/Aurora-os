using System.Diagnostics;
using Whisper.net;
using WhisperDictationProbe;

const int defaultSilenceMs = 1750;
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine($"replay <whisper-model> <silero-model> <fixtures> <results.json> [--silence-ms {defaultSilenceMs}] [--fast]");
    Console.WriteLine($"microphone <whisper-model> <silero-model> [--silence-ms {defaultSilenceMs}]");
    return 0;
}

bool replay = args[0] == "replay";
int required = replay ? 5 : 3;
if ((!replay && args[0] != "microphone") || args.Length < required)
{
    Console.Error.WriteLine("Invalid arguments. Use --help for usage.");
    return 2;
}

int silenceMs = defaultSilenceMs;
bool realtime = true;
for (int i = required; i < args.Length; i++)
{
    if (replay && args[i] == "--fast")
    {
        realtime = false;
        continue;
    }
    if (args[i] == "--silence-ms" && ++i < args.Length && int.TryParse(args[i], out silenceMs) && silenceMs is >= 300 and <= 2000)
        continue;

    Console.Error.WriteLine("Invalid option. --silence-ms accepts 300 to 2000 milliseconds.");
    return 2;
}

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.Cancel();
};

try
{
    var clock = Stopwatch.StartNew();
    using var activity = new VoiceActivity(args[2]);
    using var factory = WhisperFactory.FromPath(args[1], new WhisperFactoryOptions { UseGpu = false });
    using var processor = factory.CreateBuilder().WithThreads(4).WithLanguage("en").Build();
    await foreach (var _ in processor.ProcessAsync(new float[VoiceActivity.SampleRate], stop.Token)) { }
    Console.WriteLine($"Model load and warm-up: {clock.Elapsed.TotalMilliseconds:F0} ms. End silence: {silenceMs} ms.");

    var session = new DictationSession(activity, processor, silenceMs);
    if (replay)
    {
        await ReplayBenchmark.RunAsync(session, args[3], args[4], silenceMs, realtime, stop.Token);
    }
    else
    {
        Console.WriteLine("Listening on the default microphone. Press Ctrl+C to stop. Audio stays in memory.");
        await session.RunAsync(AudioInput.Microphone(stop.Token), stop.Token, transcript =>
        {
            Console.WriteLine($"{transcript.Text} [{transcript.Reason}; {transcript.FinalAfterSpeechMs:F0} ms after detected speech]");
        });
    }
    return 0;
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
