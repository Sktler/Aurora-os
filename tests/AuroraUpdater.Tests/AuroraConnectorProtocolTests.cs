using System.Security.Cryptography;
using System.Text;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class AuroraConnectorProtocolTests
{
    [Fact]
    public void Pairing_request_signature_round_trips()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new AuroraPairingRequest(
            AuroraConnectorProtocol.ProtocolVersion,
            "device-a",
            AuroraConnectorPlatform.Windows,
            "Aurora Desktop",
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).PublicKey.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
            "",
            "capabilities-hash");

        var canonical = Encoding.UTF8.GetBytes(AuroraConnectorProtocol.CanonicalizePairingRequest(request));
        var signature = AuroraConnectorProtocol.Sign(canonical, key);
        var publicKey = ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(request.PublicKeyBase64), out _);

        Assert.True(AuroraConnectorProtocol.Verify(canonical, signature, publicKey));
        canonical[0] ^= 1;
        Assert.False(AuroraConnectorProtocol.Verify(canonical, signature, publicKey));
    }

    [Fact]
    public void Fresh_ECDH_keys_produce_the_same_session_key()
    {
        using var a = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var b = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var aPublic = a.PublicKey.ExportSubjectPublicKeyInfo();
        var bPublic = b.PublicKey.ExportSubjectPublicKeyInfo();
        var transcript = SHA256.HashData(Encoding.UTF8.GetBytes("Aurora handshake transcript"));

        var aKey = AuroraConnectorProtocol.DeriveSessionKey(a, bPublic, transcript);
        var bKey = AuroraConnectorProtocol.DeriveSessionKey(b, aPublic, transcript);

        Assert.Equal(aKey, bKey);
    }

    [Fact]
    public void Envelope_encrypts_and_rejects_tampering()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("safe Aurora state");
        var envelope = AuroraConnectorProtocol.Encrypt("state.update", "device-a", 1, key, plaintext);

        Assert.Equal(plaintext, AuroraConnectorProtocol.Decrypt(envelope, key));

        var tampered = envelope with { Sequence = 2 };
        Assert.Throws<CryptographicException>(() => AuroraConnectorProtocol.Decrypt(tampered, key));
    }

    [Fact]
    public void Windows_identity_is_stable_and_private_material_is_not_plaintext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aurora-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "device.identity");

        try
        {
            var service = new AuroraDeviceIdentityService(path, AuroraConnectorPlatform.Windows, "Test Aurora");
            var first = service.GetOrCreate();
            var second = service.GetOrCreate();

            Assert.Equal(first.DeviceId, second.DeviceId);
            Assert.Equal(first.Fingerprint, second.Fingerprint);
            Assert.NotEqual("", first.Fingerprint);

            var raw = File.ReadAllText(path);
            Assert.DoesNotContain(first.PublicKeyBase64, raw, StringComparison.Ordinal);
            Assert.Contains("ProtectedPrivateKeyBase64", raw, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Pairing_codes_are_six_digits_and_not_reused_by_assumption()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => AuroraConnectorProtocol.GeneratePairingCode()).ToArray();
        Assert.All(codes, code => Assert.Matches("^\\d{6}$", code));
        Assert.True(codes.Distinct(StringComparer.Ordinal).Count() > 90);
    }
}
