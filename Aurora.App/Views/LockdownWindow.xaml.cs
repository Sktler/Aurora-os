using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Aurora.App.Services;

namespace Aurora.App.Views;

public partial class LockdownWindow : Window
{
    private readonly InstallationSecurityService _security;
    private readonly Func<string, bool> _softwareIntegrityCheck;
    private readonly Action _unlocked;

    public LockdownWindow(
        InstallationSecurityService security,
        Func<string, bool> softwareIntegrityCheck,
        Action unlocked)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _softwareIntegrityCheck = softwareIntegrityCheck ?? throw new ArgumentNullException(nameof(softwareIntegrityCheck));
        _unlocked = unlocked ?? throw new ArgumentNullException(nameof(unlocked));

        InitializeComponent();
        Loaded += (_, _) =>
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Activate();
            RecoveryCodeBox.Focus();
        };
        Closing += (_, e) =>
        {
            // The overlay may only close after the security state has been cleared.
            if (_security.IsLockedDown)
                e.Cancel = true;
        };
    }

    private void RecoveryCodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Recover_Click(sender, new RoutedEventArgs());
        }
    }

    private void Recover_Click(object sender, RoutedEventArgs e)
    {
        var code = RecoveryCodeBox.Password;
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusText.Text = "Enter a recovery code.";
            return;
        }

        // The software check is deliberately supplied by the caller. A UI action alone
        // can never clear lockdown; both checks must pass through TryRecover.
        var softwareValid = _softwareIntegrityCheck(code);
        if (!softwareValid)
        {
            StatusText.Text = "Installation verification failed. Aurora remains locked.";
            RecoveryCodeBox.Clear();
            return;
        }

        try
        {
            var userValid = App.Profiles != null &&
                            App.Profiles.VerifyRecoveryCode(code);
            if (!_security.TryRecover(softwareValid, userValid))
            {
                StatusText.Text = "Recovery code rejected. Aurora remains locked.";
                RecoveryCodeBox.Clear();
                return;
            }

            _unlocked();
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Recovery failed: {ex.Message}";
            RecoveryCodeBox.Clear();
        }
    }
}
