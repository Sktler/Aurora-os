# Whisper dictation probe

This standalone Windows console app uses local Whisper `base.en` for transcription and Silero VAD for detecting speech. It waits for 1,750 ms of detected silence by default. The microphone mode uses the Windows default recording device and prints completed transcripts to the console. Audio stays in memory.

## Replay the #278 recordings

The paths below use the models and labeled recordings from the #278 benchmark. The fixture directory contains a `manifest.json` with the clip IDs, groups, and reference text, plus the mono 16 kHz recordings. [Fluent Speech Commands](https://fluent.ai/fluent-speech-commands-a-dataset-for-spoken-language-understanding-research/) · [LibriSpeech](https://openslr.org/12/)

```powershell
$root = 'artifacts/issue-278-speech'
$project = 'tests/WhisperDictationProbe/WhisperDictationProbe.csproj'
$whisper = "$root/whisper-base-en/ggml-base.en.bin"
$silero = "$root/silero_vad-v6.2.3.onnx"
dotnet run --project $project -c Release -- replay $whisper $silero "$root/fixtures" "$root/whisper-dictation-realtime.json"
```

Replay feeds the clips in 32 ms frames at recording speed. It also inserts pauses in two read-speech clips and tests two commands separated by silence. The output JSON lists the transcript count, normalized exact matches, end-of-speech timestamps as detected by Silero, and Whisper decode times. The elapsed time from detected speech end to final text includes the silence window. Use `--fast` to compare transcript quality and boundaries without waiting for real-time playback.

## Try a microphone

Use the model variables from the replay command. The probe starts recording after the models load. Press Ctrl+C to stop. [Whisper model](https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-base.en.bin) · [Silero VAD model](https://github.com/snakers4/silero-vad/blob/v6.2.3/src/silero_vad/data/silero_vad.onnx)

```powershell
dotnet run --project $project -c Release -- microphone $whisper $silero
```

`--silence-ms` accepts a value from 300 to 2,000 for either mode.
