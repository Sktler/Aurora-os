using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraConnectorSession : IDisposable
{
    private readonly byte[] _sessionKey;
    private readonly HashSet<string> _messageIds = new(StringComparer.Ordinal);
    private long _nextSendSequence;
    private long _lastReceivedSequence;

    public AuroraConnectorSession(ReadOnlySpan<byte> sessionKey)
    {
        if (sessionKey.Length != 32) throw new ArgumentException("Session key must be 32 bytes.", nameof(sessionKey));
        _sessionKey = sessionKey.ToArray();
    }

    public AuroraConnectorEnvelope Encrypt(string type, string senderDeviceId, ReadOnlySpan<byte> payload)
    {
        var sequence = ++_nextSendSequence;
        return AuroraConnectorProtocol.Encrypt(type, senderDeviceId, sequence, _sessionKey, payload);
    }

    public byte[] Decrypt(AuroraConnectorEnvelope envelope)
    {
        if (!string.Equals(envelope.ProtocolVersion, AuroraConnectorProtocol.ProtocolVersion, StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported connector protocol version.");

        if (envelope.Sequence <= _lastReceivedSequence)
            throw new CryptographicException("Replay or out-of-order connector message rejected.");

        if (!_messageIds.Add(envelope.MessageId))
            throw new CryptographicException("Duplicate connector message rejected.");

        var plaintext = AuroraConnectorProtocol.Decrypt(envelope, _sessionKey);
        _lastReceivedSequence = envelope.Sequence;
        return plaintext;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(_sessionKey);
}
