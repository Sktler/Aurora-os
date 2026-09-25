using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.App.Models;

namespace Aurora.App.Services;

public static class AuroraConnectorProtocol
{
    public const string ProtocolVersion = "1";
    public const int PairingCodeLength = 6;
    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(5);

    public static string CanonicalizePairingRequest(AuroraPairingRequest request) =>
        string.Join("|", ProtocolVersion, request.DeviceId, request.Platform, request.DisplayName,
            request.PublicKeyBase64, request.EphemeralPublicKeyBase64, request.NonceBase64, request.CapabilitiesHash);

    public static byte[] Sign(ReadOnlySpan<byte> data, ECDsa privateKey) =>
        privateKey.SignData(data, HashAlgorithmName.SHA256);

    public static bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, ECDsa publicKey) =>
        publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);

    public static string ComputeFingerprint(ReadOnlySpan<byte> publicKey) =>
        Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();

    public static string GeneratePairingCode()
    {
        Span<byte> random = stackalloc byte[4];
        RandomNumberGenerator.Fill(random);
        return (BitConverter.ToUInt32(random) % 1_000_000).ToString("D6");
    }

    public static byte[] DeriveSessionKey(ECDiffieHellman localPrivateKey,
        ReadOnlySpan<byte> remotePublicKey, ReadOnlySpan<byte> transcriptHash)
    {
        using var remote = ECDiffieHellman.Create();
        remote.ImportSubjectPublicKeyInfo(remotePublicKey, out _);
        var shared = localPrivateKey.DeriveKeyMaterial(remote.PublicKey);
        try
        {
            using var hmac = new HMACSHA256(shared);
            var transcriptBytes = transcriptHash.ToArray();
            return hmac.ComputeHash(transcriptBytes);
        }
        finally { CryptographicOperations.ZeroMemory(shared); }
    }

    public static AuroraConnectorEnvelope Encrypt(string type, string senderDeviceId, long sequence,
        ReadOnlySpan<byte> sessionKey, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        var messageId = Guid.NewGuid().ToString("N");
        var aad = Encoding.UTF8.GetBytes($"{ProtocolVersion}|{messageId}|{type}|{senderDeviceId}|{sequence}");
        using var aes = new AesGcm(sessionKey, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        return new AuroraConnectorEnvelope(ProtocolVersion, messageId, type, senderDeviceId, sequence,
            Convert.ToBase64String(nonce), Convert.ToBase64String(ciphertext), Convert.ToBase64String(tag));
    }

    public static byte[] Decrypt(AuroraConnectorEnvelope envelope, ReadOnlySpan<byte> sessionKey)
    {
        var nonce = Convert.FromBase64String(envelope.NonceBase64);
        var ciphertext = Convert.FromBase64String(envelope.CiphertextBase64);
        var tag = Convert.FromBase64String(envelope.TagBase64);
        var plaintext = new byte[ciphertext.Length];
        var aad = Encoding.UTF8.GetBytes($"{ProtocolVersion}|{envelope.MessageId}|{envelope.Type}|{envelope.SenderDeviceId}|{envelope.Sequence}");
        using var aes = new AesGcm(sessionKey, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }

    public static byte[] Serialize(object value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
