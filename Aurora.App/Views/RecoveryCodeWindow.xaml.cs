using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace Aurora.App.Views;

public partial class RecoveryCodeWindow : Window
{
    private readonly Aurora.App.Services.ProfileRecoveryCodeStore _store = new();
    private string? _plainCode;

    public RecoveryCodeWindow()
    {
        InitializeComponent();
        ProfileLabel.Text = $"Profile: {App.Profiles.ActiveProfile.DisplayName}";
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        _plainCode = FormatCode(Aurora.App.Services.AuroraRecoveryService.GenerateCode());
        RecoveryCodeBox.Text = _plainCode;
        CopyButton.IsEnabled = SaveButton.IsEnabled = PrintButton.IsEnabled = true;
        SavedCheckBox.IsChecked = false;
        DoneButton.IsEnabled = false;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_plainCode != null) Clipboard.SetText(_plainCode);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_plainCode == null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Aurora recovery code",
            FileName = "Aurora-Recovery-Code.txt",
            Filter = "Text file (*.txt)|*.txt",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) == true)
            File.WriteAllText(dialog.FileName, $"Aurora profile recovery code{Environment.NewLine}{Environment.NewLine}{_plainCode}{Environment.NewLine}", new System.Text.UTF8Encoding(false));
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_plainCode == null) return;
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() != true) return;
        var document = new FlowDocument(new Paragraph(new Run($"Aurora profile recovery code{Environment.NewLine}{Environment.NewLine}{_plainCode}")))
        { PagePadding = new Thickness(60, 60, 60, 60), FontSize = 18 };
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Aurora Recovery Code");
    }

    private void SavedCheckBox_Checked(object sender, RoutedEventArgs e) =>
        DoneButton.IsEnabled = _plainCode != null && SavedCheckBox.IsChecked == true;

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        if (_plainCode == null || SavedCheckBox.IsChecked != true) return;
        try
        {
            _store.SetCode(App.Profiles.ActiveProfile.Id, _plainCode);
            ClearPlaintext();
            MessageBox.Show(this, "Your recovery code is now protected for this profile. Aurora does not store the plaintext code.",
                "Recovery code saved", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Recovery code", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { ClearPlaintext(); Close(); }

    protected override void OnClosed(EventArgs e) { ClearPlaintext(); base.OnClosed(e); }

    private void ClearPlaintext()
    {
        _plainCode = null;
        RecoveryCodeBox.Clear();
        CopyButton.IsEnabled = SaveButton.IsEnabled = PrintButton.IsEnabled = false;
        SavedCheckBox.IsChecked = false;
        DoneButton.IsEnabled = false;
    }

    private static string FormatCode(string code)
    {
        var normalized = Aurora.App.Services.AuroraRecoveryService.NormalizeRecoveryCode(code);
        return $"{normalized.Substring(0,8)}-{normalized.Substring(8,8)}-{normalized.Substring(16,8)}-{normalized.Substring(24,8)}";
    }
}
