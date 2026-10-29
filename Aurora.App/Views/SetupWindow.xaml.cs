using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Linq;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    public partial class SetupWindow : Window
    {
        private readonly bool _restartOnSave;
        private string? _generatedRecoveryCode;
        private bool _loadingLanguage;
        public bool KeySaved { get; private set; }

        public SetupWindow(bool restartOnSave = true)
        {
            InitializeComponent();
            DialogLayout.ConstrainToWorkArea(this);
            _restartOnSave = restartOnSave;
            _loadingLanguage = true;
            LanguageBox.ItemsSource = Localizer.SupportedLanguages;
            LanguageBox.SelectedValue = Localizer.NormalizeLanguage(App.Settings.Language);
            _loadingLanguage = false;
            Title = "Aurora - Initial Setup";
            ProfileNameBox.Text = !_restartOnSave ? Localizer.GetUiText("New User") : (string.IsNullOrWhiteSpace(App.Settings.UserName) ? Localizer.GetUiText("New User") : App.Settings.UserName);

            ProviderCombo.ItemsSource = AIProviderCatalog.All;
            var current = AIProviderCatalog.Get(App.Settings.ChatProvider);
            ProviderCombo.SelectedItem = current;

            // The setup footer is identical for every provider and every setup state.
            // Always show Cancel + Save and Continue; never let provider-specific state
            // change the presence or label of either action.
            SkipButton.Content = "Cancel";
            SaveContinueButton.Content = "Save and Continue";
            UpdateProviderText();

            var alreadyConfigured = !string.IsNullOrWhiteSpace(App.Settings.GeminiApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.GroqApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.OpenAIApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.ClaudeApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.GitHubCopilotApiKey);
            if (alreadyConfigured)
                HeaderText.Text = "Aurora Setup";
        }

        private AIProviderInfo SelectedProvider => (AIProviderInfo)ProviderCombo.SelectedItem;

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingLanguage || LanguageBox.SelectedValue is not string code) return;
            var usesDefaultName = string.Equals(ProfileNameBox.Text, Localizer.GetUiText("New User", Localizer.Language), StringComparison.Ordinal);
            App.Settings.Language = Localizer.NormalizeLanguage(code);
            Localizer.SetLanguage(App.Settings.Language);
            if (usesDefaultName) ProfileNameBox.Text = Localizer.GetUiText("New User");
            UpdateProviderText();
        }

        private void UpdateProviderText()
        {
            if (ProviderCombo.SelectedItem is not AIProviderInfo p) return;
            KeyLabel.Text = string.Format(Localizer.Get("Setup.KeyRequired"), p.DisplayName);
            GetKeyButton.ToolTip = string.Format(Localizer.Get("Setup.GetKeyTooltip"), p.DisplayName);
            GetKeyButton.Content = Localizer.GetUiText(p.GetKeyButtonText);
            KeyHintText.Text = Localizer.GetUiText(p.KeyHint);
            CostNoteText.Text = Localizer.GetUiText(p.CostNote);
            ModelExamplesText.Text = string.Format(Localizer.Get("Setup.Examples"), p.ModelExamples);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        private static string GetApiKeyFor(string key) => key switch
        {
            "groq" => App.Settings.GroqApiKey,
            "openai" => App.Settings.OpenAIApiKey,
            "claude" => App.Settings.ClaudeApiKey,
            "copilot" => App.Settings.GitHubCopilotApiKey,
            _ => App.Settings.GeminiApiKey
        };

        private static string GetModelFor(string key) => key switch
        {
            "groq" => App.Settings.GroqModel,
            "openai" => App.Settings.OpenAIModel,
            "claude" => App.Settings.ClaudeModel,
            "copilot" => App.Settings.GitHubCopilotModel,
            _ => App.Settings.GeminiModel
        };

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            if (KeyLabel == null || ProviderCombo.SelectedItem == null) return;

            var p = SelectedProvider;
            ErrorText.Visibility = Visibility.Collapsed;
            ClipboardHint.Visibility = Visibility.Collapsed;

            KeyLabel.Text = string.Format(Localizer.Get("Setup.KeyRequired"), p.DisplayName);
            KeyHintText.Text = Localizer.GetUiText(p.KeyHint);
            GetKeyButton.Content = Localizer.GetUiText(p.GetKeyButtonText);
            GetKeyButton.ToolTip = string.Format(Localizer.Get("Setup.GetKeyTooltip"), p.DisplayName);
            ImageGenNote.Visibility = p.BundlesImageGen ? Visibility.Visible : Visibility.Collapsed;
            CostNoteText.Text = Localizer.GetUiText(p.CostNote);

            ApiKeyBox.Password = GetApiKeyFor(p.Key);
            var existingModel = GetModelFor(p.Key);
            ModelBox.ItemsSource = p.ModelExamples.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            ModelBox.Text = string.IsNullOrWhiteSpace(existingModel) ? p.DefaultModel : existingModel;
            ModelExamplesText.Text = string.Format(Localizer.Get("Setup.Examples"), p.ModelExamples);

            DocsLink.NavigateUri = new Uri(p.DocsUrl);
            RateLimitsLink.NavigateUri = new Uri(p.RateLimitsUrl);
            ModelsLink.NavigateUri = new Uri(p.ModelsUrl);
            PricingLink.NavigateUri = new Uri(p.PricingUrl);

            // Reassert the shared footer state whenever the provider changes.
            SkipButton.Content = "Cancel";
            SaveContinueButton.Content = "Save and Continue";
            SaveContinueButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ErrorText.Text = string.Format(Localizer.Get("Setup.OpenBrowserError"), ex.Message, url);
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink { NavigateUri: not null } link)
                OpenUrl(link.NavigateUri.ToString());
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password)) return;
            if (ProviderCombo.SelectedItem == null) return;

            string? clip;
            try
            {
                clip = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : null;
            }
            catch
            {
                clip = null;
            }

            if (string.IsNullOrWhiteSpace(clip)) return;

            var prefix = SelectedProvider.KeyShapePrefix;
            var looksRight = clip.StartsWith(prefix, StringComparison.Ordinal) &&
                              clip.Length is >= 20 and <= 120 && !clip.Contains(' ');
            if (looksRight)
            {
                ApiKeyBox.Password = clip;
                ClipboardHint.Visibility = Visibility.Visible;
            }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(18);
            try
            {
                const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
                var password = new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
                ProfilePasswordBox.Password = password;
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }

        private void GenerateRecoveryCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var code = AuroraRecoveryService.GenerateCode();
                var normalized = AuroraRecoveryService.NormalizeRecoveryCode(code);
                _generatedRecoveryCode = $"{normalized.Substring(0,8)}-{normalized.Substring(8,8)}-{normalized.Substring(16,8)}-{normalized.Substring(24,8)}";

                var dialog = new Window
                {
                    Owner = this,
                    Title = Localizer.Get("Setup.RecoveryTitle"),
                    Width = 560,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = System.Windows.Media.Brushes.Black
                };
                var panel = new StackPanel { Margin = new Thickness(24, 24, 24, 24) };
                panel.Children.Add(new TextBlock { Text = Localizer.Get("Setup.RecoveryLead"), TextWrapping = TextWrapping.Wrap, FontSize = 18, FontWeight = FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text = Localizer.Get("Setup.RecoveryBody"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 14) });
                var box = new TextBox { Text = _generatedRecoveryCode, IsReadOnly = true, FontSize = 20, Padding = new Thickness(10, 10, 10, 10), HorizontalContentAlignment = HorizontalAlignment.Center };
                panel.Children.Add(box);
                var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
                var copy = new Button { Content = "Copy", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
                var close = new Button { Content = "Done", Padding = new Thickness(14, 8, 14, 8) };
                copy.Click += (_, _) => Clipboard.SetText(_generatedRecoveryCode);
                close.Click += (_, _) => dialog.Close();
                actions.Children.Add(copy);
                actions.Children.Add(close);
                panel.Children.Add(actions);
                dialog.Content = panel;
                DialogLayout.UseContentHeight(dialog);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorText.Text = string.Format(Localizer.Get("Setup.RecoveryError"), ex.Message);
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void GetFreeKey_Click(object sender, RoutedEventArgs e) => OpenUrl(SelectedProvider.GetKeyUrl);

        private bool _checkingKey;

        private async void ApiKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var key = ApiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(key) || ProviderCombo.SelectedItem == null) return;
            if (_checkingKey) return;

            var p = SelectedProvider;
            _checkingKey = true;
            ModelBox.IsEnabled = false;
            ModelExamplesText.Text = string.Format(Localizer.Get("Setup.CheckingKey"), p.DisplayName);
            try
            {
                IChatEngine client = p.Key switch
                {
                    "groq" => new GroqClient(key, p.DefaultModel),
                    "openai" => new OpenAIClient(key, p.DefaultModel),
                    "claude" => new ClaudeClient(key, p.DefaultModel),
                    "copilot" => new GitHubCopilotClient(key, p.DefaultModel),
                    _ => new GeminiClient(key, p.DefaultModel)
                };
                var models = await client.ListModelsAsync();
                if (models.Count > 0)
                {
                    ModelBox.ItemsSource = models;
                    ModelExamplesText.Text = string.Format(Localizer.Get("Setup.ModelsLoaded"), models.Count, p.DisplayName);
                }
                else
                {
                    ModelExamplesText.Text = string.Format(Localizer.Get("Setup.Examples"), p.ModelExamples);
                }
            }
            catch
            {
                ModelExamplesText.Text = string.Format(Localizer.Get("Setup.Examples"), p.ModelExamples);
            }
            finally
            {
                ModelBox.IsEnabled = true;
                _checkingKey = false;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyBox.Password.Trim();
            var p = SelectedProvider;
            var profileName = ProfileNameBox.Text.Trim();
            var profilePassword = ProfilePasswordBox.Password;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                ErrorText.Text = Localizer.Get("Setup.ProfileNameRequired");
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrWhiteSpace(profilePassword))
            {
                ErrorText.Text = Localizer.Get("Setup.ProfilePasswordRequired");
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorText.Text = string.Format(Localizer.Get("Setup.ApiKeyRequired"), p.DisplayName);
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var model = string.IsNullOrWhiteSpace(ModelBox.Text) ? p.DefaultModel : ModelBox.Text.Trim();
            if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                model = model["models/".Length..];

            switch (p.Key)
            {
                case "groq":
                    App.Settings.GroqApiKey = apiKey;
                    App.Settings.GroqModel = model;
                    break;
                case "openai":
                    App.Settings.OpenAIApiKey = apiKey;
                    App.Settings.OpenAIModel = model;
                    break;
                case "claude":
                    App.Settings.ClaudeApiKey = apiKey;
                    App.Settings.ClaudeModel = model;
                    break;
                case "copilot":
                    App.Settings.GitHubCopilotApiKey = apiKey;
                    App.Settings.GitHubCopilotModel = model;
                    break;
                default:
                    App.Settings.GeminiApiKey = apiKey;
                    App.Settings.GeminiModel = model;
                    App.Settings.ImageProvider = "gemini";
                    break;
            }

            App.Settings.ChatProvider = p.Key;
            App.Settings.UserName = profileName;
            App.Profiles.SaveActiveSettings();
            new ProfilePasswordStore().SetPassword(App.Profiles.ActiveProfile.Id, profilePassword);
            if (!string.IsNullOrWhiteSpace(_generatedRecoveryCode))
                new ProfileRecoveryCodeStore().SetCode(App.Profiles.ActiveProfile.Id, _generatedRecoveryCode);
            App.Settings.Save();
            KeySaved = true;
            if (_restartOnSave)
                RestartApp();
            else
                DialogResult = true;
        }

        private static void RestartApp()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                }
                catch
                {
                }
            }
            Environment.Exit(0);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            KeySaved = false;
            Close();
        }
    }
}
