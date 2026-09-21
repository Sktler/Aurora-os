using System;
using System.Windows;
using System.Windows.Input;
using Aurora.App.Services;

namespace Aurora.App.Views;

public partial class LockdownWindow : Window
{
    private readonly InstallationSecurityService _security;
    private readonly Func<bool> _softwareIntegrityCheck;
    private readonly Func<string, bool> _userRecoveryCheck;
    private readonly Action _unlocked;

    public LockdownWindow(
        InstallationSecurityService security,
        Func<bool> softwareIntegrityCheck,
        Func<string, bool> userRecoveryCheck,
        Action unlocked)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _softwareIntegrityCheck = softwareIntegrityCheck ?? throw new ArgumentNullException(nameof(softwareIntegrityCheck));
        _userRecoveryCheck = userRecoveryCheck ?? throw new ArgumentNullException(nameof(userRecoveryCheck));
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
            if (_security.IsLockedDown)
                e.Cancel = true;
        };
    }

    private void RecoveryCodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Recover_Click(sender, new RoutedEventArgs());
    }

    private void Recover_Click(object sender, RoutedEventArgs e)
    {
        var code = RecoveryCodeBox.Password;
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusText.Text = "Enter a recovery code.";
            return;
        }

        if (!_softwareIntegrityCheck())
        {
            StatusText.Text = "Installation verification failed. Aurora remains locked.";
            RecoveryCodeBox.Clear();
            return;
        }

        try
        {
            var userValid = _userRecoveryCheck(code);
            if (!_security.TryRecover(softwareSignatureValid: true, userRecoveryValid: userValid))
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
