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
    private readonly Func<bool> _recoveryCodeAvailable;
    private readonly Action _unlocked;
    private readonly Rect _workArea;

    public LockdownWindow(
        InstallationSecurityService security,
        Func<bool> softwareIntegrityCheck,
        Func<string, bool> userRecoveryCheck,
        Func<bool> recoveryCodeAvailable,
        Action unlocked,
        Rect? workArea = null)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _softwareIntegrityCheck = softwareIntegrityCheck ?? throw new ArgumentNullException(nameof(softwareIntegrityCheck));
        _userRecoveryCheck = userRecoveryCheck ?? throw new ArgumentNullException(nameof(userRecoveryCheck));
        _recoveryCodeAvailable = recoveryCodeAvailable ?? throw new ArgumentNullException(nameof(recoveryCodeAvailable));
        _unlocked = unlocked ?? throw new ArgumentNullException(nameof(unlocked));
        _workArea = workArea ?? SystemParameters.WorkArea;

        InitializeComponent();
        UpdateRecoveryHint();
        Loaded += (_, _) =>
        {
            Left = _workArea.Left + Math.Max(0, (_workArea.Width - Width) / 2);
            Top = _workArea.Top + Math.Max(0, (_workArea.Height - Height) / 2);
            Activate();
            RecoveryCodeBox.Focus();
        };
        Closing += (_, e) =>
        {
            if (_security.IsLockedDown)
                e.Cancel = true;
        };
    }

    private void UpdateRecoveryHint()
    {
        RecoveryCodeDisplay.Text = _recoveryCodeAvailable()
            ? "Enter the recovery code saved for the active profile."
            : "No recovery code is saved for the active profile. Aurora remains locked until one is created and confirmed.";
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
