using Microsoft.ML.OnnxRuntime;

namespace WhisperDictationProbe;

internal sealed class VoiceActivity : IDisposable
{
    public const int SampleRate = 16000;
    public const int FrameSamples = 512;
    public const int FrameMilliseconds = FrameSamples * 1000 / SampleRate;

    private const int ContextSamples = 64;
    private readonly float[] _input = new float[ContextSamples + FrameSamples];
    private readonly float[] _state = new float[2 * 128];
    private readonly InferenceSession _session;
    private readonly RunOptions _runOptions = new();

    public VoiceActivity(string modelPath)
    {
        using var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        _session = new InferenceSession(modelPath, options);
    }

    public void Reset()
    {
        Array.Clear(_input);
        Array.Clear(_state);
    }

    public float Detect(float[] frame)
    {
        // Silero expects 32 ms of audio plus the preceding 4 ms of context and its recurrent state.
        frame.CopyTo(_input, ContextSamples);
        using var audio = OrtValue.CreateTensorValueFromMemory<float>(_input, [1, _input.Length]);
        using var state = OrtValue.CreateTensorValueFromMemory<float>(_state, [2, 1, 128]);
        using var sampleRate = OrtValue.CreateTensorValueFromMemory<long>([SampleRate], []);
        using var result = _session.Run(_runOptions, ["input", "state", "sr"], [audio, state, sampleRate], ["output", "stateN"]);
        float probability = result[0].GetTensorDataAsSpan<float>()[0];
        result[1].GetTensorDataAsSpan<float>().CopyTo(_state);
        frame.AsSpan(FrameSamples - ContextSamples).CopyTo(_input);
        return probability;
    }

    public void Dispose()
    {
        _session.Dispose();
        _runOptions.Dispose();
    }
}
