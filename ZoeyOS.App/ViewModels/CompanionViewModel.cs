using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.ViewModels
{
    public partial class CompanionViewModel : ObservableObject
    {
        public Companion Companion { get; }
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        [ObservableProperty] private string _draftMessage = "";
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isRenaming;
        [ObservableProperty] private string _renameDraft = "";
        [ObservableProperty] private bool _isListening;
        [ObservableProperty] private bool _speakRepliesEnabled = App.Settings.SpeakRepliesByDefault;

        public CompanionViewModel(Companion companion)
        {
            Companion = companion;
            foreach (var m in App.Memory.LoadHistory(companion.Id)) Messages.Add(m);
        }

        [RelayCommand] private void BeginRename() { RenameDraft = Companion.Name; IsRenaming = true; }
        [RelayCommand] private void CommitRename() { var trimmed = RenameDraft.Trim(); if (!string.IsNullOrWhiteSpace(trimmed)) { Companion.Name = trimmed; App.Memory.SaveCompanion(Companion); } IsRenaming = false; }
        [RelayCommand] private void CancelRename() => IsRenaming = false;

        [RelayCommand]
        private void ClearChatHistory()
        {
            if (IsBusy) return;
            App.Memory.ClearHistory(Companion.Id);
            Messages.Clear();
            DraftMessage = "";
            AttachStatus = "Chat history cleared.";
            Companion.LastActivitySummary = "New conversation ready.";
            Companion.Status = CompanionStatus.Idle;
        }

        private readonly Queue<string> _pendingUtterances = new();
        private bool _isDrainingQueue;
        private string _lastSubmittedUtterance = "";
        private DateTime _lastSubmittedAtUtc = DateTime.MinValue;

        [RelayCommand]
        private void Listen()
        {
            if (IsListening)
            {
                App.Voice.StopContinuousListening();
                IsListening = false;
                App.WakeWord?.Start();
                return;
            }

            App.WakeWord?.Stop();
            var started = App.Voice.StartContinuousListening(
                onUtteranceRecognized: heard => System.Windows.Application.Current?.Dispatcher.Invoke(() => EnqueueHeardUtterance(heard)),
                onStoppedByAnotherListener: () => System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsListening = false;
                    App.WakeWord?.Start();
                }));
            if (!started) App.WakeWord?.Start();
            IsListening = started;
        }

        public void SubmitVoiceUtterance(string heard) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => EnqueueHeardUtterance(heard));

        private void EnqueueHeardUtterance(string heard)
        {
            var normalized = VoiceInputNormalizer.Normalize(heard);
            if (!VoiceInputNormalizer.IsUsable(normalized)) return;

            // Windows continuous recognition can emit the same completed phrase more than once.
            // Suppress only immediate exact duplicates; distinct phrases remain untouched.
            if (VoiceInputNormalizer.IsLikelyDuplicate(normalized, _lastSubmittedUtterance) &&
                DateTime.UtcNow - _lastSubmittedAtUtc < TimeSpan.FromSeconds(2))
                return;

            _lastSubmittedUtterance = normalized;
            _lastSubmittedAtUtc = DateTime.UtcNow;
            _pendingUtterances.Enqueue(normalized);
            if (!_isDrainingQueue) _ = DrainUtteranceQueueAsync();
        }

        private async Task DrainUtteranceQueueAsync()
        {
            _isDrainingQueue = true;
            try
            {
                while (_pendingUtterances.Count > 0)
                {
                    var utterance = _pendingUtterances.Dequeue();
                    await SendCoreAsync(utterance);
                }
            }
            finally { _isDrainingQueue = false; }
        }

        [RelayCommand]
        private void ToggleSpeakReplies() { SpeakRepliesEnabled = !SpeakRepliesEnabled; if (!SpeakRepliesEnabled) App.Voice.StopSpeaking(); }

        [ObservableProperty] private bool _isOrbViewActive;
        [RelayCommand] private void ToggleOrbView() => IsOrbViewActive = !IsOrbViewActive;
        [ObservableProperty] private string _attachStatus = "";

        public void AttachFile(string filePath)
        {
            var (ok, contentOrError) = FileTools.TryReadAsText(filePath);
            if (!ok) { AttachStatus = contentOrError; return; }
            var fileName = System.IO.Path.GetFileName(filePath);
            var block = $"[Attached file: {fileName}]\n{contentOrError}\n\n";
            DraftMessage = string.IsNullOrWhiteSpace(DraftMessage) ? block : block + DraftMessage;
            AttachStatus = $"Attached {fileName}.";
        }

        [RelayCommand]
        private async Task SendAsync()
        {
            if (string.IsNullOrWhiteSpace(DraftMessage) || IsBusy) return;
            var userText = VoiceInputNormalizer.Normalize(DraftMessage);
            if (!VoiceInputNormalizer.IsUsable(userText)) return;
            DraftMessage = "";
            await SendCoreAsync(userText);
        }

        private async Task SendCoreAsync(string userText)
        {
            userText = VoiceInputNormalizer.Normalize(userText);
            if (!VoiceInputNormalizer.IsUsable(userText)) return;
            var userMsg = new ChatMessage { CompanionId = Companion.Id, Role = "user", Content = userText };
            Messages.Add(userMsg);
            App.Memory.AppendMessage(userMsg);
            IsBusy = true;
            Companion.Status = CompanionStatus.Thinking;
            Companion.LastActivitySummary = "Thinking...";

            try
            {
                var historyForClaude = Messages.Count > 1 ? SliceHistory() : new List<ChatMessage>();
                var toolDefinitions = SystemTools.Definitions
                    .Concat(CameraTools.Definitions)
                    .GroupBy(x => x.GetType().GetProperty("name")?.GetValue(x)?.ToString() ?? "")
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .Select(g => g.First())
                    .ToList();
                var toolNames = toolDefinitions
                    .Select(x => x.GetType().GetProperty("name")?.GetValue(x)?.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var authoritativeToolContext =
                    "AUTHORITATIVE AURORA TOOL INVENTORY FOR THIS REQUEST: " + string.Join(", ", toolNames) +
                    ". Never claim to have inspected the source code or an internal registry. " +
                    "If asked which tools are available, use this inventory; do not invent or omit tools. " +
                    "The camera tool is available when `camera` appears in this inventory.";
                var effectiveSystemPrompt = Companion.SystemPrompt + "\n\n" + authoritativeToolContext;

                var reply = await App.AI.SendWithToolsAsync(effectiveSystemPrompt, historyForClaude, userText, toolDefinitions, ExecuteToolAsync);
                var assistantMsg = new ChatMessage { CompanionId = Companion.Id, Role = "assistant", Content = reply };
                Messages.Add(assistantMsg);
                App.Memory.AppendMessage(assistantMsg);
                Companion.LastActivitySummary = Truncate(reply, 60);
                Companion.Status = CompanionStatus.Idle;
                if (SpeakRepliesEnabled) App.Voice.Speak(reply);
            }
            catch (Exception ex)
            {
                Companion.Status = CompanionStatus.Error;
                if (App.Settings.DevModeEnabled)
                {
                    Companion.LastActivitySummary = Truncate($"{ex.GetType().Name}: {ex.Message}", 60);
                    var errMsg = new ChatMessage { CompanionId = Companion.Id, Role = "assistant", Content = $"[dev mode] {ex.GetType().Name}: {ex.Message}" };
                    Messages.Add(errMsg);
                    App.Memory.AppendMessage(errMsg);
                }
                else Companion.LastActivitySummary = "Something went wrong on the last request.";
            }
            finally { IsBusy = false; }
        }

        private static Task<string> ExecuteToolAsync(string toolName, System.Text.Json.JsonElement input) =>
            CameraTools.IsCameraTool(toolName) ? CameraTools.ExecuteAsync(toolName, input) : SystemTools.ExecuteAsync(toolName, input);

        private List<ChatMessage> SliceHistory() { var list = new List<ChatMessage>(Messages); list.RemoveAt(list.Count - 1); return list; }
        private static string Truncate(string s, int len) => s.Length <= len ? s : s.Substring(0, len) + "…";
    }
}
