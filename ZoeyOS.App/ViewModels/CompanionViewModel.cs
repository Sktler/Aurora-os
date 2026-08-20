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

        [ObservableProperty]
        private string _draftMessage = "";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isRenaming;

        [ObservableProperty]
        private string _renameDraft = "";

        [ObservableProperty]
        private bool _isListening;

        [ObservableProperty]
        private bool _speakRepliesEnabled = App.Settings.SpeakRepliesByDefault;

        public CompanionViewModel(Companion companion)
        {
            Companion = companion;
            foreach (var m in App.Memory.LoadHistory(companion.Id))
                Messages.Add(m);
        }

        [RelayCommand]
        private void BeginRename()
        {
            RenameDraft = Companion.Name;
            IsRenaming = true;
        }

        [RelayCommand]
        private void CommitRename()
        {
            var trimmed = RenameDraft.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                Companion.Name = trimmed;
                App.Memory.SaveCompanion(Companion);
            }
            IsRenaming = false;
        }

        [RelayCommand]
        private void CancelRename()
        {
            IsRenaming = false;
        }

        // Utterances recognized while a previous reply is still being generated queue up
        // here instead of being dropped or overlapping - SendCoreAsync already serializes
        // via IsBusy, so this loop only ever has one send in flight at a time.
        private readonly Queue<string> _pendingUtterances = new();
        private bool _isDrainingQueue;

        [RelayCommand]
        private void Listen()
        {
            if (IsListening)
            {
                App.Voice.StopContinuousListening();
                IsListening = false;
                return;
            }

            var started = App.Voice.StartContinuousListening(
                onUtteranceRecognized: heard =>
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => EnqueueHeardUtterance(heard)),
                onStoppedByAnotherListener: () =>
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => IsListening = false));

            IsListening = started;
        }

        private void EnqueueHeardUtterance(string heard)
        {
            var trimmed = heard.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return;

            _pendingUtterances.Enqueue(trimmed);
            if (_isDrainingQueue) return;

            _ = DrainUtteranceQueueAsync();
        }

        private async Task DrainUtteranceQueueAsync()
        {
            _isDrainingQueue = true;
            try
            {
                while (_pendingUtterances.Count > 0)
                {
                    var next = _pendingUtterances.Dequeue();
                    await SendCoreAsync(next);
                }
            }
            finally
            {
                _isDrainingQueue = false;
            }
        }

        [RelayCommand]
        private void ToggleSpeakReplies()
        {
            SpeakRepliesEnabled = !SpeakRepliesEnabled;
            if (!SpeakRepliesEnabled)
                App.Voice.StopSpeaking();
        }

        // Swaps the message list for the big particle-wave orb view, in place - stays within
        // the main window rather than opening a separate one.
        [ObservableProperty]
        private bool _isOrbViewActive;

        [RelayCommand]
        private void ToggleOrbView() => IsOrbViewActive = !IsOrbViewActive;

        [ObservableProperty]
        private string _attachStatus = "";

        /// <summary>Called after the user picks a file via the composer's attach button.
        /// Folds the file's text content into the draft message so it's sent along with
        /// whatever the user types next - the model never browses to it on its own.</summary>
        public void AttachFile(string filePath)
        {
            var (ok, contentOrError) = FileTools.TryReadAsText(filePath);
            if (!ok)
            {
                AttachStatus = contentOrError;
                return;
            }

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

        /// <summary>The actual send-and-get-a-reply logic, shared by the Send button/Enter
        /// key and by continuous voice mode's auto-send - both end up here so a spoken
        /// message gets identical handling (tools, memory, speak-back) to a typed one.</summary>
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
                var historyForClaude = Messages.Count > 1 ? SliceHistory() : new System.Collections.Generic.List<ChatMessage>();

                var reply = Companion.ToolAccess switch
                {
                    CompanionToolAccess.HomeAutomation => await App.AI.SendWithToolsAsync(Companion.SystemPrompt, historyForClaude, userText,
                        HomeTools.Definitions, HomeTools.ExecuteAsync),
                    CompanionToolAccess.InboxDocuments => await App.AI.SendWithToolsAsync(Companion.SystemPrompt, historyForClaude, userText,
                        FileTools.Definitions, FileTools.ExecuteAsync),
                    CompanionToolAccess.General => await App.AI.SendWithToolsAsync(Companion.SystemPrompt, historyForClaude, userText,
                        SystemTools.Definitions, SystemTools.ExecuteAsync),
                    _ => await App.AI.SendAsync(Companion.SystemPrompt, historyForClaude, userText)
                };

                var assistantMsg = new ChatMessage { CompanionId = Companion.Id, Role = "assistant", Content = reply };
                Messages.Add(assistantMsg);
                App.Memory.AppendMessage(assistantMsg);

                Companion.LastActivitySummary = Truncate(reply, 60);
                Companion.Status = CompanionStatus.Idle;

                if (SpeakRepliesEnabled)
                    App.Voice.Speak(reply);
            }
            catch (Exception ex)
            {
                Companion.Status = CompanionStatus.Error;

                if (App.Settings.DevModeEnabled)
                {
                    Companion.LastActivitySummary = Truncate($"{ex.GetType().Name}: {ex.Message}", 60);
                    var errMsg = new ChatMessage
                    {
                        CompanionId = Companion.Id,
                        Role = "assistant",
                        Content = $"[dev mode] {ex.GetType().Name}: {ex.Message}"
                    };
                    Messages.Add(errMsg);
                    App.Memory.AppendMessage(errMsg);
                }
                else
                {
                    Companion.LastActivitySummary = "Something went wrong on the last request.";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private System.Collections.Generic.List<ChatMessage> SliceHistory()
        {
            // everything except the message we just added (it's passed separately as newUserMessage)
            var list = new System.Collections.Generic.List<ChatMessage>(Messages);
            list.RemoveAt(list.Count - 1);
            return list;
        }

        private static string Truncate(string s, int len) =>
            s.Length <= len ? s : s.Substring(0, len) + "…";
    }
}
