using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraDeviceIdentityService
{
    private sealed record IdentityFile(string DeviceId, string PublicKeyBase64, string ProtectedPrivateKeyBase64);
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
                var stored = JsonSerializer.Deserialize<IdentityFile>(File.ReadAllText(_path))
                    ?? throw new InvalidDataException("Aurora device identity is invalid.");
                _key = ECDsa.Create();
                var privateKey = Unprotect(Convert.FromBase64String(stored.ProtectedPrivateKeyBase64));
                try { _key.ImportECPrivateKey(privateKey, out _); }
                finally { CryptographicOperations.ZeroMemory(privateKey); }
                var publicKey = Convert.FromBase64String(stored.PublicKeyBase64);
                _identity = new AuroraDeviceIdentity(stored.DeviceId, Platform, DisplayName,
                    stored.PublicKeyBase64, AuroraConnectorProtocol.ComputeFingerprint(publicKey));
                return _identity;
            }

            _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKeyBytes = _key.ExportECPrivateKey();
            try
            {
                var publicKeyBytes = _key.ExportSubjectPublicKeyInfo();
                var publicKey = Convert.ToBase64String(publicKeyBytes);
                var deviceId = Guid.NewGuid().ToString("N");
                var protectedPrivateKey = Convert.ToBase64String(Protect(privateKeyBytes));
                var file = new IdentityFile(deviceId, publicKey, protectedPrivateKey);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, _path, true);
                _identity = new AuroraDeviceIdentity(deviceId, Platform, DisplayName, publicKey,
                    AuroraConnectorProtocol.ComputeFingerprint(publicKeyBytes));
                return _identity;
            }
            finally { CryptographicOperations.ZeroMemory(privateKeyBytes); }
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

    private static byte[] Protect(byte[] data)
    {
        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            input.cbData = (uint)data.Length;
            input.pbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, input.pbData, data.Length);
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            var protectedBytes = new byte[output.cbData];
            Marshal.Copy(output.pbData, protectedBytes, 0, protectedBytes.Length);
            return protectedBytes;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    private static byte[] Unprotect(byte[] data)
    {
        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            input.cbData = (uint)data.Length;
            input.pbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, input.pbData, data.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            var clear = new byte[output.cbData];
            Marshal.Copy(output.pbData, clear, 0, clear.Length);
            return clear;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr pOptionalEntropy,
        IntPtr ppDescr, IntPtr pOptionalEntropyOut, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
