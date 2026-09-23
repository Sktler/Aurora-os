using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraDeviceIdentityService
{
    private sealed record IdentityFile(string DeviceId, string PublicKeyBase64, string PrivateKeyBase64);
    private readonly string _path;
    private readonly object _sync = new();
    private ECDsa? _key;
    private AuroraDeviceIdentity? _identity;

    public AuroraDeviceIdentityService(string path, AuroraConnectorPlatform platform, string displayName)
    {
        _path = path;
        Platform = platform;
        DisplayName = displayName;
    }

    public AuroraConnectorPlatform Platform { get; }
    public string DisplayName { get; private set; } = "";

    public AuroraDeviceIdentity GetOrCreate()
    {
        lock (_sync)
        {
            if (_identity != null) return _identity;
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            if (File.Exists(_path))
            {
                var stored = JsonSerializer.Deserialize<IdentityFile>(File.ReadAllText(_path));
                if (stored != null)
                {
                    _key = ECDsa.Create();
                    _key.ImportECPrivateKey(Convert.FromBase64String(stored.PrivateKeyBase64), out _);
                    var publicKey = Convert.FromBase64String(stored.PublicKeyBase64);
                    _identity = new AuroraDeviceIdentity(stored.DeviceId, Platform, DisplayName,
                        stored.PublicKeyBase64, AuroraConnectorProtocol.ComputeFingerprint(publicKey));
                    return _identity;
                }
            }

            _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKey = Convert.ToBase64String(_key.ExportECPrivateKey());
            var publicKeyBytes = _key.ExportSubjectPublicKeyInfo();
            var publicKey = Convert.ToBase64String(publicKeyBytes);
            var deviceId = Guid.NewGuid().ToString("N");
            var file = new IdentityFile(deviceId, publicKey, privateKey);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, true);
            _identity = new AuroraDeviceIdentity(deviceId, Platform, DisplayName, publicKey,
                AuroraConnectorProtocol.ComputeFingerprint(publicKeyBytes));
            return _identity;
        }
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            if (_key == null) GetOrCreate();
            return AuroraConnectorProtocol.Sign(data, _key!);
        }
    }

    public void DeleteIdentity()
    {
        lock (_sync)
        {
            _key?.Dispose();
            _key = null;
            _identity = null;
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
