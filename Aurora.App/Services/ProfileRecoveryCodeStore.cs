using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aurora.App.Services;

public sealed class ProfileRecoveryCodeStore
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;

    private sealed record RecoveryVerifier(string Salt, string Hash, int Iterations);

    private static string PathFor(string profileId)
    {
        var safeId = new string(profileId.Where(char.IsLetterOrDigit).ToArray());
        return System.IO.Path.Combine(AppSettings.ConfigDir, "recovery", safeId + ".json");
    }

    public void SetCode(string profileId, string recoveryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryCode);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        try
        {
            var hash = Derive(recoveryCode, salt);
            var document = new RecoveryVerifier(Convert.ToBase64String(salt), Convert.ToBase64String(hash), Iterations);
            var path = PathFor(profileId);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(document), new UTF8Encoding(false));
                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            CryptographicOperations.ZeroMemory(hash);
        }
        finally { CryptographicOperations.ZeroMemory(salt); }
    }

    public bool HasCode(string profileId) => File.Exists(PathFor(profileId));

    public bool VerifyCode(string profileId, string recoveryCode)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(recoveryCode)) return false;
        try
        {
            var document = JsonSerializer.Deserialize<RecoveryVerifier>(File.ReadAllText(PathFor(profileId)));
            if (document == null || document.Iterations < 100_000) return false;
            var salt = Convert.FromBase64String(document.Salt);
            var expected = Convert.FromBase64String(document.Hash);
            var actual = Derive(recoveryCode, salt, document.Iterations);
            try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
                CryptographicOperations.ZeroMemory(salt);
                CryptographicOperations.ZeroMemory(expected);
            }
        }
        catch { return false; }
    }

    public void RemoveCode(string profileId)
    {
        var path = PathFor(profileId);
        if (File.Exists(path)) File.Delete(path);
    }

    private static byte[] Derive(string code, byte[] salt, int iterations = Iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(AuroraRecoveryService.NormalizeRecoveryCode(code), salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
