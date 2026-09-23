using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraTrustedDeviceStore
{
    public sealed record TrustedDevice(string DeviceId, AuroraConnectorPlatform Platform, string DisplayName, string Fingerprint, DateTimeOffset ApprovedAt);

    private readonly string _path;
    private readonly object _sync = new();
    private readonly Dictionary<string, TrustedDevice> _devices = new(StringComparer.Ordinal);

    public AuroraTrustedDeviceStore(string path)
    {
        _path = path;
        Load();
    }

    public IReadOnlyList<TrustedDevice> Devices
    {
        get { lock (_sync) return _devices.Values.OrderBy(x => x.DisplayName).ToArray(); }
    }

    public void Trust(AuroraDeviceIdentity identity)
    {
        lock (_sync)
        {
            _devices[identity.DeviceId] = new TrustedDevice(
                identity.DeviceId, identity.Platform, identity.DisplayName, identity.Fingerprint, DateTimeOffset.UtcNow);
            Save();
        }
    }

    public bool IsTrusted(string deviceId, string fingerprint)
    {
        lock (_sync)
            return _devices.TryGetValue(deviceId, out var device) &&
                   CryptographicEquals(device.Fingerprint, fingerprint);
    }

    public bool Revoke(string deviceId)
    {
        lock (_sync)
        {
            if (!_devices.Remove(deviceId)) return false;
            Save();
            return true;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<TrustedDevice>>(File.ReadAllText(_path)) ?? [];
            foreach (var device in list)
                _devices[device.DeviceId] = device;
        }
        catch (JsonException) { _devices.Clear(); }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_devices.Values, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(a);
        var right = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }
}
