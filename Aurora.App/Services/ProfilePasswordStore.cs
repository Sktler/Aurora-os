using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aurora.App.Services;

public sealed class ProfilePasswordStore
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;

    private sealed record PasswordVerifier(string Salt, string Hash, int Iterations);

    private static string PathFor(string profileId)
    {
        var safeId = new string(profileId.Where(char.IsLetterOrDigit).ToArray());
        return Path.Combine(AppSettings.ConfigDir, "credentials", safeId + ".json");
    }

    public void SetPassword(string profileId, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        try
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            var document = new PasswordVerifier(Convert.ToBase64String(salt), Convert.ToBase64String(hash), Iterations);
            var path = PathFor(profileId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    public bool HasPassword(string profileId) => File.Exists(PathFor(profileId));

    public bool VerifyPassword(string profileId, string password)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(password)) return false;
        try
        {
            var document = JsonSerializer.Deserialize<PasswordVerifier>(File.ReadAllText(PathFor(profileId)));
            if (document == null || document.Iterations < 100_000) return false;
            var salt = Convert.FromBase64String(document.Salt);
            var expected = Convert.FromBase64String(document.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, document.Iterations, HashAlgorithmName.SHA256, HashSize);
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

    public void RemovePassword(string profileId)
    {
        var path = PathFor(profileId);
        if (File.Exists(path)) File.Delete(path);
    }
}
