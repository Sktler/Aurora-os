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
    private readonly string _storageDirectory;

    private sealed record RecoveryVerifier(string Salt, string Hash, int Iterations);

    public ProfileRecoveryCodeStore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? RecoveryStorageSettings.DirectoryPath;
    }

    private string PathFor(string profileId)
    {
        var profileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profileId)));
        return Path.Combine(_storageDirectory, profileHash + ".json");
    }

    private string LegacyPathFor(string profileId)
    {
        var safeId = new string(profileId.Where(char.IsLetterOrDigit).ToArray());
        return Path.Combine(_storageDirectory, safeId + ".json");
    }

    public static void MoveExistingData(string sourceDirectory, string destinationDirectory)
    {
        var source = Path.GetFullPath(sourceDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(source)) return;

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target))
                throw new IOException($"A recovery verifier already exists at '{target}'.");
        }

        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.TopDirectoryOnly))
            File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
    }

    public void SetCode(string profileId, string recoveryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryCode);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[]? hash = null;
        try
        {
            hash = Derive(recoveryCode, salt);
            var document = new RecoveryVerifier(Convert.ToBase64String(salt), Convert.ToBase64String(hash), Iterations);
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
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            if (hash is not null) CryptographicOperations.ZeroMemory(hash);
        }
    }

    public bool HasCode(string profileId) =>
        !string.IsNullOrWhiteSpace(profileId) &&
        (File.Exists(PathFor(profileId)) || File.Exists(LegacyPathFor(profileId)));

    public bool VerifyCode(string profileId, string recoveryCode)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(recoveryCode)) return false;
        try
        {
            var verifierPath = PathFor(profileId);
            var isLegacyVerifier = !File.Exists(verifierPath);
            if (isLegacyVerifier) verifierPath = LegacyPathFor(profileId);
            var document = JsonSerializer.Deserialize<RecoveryVerifier>(File.ReadAllText(verifierPath));
            if (document == null || document.Iterations != Iterations) return false;
            var salt = Convert.FromBase64String(document.Salt);
            var expected = Convert.FromBase64String(document.Hash);
            if (salt.Length != SaltSize || expected.Length != HashSize)
            {
                CryptographicOperations.ZeroMemory(salt);
                CryptographicOperations.ZeroMemory(expected);
                return false;
            }
            var actual = Derive(recoveryCode, salt, document.Iterations);
            try
            {
                var verified = CryptographicOperations.FixedTimeEquals(actual, expected);
                if (verified && isLegacyVerifier)
                {
                    SetCode(profileId, recoveryCode);
                    File.Delete(verifierPath);
                }
                return verified;
            }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        var path = PathFor(profileId);
        if (File.Exists(path)) File.Delete(path);
        var legacyPath = LegacyPathFor(profileId);
        if (File.Exists(legacyPath)) File.Delete(legacyPath);
    }

    private static byte[] Derive(string code, byte[] salt, int iterations = Iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(AuroraRecoveryService.NormalizeRecoveryCode(code), salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
