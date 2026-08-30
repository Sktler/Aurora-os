using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
{
    public partial class SetupWindow : Window
    {
        public bool KeySaved { get; private set; }

        public SetupWindow()
        {
            InitializeComponent();

            ProviderCombo.ItemsSource = AIProviderCatalog.All;
            var current = AIProviderCatalog.Get(App.Settings.ChatProvider);
            ProviderCombo.SelectedItem = current;

            // The setup footer is identical for every provider and every setup state.
            // Always show Cancel + Save and Continue; never let provider-specific state
            // change the presence or label of either action.
            SkipButton.Content = "Cancel";
            SaveContinueButton.Content = "Save and Continue";

            var alreadyConfigured = !string.IsNullOrWhiteSpace(App.Settings.GeminiApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.GroqApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.OpenAIApiKey) ||
                                     !string.IsNullOrWhiteSpace(App.Settings.ClaudeApiKey);
            if (alreadyConfigured)
                HeaderText.Text = "Aurora Setup";
        }

        private AIProviderInfo SelectedProvider => (AIProviderInfo)ProviderCombo.SelectedItem;

        private static string GetApiKeyFor(string key) => key switch
        {
            "groq" => App.Settings.GroqApiKey,
            "openai" => App.Settings.OpenAIApiKey,
            "claude" => App.Settings.ClaudeApiKey,
            _ => App.Settings.GeminiApiKey
        };

        private static string GetModelFor(string key) => key switch
        {
            "groq" => App.Settings.GroqModel,
            "openai" => App.Settings.OpenAIModel,
            "claude" => App.Settings.ClaudeModel,
            _ => App.Settings.GeminiModel
        };

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            if (KeyLabel == null || ProviderCombo.SelectedItem == null) return;

            var p = SelectedProvider;
            ErrorText.Visibility = Visibility.Collapsed;
            ClipboardHint.Visibility = Visibility.Collapsed;

            KeyLabel.Text = $"{p.DisplayName} API key (required)";
            KeyHintText.Text = p.KeyHint;
            GetKeyButton.Content = p.GetKeyButtonText;
            GetKeyButton.ToolTip = $"Opens {p.DisplayName}'s key page in your browser so you can sign in and create a key";
            ImageGenNote.Visibility = p.BundlesImageGen ? Visibility.Visible : Visibility.Collapsed;
            CostNoteText.Text = p.CostNote;

            ApiKeyBox.Password = GetApiKeyFor(p.Key);
            var existingModel = GetModelFor(p.Key);
            ModelBox.ItemsSource = p.ModelExamples.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            ModelBox.Text = string.IsNullOrWhiteSpace(existingModel) ? p.DefaultModel : existingModel;
            ModelExamplesText.Text = $"Examples: {p.ModelExamples}";

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
                ErrorText.Text = $"Couldn't open the browser automatically ({ex.Message}). Go to {url} manually.";
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
            ModelExamplesText.Text = $"Checking your {p.DisplayName} key...";
            try
            {
                IChatEngine client = p.Key switch
                {
                    "groq" => new GroqClient(key, p.DefaultModel),
                    "openai" => new OpenAIClient(key, p.DefaultModel),
                    "claude" => new ClaudeClient(key, p.DefaultModel),
                    _ => new GeminiClient(key, p.DefaultModel)
                };
                var models = await client.ListModelsAsync();
                if (models.Count > 0)
                {
                    ModelBox.ItemsSource = models;
                    ModelExamplesText.Text = $"{models.Count} models loaded live from {p.DisplayName}.";
                }
                else
                {
                    ModelExamplesText.Text = $"Examples: {p.ModelExamples}";
                }
            }
            catch
            {
                ModelExamplesText.Text = $"Examples: {p.ModelExamples}";
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

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorText.Text = $"A {p.DisplayName} API key is required to bring your companions online. Use \"Cancel\" if you'd rather add it later.";
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
                default:
                    App.Settings.GeminiApiKey = apiKey;
                    App.Settings.GeminiModel = model;
                    App.Settings.ImageProvider = "gemini";
                    break;
            }

            App.Settings.ChatProvider = p.Key;
            App.Settings.Save();
            KeySaved = true;
            RestartApp();
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
