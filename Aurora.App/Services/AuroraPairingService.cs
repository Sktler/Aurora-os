using System;
using System.Security.Cryptography;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraPairingService
{
    private readonly AuroraDeviceIdentityService _identity;
    private string? _pendingCode;
    private DateTimeOffset _expiresAt;

    public AuroraPairingService(AuroraDeviceIdentityService identity) => _identity = identity;

    public AuroraPairingApproval BeginApproval()
    {
        var me = _identity.GetOrCreate();
        _pendingCode = AuroraConnectorProtocol.GeneratePairingCode();
        _expiresAt = DateTimeOffset.UtcNow.Add(AuroraConnectorProtocol.PairingCodeLifetime);
        return new AuroraPairingApproval(me.DeviceId, _pendingCode, _expiresAt);
    }

    public bool ConfirmApproval(string deviceId, string code)
    {
        if (!string.Equals(deviceId, _identity.GetOrCreate().DeviceId, StringComparison.Ordinal) ||
            DateTimeOffset.UtcNow > _expiresAt ||
            string.IsNullOrWhiteSpace(_pendingCode))
            return false;

        var left = System.Text.Encoding.UTF8.GetBytes(_pendingCode);
        var right = System.Text.Encoding.UTF8.GetBytes(code.Trim());
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    public void CancelApproval()
    {
        _pendingCode = null;
        _expiresAt = default;
    }
}
