using System.IO;
using System.Security.Cryptography;
using System.Text;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class AuroraRecoveryServiceTests
{
    [Fact]
    public void RecoveryCodes_use_secure_defaults_and_hard_minimum()
    {
        var codes = AuroraRecoveryService.GenerateRecoveryCodes();

        Assert.Equal(AuroraRecoveryService.DefaultRecoveryCodeCount, codes.Count);
        Assert.All(codes, code => Assert.Equal(AuroraRecoveryService.DefaultRecoveryCodeLength, code.Length));
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.True(AuroraRecoveryService.VerifyRecoveryCode(code, codes)));
        Assert.False(AuroraRecoveryService.ValidateRecoveryCodeConfiguration(5, 25));
        Assert.True(AuroraRecoveryService.ValidateRecoveryCodeConfiguration(5, 26));
    }

    [Fact]
    public void RecoveryCode_normalization_allows_human_friendly_formatting()
    {
        var code = AuroraRecoveryService.GenerateCode();
        var formatted = $"{code[..8]}-{code[8..16]}-{code[16..24]}-{code[24..]}";

        Assert.True(AuroraRecoveryService.VerifyRecoveryCode(formatted, new[] { code }));
        Assert.False(AuroraRecoveryService.VerifyRecoveryCode("wrong-code", new[] { code }));
    }

    [Fact]
    public void Snapshot_round_trips_with_each_recovery_code_but_not_wrong_code()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aurora-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var snapshot = Path.Combine(root, "profile.aurora-recovery");
        var codes = AuroraRecoveryService.GenerateRecoveryCodes(5, 32);
        const string profileId = "profile-a";
        const string payload = "{\"name\":\"Alice\",\"settings\":{\"theme\":\"dark\"}}";

        try
        {
            AuroraRecoveryService.CreateEncryptedSnapshot(snapshot, profileId, payload, codes);

            foreach (var code in codes)
                Assert.Equal(payload, AuroraRecoveryService.RestoreEncryptedSnapshot(snapshot, code, profileId));

            Assert.Throws<CryptographicException>(() =>
            {
                AuroraRecoveryService.RestoreEncryptedSnapshot(snapshot, AuroraRecoveryService.GenerateCode(), profileId);
            });
            Assert.Throws<InvalidDataException>(() =>
            {
                AuroraRecoveryService.RestoreEncryptedSnapshot(snapshot, codes[0], "different-profile");
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Snapshot_does_not_contain_plaintext_payload_or_recovery_code()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aurora-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var snapshot = Path.Combine(root, "profile.aurora-recovery");
        var codes = AuroraRecoveryService.GenerateRecoveryCodes(1, 32);
        const string payload = "{\"secret\":\"this must be encrypted\"}";

        try
        {
            AuroraRecoveryService.CreateEncryptedSnapshot(snapshot, "profile-a", payload, codes);
            var raw = File.ReadAllText(snapshot);

            Assert.DoesNotContain(payload, raw, StringComparison.Ordinal);
            Assert.DoesNotContain(codes[0], raw, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Detached_signature_verification_accepts_only_matching_key_and_content()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var content = Encoding.UTF8.GetBytes("Aurora trusted release manifest");
        var signature = rsa.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(AuroraRecoveryService.VerifyDetachedSignature(content, signature, publicKey));
        Assert.False(AuroraRecoveryService.VerifyDetachedSignature(Encoding.UTF8.GetBytes("tampered"), signature, publicKey));
    }
}
