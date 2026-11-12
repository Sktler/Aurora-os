using System.Windows;
using System.Windows.Controls;
using Aurora.App.Services;

namespace Aurora.App.Views;

/// <summary>The same persisted language choice in System and the pre-setup welcome screen.</summary>
public sealed class LanguagePicker : ComboBox
{
    public LanguagePicker()
    {
        SetResourceReference(StyleProperty, typeof(ComboBox));
        ItemsSource = Localizer.SupportedLanguages;
        DisplayMemberPath = nameof(AppLanguage.NativeName);
        SelectedValuePath = nameof(AppLanguage.Code);
        SelectedValue = Localizer.NormalizeLanguage(App.Settings.Language);
        MaxDropDownHeight = 360;
        System.Windows.Automation.AutomationProperties.SetName(this, Localizer.GetUiText("Interface language"));
        SelectionChanged += (_, _) =>
        {
            if (SelectedValue is not string code) return;
            App.Settings.Language = Localizer.NormalizeLanguage(code);
            // First-run selection must not prematurely mark provider setup complete.
            if (AppSettings.HasSavedConfiguration) App.Settings.Save();
            Localizer.SetLanguage(code);
            if (App.Voice?.IsListening == true) App.Voice.RestartContinuousListening();
            else { App.WakeWord?.Stop(); App.RefreshVoiceActivation(); }
        };
    }
}
