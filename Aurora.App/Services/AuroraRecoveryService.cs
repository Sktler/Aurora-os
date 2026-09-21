using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aurora.App.Services;

/// <summary>
/// Provides local recovery credentials, encrypted profile snapshots, detached-signature
/// verification, and fail-closed installation state. The service deliberately does not
/// contain or generate Aurora's release-signing private key.
/// </summary>
public sealed class AuroraRecoveryService
{
    public const int MinimumRecoveryCodeLength = 26;
    public const int DefaultRecoveryCodeLength = 32;
    public const int MaximumRecoveryCodeLength = 128;
    public const int DefaultRecoveryCodeCount = 5;

    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int KeySize = 32;
    private const int TagSize = 16;
    private const int Pbkdf2Iterations = 600_000;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string SnapshotMagic = "AURORA-RECOVERY-1";

    public static IReadOnlyList<string> GenerateRecoveryCodes(
        int count = DefaultRecoveryCodeCount,
        int length = DefaultRecoveryCodeLength)
    {
        if (count is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(count), "Recovery code count must be between 1 and 100.");
        if (length is < MinimumRecoveryCodeLength or > MaximumRecoveryCodeLength)
            throw new ArgumentOutOfRangeException(nameof(length), $"Recovery code length must be between {MinimumRecoveryCodeLength} and {MaximumRecoveryCodeLength}.");

        var codes = new List<string>(count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (codes.Count < count)
        {
            var code = GenerateCode(length);
            if (seen.Add(code))
                codes.Add(code);
        }

        return codes;
    }

    public static bool ValidateRecoveryCodeConfiguration(int count, int length)
    {
        return count is >= 1 and <= 100 &&
               length is >= MinimumRecoveryCodeLength and <= MaximumRecoveryCodeLength;
    }

    public static bool VerifyRecoveryCode(string code, IEnumerable<string> validCodes)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var candidate = NormalizeRecoveryCode(code);
        foreach (var validCode in validCodes)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(candidate),
                    Encoding.UTF8.GetBytes(NormalizeRecoveryCode(validCode))))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates an encrypted snapshot. Each recovery code receives its own encrypted
    /// envelope around the same random snapshot key, so revoking one code does not
    /// require re-encrypting the profile payload.
    /// </summary>
    public static void CreateEncryptedSnapshot(
        string outputPath,
        string profileId,
        string profilePayload,
        IReadOnlyCollection<string> recoveryCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(profilePayload);
        ArgumentNullException.ThrowIfNull(recoveryCodes);

        if (recoveryCodes.Count == 0)
            throw new ArgumentException("At least one recovery code is required.", nameof(recoveryCodes));

        var normalizedCodes = recoveryCodes
            .Select(NormalizeRecoveryCode)
            .Where(c => c.Length >= MinimumRecoveryCodeLength)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length != recoveryCodes.Count)
            throw new ArgumentException("Recovery codes must be unique and meet the minimum length.", nameof(recoveryCodes));

        var snapshotKey = RandomNumberGenerator.GetBytes(KeySize);
        var payloadBytes = Encoding.UTF8.GetBytes(profilePayload);
        var payloadNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var payloadCiphertext = new byte[payloadBytes.Length];
        var payloadTag = new byte[TagSize];

        using (var payloadAes = new AesGcm(snapshotKey, TagSize))
        {
            payloadAes.Encrypt(payloadNonce, payloadBytes, payloadCiphertext, payloadTag,
                Encoding.UTF8.GetBytes($"{SnapshotMagic}|{profileId}"));
        }

        var envelopes = new List<RecoveryEnvelope>(normalizedCodes.Length);
        foreach (var code in normalizedCodes)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var kek = DeriveKey(code, salt);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var wrappedKey = new byte[snapshotKey.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(kek, TagSize))
            {
                aes.Encrypt(nonce, snapshotKey, wrappedKey, tag,
                    Encoding.UTF8.GetBytes($"{SnapshotMagic}|{profileId}|key"));
            }

            CryptographicOperations.ZeroMemory(kek);
            envelopes.Add(new RecoveryEnvelope(
                Convert.ToBase64String(salt),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(wrappedKey),
                Convert.ToBase64String(tag)));
        }

        var document = new RecoverySnapshot(
            SnapshotMagic,
            profileId,
            DateTimeOffset.UtcNow,
            Convert.ToBase64String(payloadNonce),
            Convert.ToBase64String(payloadCiphertext),
            Convert.ToBase64String(payloadTag),
            envelopes);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var temporaryPath = outputPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            CryptographicOperations.ZeroMemory(snapshotKey);
        }
    }

    public static string RestoreEncryptedSnapshot(
        string snapshotPath,
        string recoveryCode,
        string expectedProfileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProfileId);

        var document = JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(snapshotPath))
            ?? throw new InvalidDataException("Recovery snapshot is empty or malformed.");

        if (!string.Equals(document.Magic, SnapshotMagic, StringComparison.Ordinal) ||
            !string.Equals(document.ProfileId, expectedProfileId, StringComparison.Ordinal))
            throw new InvalidDataException("Recovery snapshot does not belong to the requested Aurora profile.");

        var associatedData = Encoding.UTF8.GetBytes($"{SnapshotMagic}|{document.ProfileId}");
        var keyAssociatedData = Encoding.UTF8.GetBytes($"{SnapshotMagic}|{document.ProfileId}|key");
        byte[]? snapshotKey = null;

        foreach (var envelope in document.Envelopes)
        {
            byte[]? kek = null;
            try
            {
                var salt = Convert.FromBase64String(envelope.Salt);
                var nonce = Convert.FromBase64String(envelope.Nonce);
                var wrappedKey = Convert.FromBase64String(envelope.WrappedKey);
                var tag = Convert.FromBase64String(envelope.Tag);

                kek = DeriveKey(NormalizeRecoveryCode(recoveryCode), salt);
                snapshotKey = new byte[KeySize];

                using var aes = new AesGcm(kek, TagSize);
                aes.Decrypt(nonce, wrappedKey, tag, snapshotKey, keyAssociatedData);
                break;
            }
            catch (CryptographicException)
            {
                if (snapshotKey is not null)
                    CryptographicOperations.ZeroMemory(snapshotKey);
                snapshotKey = null;
            }
            finally
            {
                if (kek is not null)
                    CryptographicOperations.ZeroMemory(kek);
            }
        }

        if (snapshotKey is null)
            throw new CryptographicException("The recovery credential is invalid for this snapshot.");

        try
        {
            var payloadNonce = Convert.FromBase64String(document.PayloadNonce);
            var payloadCiphertext = Convert.FromBase64String(document.PayloadCiphertext);
            var payloadTag = Convert.FromBase64String(document.PayloadTag);
            var payload = new byte[payloadCiphertext.Length];

            using var payloadAes = new AesGcm(snapshotKey, TagSize);
            payloadAes.Decrypt(payloadNonce, payloadCiphertext, payloadTag, payload, associatedData);
            return Encoding.UTF8.GetString(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(snapshotKey);
        }
    }

    public static bool VerifyDetachedSignature(
        ReadOnlySpan<byte> content,
        ReadOnlySpan<byte> signature,
        string subjectPublicKeyInfoBase64)
    {
        if (content.IsEmpty || signature.IsEmpty || string.IsNullOrWhiteSpace(subjectPublicKeyInfoBase64))
            return false;

        try
        {
            var keyBytes = Convert.FromBase64String(subjectPublicKeyInfoBase64);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            return rsa.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string NormalizeRecoveryCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    public static string GenerateCode(int length = DefaultRecoveryCodeLength)
    {
        if (length is < MinimumRecoveryCodeLength or > MaximumRecoveryCodeLength)
            throw new ArgumentOutOfRangeException(nameof(length));

        var bytes = RandomNumberGenerator.GetBytes(length);
        try
        {
            var chars = new char[length];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
            return new string(chars);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static byte[] DeriveKey(string recoveryCode, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(recoveryCode),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    private sealed record RecoverySnapshot(
        string Magic,
        string ProfileId,
        DateTimeOffset CreatedUtc,
        string PayloadNonce,
        string PayloadCiphertext,
        string PayloadTag,
        IReadOnlyList<RecoveryEnvelope> Envelopes);

    private sealed record RecoveryEnvelope(
        string Salt,
        string Nonce,
        string WrappedKey,
        string Tag);
}
