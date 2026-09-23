using System;
using System.Security.Cryptography;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraPairingRequestFactory
{
    private readonly AuroraDeviceIdentityService _identity;

    public AuroraPairingRequestFactory(AuroraDeviceIdentityService identity) => _identity = identity;

    public AuroraPairingRequest Create(ECDiffieHellman ephemeralKey, AuroraCapabilitySet capabilities)
    {
        var identity = _identity.GetOrCreate();
        var ephemeralPublicKey = ephemeralKey.PublicKey.ExportSubjectPublicKeyInfo();
        var nonce = RandomNumberGenerator.GetBytes(32);
        var capabilitiesHash = Convert.ToHexString(
            SHA256.HashData(AuroraConnectorProtocol.Serialize(capabilities))).ToLowerInvariant();

        var unsigned = new AuroraPairingRequest(
            AuroraConnectorProtocol.ProtocolVersion,
            identity.DeviceId,
            identity.Platform,
            identity.DisplayName,
            identity.PublicKeyBase64,
            Convert.ToBase64String(ephemeralPublicKey),
            Convert.ToBase64String(nonce),
            "",
            capabilitiesHash);

        var canonical = System.Text.Encoding.UTF8.GetBytes(
            AuroraConnectorProtocol.CanonicalizePairingRequest(unsigned));
        var signature = _identity.Sign(canonical);

        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    public static bool Verify(AuroraPairingRequest request)
    {
        if (!string.Equals(request.ProtocolVersion, AuroraConnectorProtocol.ProtocolVersion, StringComparison.Ordinal))
            return false;

        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(request.PublicKeyBase64), out _);
            var unsigned = request with { SignatureBase64 = "" };
            var canonical = System.Text.Encoding.UTF8.GetBytes(
                AuroraConnectorProtocol.CanonicalizePairingRequest(unsigned));
            return AuroraConnectorProtocol.Verify(
                canonical, Convert.FromBase64String(request.SignatureBase64), publicKey);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }
}
