using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Services;
using Aurora.App.ViewModels;

namespace Aurora.App.Views;

public partial class ProviderSettingsView : UserControl
{
    private bool _updatingKey;
    public ProviderSettingsViewModel ViewModel { get; }
    public ProviderSettingsView()
    {
        InitializeComponent();
        ViewModel = new ProviderSettingsViewModel(App.Settings);
        DataContext = ViewModel;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.ApiKey)) UpdateKey();
        };
        UpdateKey();
        Loaded += (_, _) => Localizer.LanguageChanged += LanguageChanged;
        Unloaded += (_, _) => Localizer.LanguageChanged -= LanguageChanged;
    }
    private void LanguageChanged(object? sender, EventArgs e) => ViewModel.RefreshLabels();
    private void UpdateKey()
    {
        _updatingKey = true;
        ApiKeyBox.Password = ViewModel.ApiKey;
        _updatingKey = false;
    }
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_updatingKey && ViewModel != null) ViewModel.ApiKey = ApiKeyBox.Password;
    }
    private async void ApiKeyBox_LostFocus(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.Save(refreshClients: true); }
        catch (Exception ex) { ViewModel.Status = Localizer.FormatUiText("Could not save settings: {0}", ex.Message); }
    }
    private void GetKey_Click(object sender, RoutedEventArgs e) => Open(ViewModel.Provider?.GetKeyUrl);
    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        var provider = ViewModel.Provider;
        Open(((sender as Button)?.Tag as string) switch
        {
            "DocsUrl" => provider?.DocsUrl, "RateLimitsUrl" => provider?.RateLimitsUrl,
            "ModelsUrl" => provider?.ModelsUrl, "PricingUrl" => provider?.PricingUrl, _ => null
        });
    }
    private void Open(string? url)
    {
        if (url == null) return;
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { ViewModel.Status = Localizer.FormatUiText("Could not open the browser: {0}", ex.Message); }
    }
}
