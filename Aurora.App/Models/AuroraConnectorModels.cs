using System;
using System.Collections.Generic;

namespace Aurora.App.Models;

public enum AuroraConnectorPlatform { Windows, Android }

public sealed record AuroraDeviceIdentity(
    string DeviceId,
    AuroraConnectorPlatform Platform,
    string DisplayName,
    string PublicKeyBase64,
    string Fingerprint);

public sealed record AuroraPairingRequest(
    string ProtocolVersion,
    string DeviceId,
    AuroraConnectorPlatform Platform,
    string DisplayName,
    string PublicKeyBase64,
    string EphemeralPublicKeyBase64,
    string NonceBase64,
    string SignatureBase64,
    string CapabilitiesHash);

public sealed record AuroraPairingApproval(
    string DeviceId,
    string PairingCode,
    DateTimeOffset ExpiresAt);

public sealed record AuroraConnectorEnvelope(
    string ProtocolVersion,
    string MessageId,
    string Type,
    string SenderDeviceId,
    long Sequence,
    string NonceBase64,
    string CiphertextBase64,
    string TagBase64);

public sealed record AuroraCapabilitySet(
    string Platform,
    string Version,
    IReadOnlyList<string> Capabilities);
