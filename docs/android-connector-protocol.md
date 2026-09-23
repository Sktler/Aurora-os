# Aurora Windows ↔ Android connector protocol

Issue #27 uses an application-layer authenticated protocol so Windows and Android remain independently functional.

## Security model

- Each installation creates a random device ID and an ECDSA P-256 identity key.
- Only the public key and device ID leave the device during pairing.
- Pairing requires explicit approval on the already-trusted device.
- The approved peer is pinned by device ID + public-key fingerprint.
- Revocation deletes the trusted peer record; future handshakes are rejected.
- Each connection uses fresh ECDH P-256 keys to derive an AES-256-GCM session key.
- Every encrypted message has a unique message ID, nonce, sender ID, and monotonically increasing sequence number authenticated as AAD.
- Credentials, API keys, recovery codes, and profile secrets are never connector payloads.
- Protocol and capability versions are negotiated before application messages are exchanged.

## Pairing flow

1. Android and Windows discover one another on the local network.
2. The initiating device sends a signed AuroraPairingRequest.
3. The receiving Aurora instance verifies the signature and displays the device name/fingerprint.
4. The user explicitly approves the device and confirms the displayed six-digit pairing code.
5. The receiving device stores the peer device ID and public-key fingerprint.
6. Both devices perform a fresh ECDH exchange and derive the session key from the shared secret plus handshake transcript.
7. Only after authentication succeeds does the connector become usable.

## Discovery

Discovery is transport metadata only. It must not contain credentials or secrets. Implementations may use mDNS/Bonjour or UDP multicast/broadcast. The discovery record should contain protocol version, device ID, platform, display name, listening port, and capabilities hash.

A discovery record is never sufficient for trust.

## Reconnect and revocation

Reconnect may reuse discovery, but authentication must run again. A peer is accepted only when its device ID and pinned public-key fingerprint match a trusted record.

A revoked peer is rejected even if it still knows an old session key.

## Offline behavior

The connector is optional. Windows and Android continue working independently when the peer is unavailable. State changes that cannot be delivered are not silently treated as delivered; callers receive an offline result and may retry.

## Shared contract

Shared events use a versioned envelope containing protocolVersion, messageId, type, senderDeviceId, sequence, nonce, ciphertext, and tag. Payload schemas are versioned by type. Keep credentials, API keys, recovery codes, and raw profile databases out of these payloads.
