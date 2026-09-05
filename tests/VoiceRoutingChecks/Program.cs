using System;
using System.IO;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var voice = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "Services", "VoiceService.cs"));
var companion = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "ViewModels", "CompanionViewModel.cs"));
var app = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "App.xaml.cs"));
var failures = 0;

Check(voice.Contains("public event Action<bool>? SpeakingChanged", StringComparison.Ordinal),
    "voice service exposes speech lifecycle notifications");
Check(voice.Contains("SpeakingChanged?.Invoke(true)", StringComparison.Ordinal)
    && voice.Contains("SpeakingChanged?.Invoke(false)", StringComparison.Ordinal),
    "speech lifecycle notifications cover the complete utterance");
Check(voice.Contains("player.PlaySync()", StringComparison.Ordinal),
    "cloud speech does not return before audio playback finishes");
Check(companion.Contains("await SpeakReplyAsync(reply)", StringComparison.Ordinal)
    && companion.Contains("App.Voice.StopContinuousListening()", StringComparison.Ordinal),
    "continuous recognition is stopped before speaking a reply");
Check(app.Contains("WakeWord.Stop()", StringComparison.Ordinal)
    && app.Contains("WakeWord.Start()", StringComparison.Ordinal),
    "wake-word recognition is paused and restored around speech");

Console.WriteLine(failures == 0 ? "PASS: voice routing checks." : $"FAIL: {failures} voice routing checks failed.");
return failures == 0 ? 0 : 1;

void Check(bool passed, string message)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {message}");
    if (!passed) failures++;
}
