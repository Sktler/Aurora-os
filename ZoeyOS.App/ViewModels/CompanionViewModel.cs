using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private readonly Queue<string> _pendingUtterances = new();
        private bool _isDrainingQueue;

        [RelayCommand]
        private void Listen()
        {
            if (IsListening) { App.Voice.StopContinuousListening(); IsListening = false; return; }
            var started = App.Voice.StartContinuousListening(
                onUtteranceRecognized: heard => System.Windows.Application.Current?.Dispatcher.Invoke(() => EnqueueHeardUtterance(heard)),
                onStoppedByAnotherListener: () => System.Windows.Application.Current?.Dispatcher.Invoke(() => IsListening = false));
            IsListening = started;
        }

        private void EnqueueHeardUtterance(string heard)
        {
            var trimmed = heard.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return;
            _pendingUtterances.Enqueue(trimmed);
            if (!_isDrainingQueue) _ = DrainUtteranceQueueAsync();
        }

        private async Task DrainUtteranceQueueAsync()
        {
            _isDrainingQueue = true;
            try { while (_pendingUtterances.Count > 0) await SendCoreAsync(_pendingUtterances.Dequeue()); }
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
            var userText = DraftMessage.Trim();
            DraftMessage = "";
            await SendCoreAsync(userText);
        }

        private async Task SendCoreAsync(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText)) return;
            var userMsg = new ChatMessage { CompanionId = Companion.Id, Role = "user", Content = userText };
            Messages.Add(userMsg);
            App.Memory.AppendMessage(userMsg);
            IsBusy = true;
            Companion.Status = CompanionStatus.Thinking;
            Companion.LastActivitySummary = "Thinking...";

            try
            {
                var historyForClaude = Messages.Count > 1 ? SliceHistory() : new List<ChatMessage>();
                // General companions receive SystemTools so the model can directly inspect and
                // control Windows media sessions. This is intentionally separate from Jamendo:
                // the model controls whatever Windows reports as the current media session.
                var reply = await App.AI.SendWithToolsAsync(
                    Companion.SystemPrompt,
                    historyForClaude,
                    userText,
                    SystemTools.Definitions,
                    SystemTools.ExecuteAsync);

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

        private List<ChatMessage> SliceHistory() { var list = new List<ChatMessage>(Messages); list.RemoveAt(list.Count - 1); return list; }
        private static string Truncate(string s, int len) => s.Length <= len ? s : s.Substring(0, len) + "…";
    }
}
