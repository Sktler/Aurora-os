using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NAudio.Wave;

namespace WhisperDictationProbe;

internal static class AudioInput
{
    public static float[] Read(string path)
    {
        using WaveStream reader = Path.GetExtension(path).Equals(".flac", StringComparison.OrdinalIgnoreCase)
            ? new MediaFoundationReader(path)
            : new WaveFileReader(path);
        if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat is not
            { SampleRate: VoiceActivity.SampleRate, BitsPerSample: 16, Channels: 1 })
            throw new InvalidDataException($"{path} must decode to 16 kHz mono PCM16.");

        using var buffer = new MemoryStream();
        reader.CopyTo(buffer);
        var bytes = buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length));
        var samples = new float[bytes.Length / sizeof(short)];
        for (int i = 0; i < samples.Length; i++) samples[i] = ToSample(bytes[(i * sizeof(short))..]);
        return samples;
    }

    public static async IAsyncEnumerable<AudioFrame> Replay(float[] samples, bool realtime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        for (int offset = 0; offset < samples.Length; offset += VoiceActivity.FrameSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new float[VoiceActivity.FrameSamples];
            Array.Copy(samples, offset, frame, 0, Math.Min(frame.Length, samples.Length - offset));
            int dueMs = (offset + frame.Length) * 1000 / VoiceActivity.SampleRate;
            var delay = TimeSpan.FromMilliseconds(dueMs) - clock.Elapsed;
            if (realtime && delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            yield return new AudioFrame(frame, dueMs, Stopwatch.GetTimestamp());
        }
    }

    public static async IAsyncEnumerable<AudioFrame> Microphone([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var capture = new WaveIn
        {
            DeviceNumber = -1,
            WaveFormat = new WaveFormat(VoiceActivity.SampleRate, 16, 1),
            BufferMilliseconds = VoiceActivity.FrameMilliseconds
        };
        var buffers = Channel.CreateBounded<byte[]>(64);
        capture.DataAvailable += (_, eventArgs) =>
        {
            if (!buffers.Writer.TryWrite(eventArgs.Buffer.AsSpan(0, eventArgs.BytesRecorded).ToArray()))
                buffers.Writer.TryComplete(new IOException("Audio processing fell behind the microphone."));
        };
        capture.RecordingStopped += (_, eventArgs) => buffers.Writer.TryComplete(eventArgs.Exception);

        capture.StartRecording();
        try
        {
            var frame = new float[VoiceActivity.FrameSamples];
            int index = 0;
            int endMs = 0;
            await foreach (var buffer in buffers.Reader.ReadAllAsync(cancellationToken))
            {
                for (int offset = 0; offset + 1 < buffer.Length; offset += sizeof(short))
                {
                    frame[index++] = ToSample(buffer.AsSpan(offset));
                    if (index < frame.Length) continue;

                    endMs += VoiceActivity.FrameMilliseconds;
                    yield return new AudioFrame(frame, endMs, Stopwatch.GetTimestamp());
                    frame = new float[VoiceActivity.FrameSamples];
                    index = 0;
                }
            }
        }
        finally
        {
            capture.StopRecording();
        }
    }

    private static float ToSample(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f;
}
