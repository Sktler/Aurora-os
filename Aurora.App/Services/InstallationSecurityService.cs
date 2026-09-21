using System.Security.Cryptography;

namespace Aurora.App.Services;

/// <summary>
/// Small fail-closed state machine for installation integrity. It does not attempt to
/// decide whether a machine is compromised; callers provide the result of their
/// integrity/signature checks.
/// </summary>
public sealed class InstallationSecurityService
{
    public InstallationSecurityService(string statePath)
    {
        StatePath = Path.GetFullPath(statePath);
    }

    public string StatePath { get; }

    public bool IsLockedDown => File.Exists(StatePath);

    public void EnterLockdown(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);

        var content = $"{DateTimeOffset.UtcNow:O}\n{reason.Trim()}\n";
        var temp = StatePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, StatePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public bool TryRecover(bool softwareSignatureValid, bool userRecoveryValid)
    {
        if (!softwareSignatureValid || !userRecoveryValid)
            return false;

        try
        {
            if (File.Exists(StatePath))
                File.Delete(StatePath);
            return !File.Exists(StatePath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool VerifyFileSha256(string path, string expectedHex)
    {
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(expectedHex))
            return false;

        var normalized = expectedHex.Trim();
        if (normalized.Length != 64 || normalized.Any(c => !Uri.IsHexDigit(c)))
            return false;

        using var stream = File.OpenRead(path);
        var actual = SHA256.HashData(stream);
        return CryptographicOperations.FixedTimeEquals(
            actual,
            Convert.FromHexString(normalized));
    }
}
