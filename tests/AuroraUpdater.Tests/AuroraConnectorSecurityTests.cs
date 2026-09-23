using System.Security.Cryptography;
using System.Text;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class AuroraConnectorSecurityTests
{
    [Fact]
    public void Trusted_device_can_be_revoked_and_cannot_reconnect()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aurora-trust-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = key.ExportSubjectPublicKeyInfo();
            var identity = new AuroraDeviceIdentity("android-1", AuroraConnectorPlatform.Android, "Pixel", 
                Convert.ToBase64String(publicKey), AuroraConnectorProtocol.ComputeFingerprint(publicKey));
            var store = new AuroraTrustedDeviceStore(Path.Combine(root, "trusted.json"));

            store.Trust(identity);
            Assert.True(store.IsTrusted(identity.DeviceId, identity.Fingerprint));
            Assert.True(store.Revoke(identity.DeviceId));
            Assert.False(store.IsTrusted(identity.DeviceId, identity.Fingerprint));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Session_rejects_replay_and_out_of_order_messages()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        using var sender = new AuroraConnectorSession(key);
        using var receiver = new AuroraConnectorSession(key);

        var first = sender.Encrypt("state.update", "windows", Encoding.UTF8.GetBytes("one"));
        var second = sender.Encrypt("state.update", "windows", Encoding.UTF8.GetBytes("two"));

        Assert.Equal("one", Encoding.UTF8.GetString(receiver.Decrypt(first)));
        Assert.Equal("two", Encoding.UTF8.GetString(receiver.Decrypt(second)));
        Assert.Throws<CryptographicException>(() => receiver.Decrypt(first));
        Assert.Throws<CryptographicException>(() => receiver.Decrypt(second with { Sequence = 1 }));
    }
}
