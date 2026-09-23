using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services;

public sealed class AuroraDiscoveryService : IDisposable
{
    public const int Port = 43871;
    private const string MulticastAddress = "239.255.42.42";
    private readonly AuroraDeviceIdentityService _identityService;
    private readonly AuroraCapabilitySet _capabilities;
    private readonly CancellationTokenSource _stop = new();
    private UdpClient? _listener;
    private Task? _listenTask;

    public AuroraDiscoveryService(AuroraDeviceIdentityService identityService, AuroraCapabilitySet capabilities)
    {
        _identityService = identityService;
        _capabilities = capabilities;
    }

    public event EventHandler<AuroraDeviceIdentity>? DeviceDiscovered;

    public void Start()
    {
        if (_listenTask != null) return;
        _listener = new UdpClient(AddressFamily.InterNetwork);
        _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
        _listener.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));
        _listenTask = Task.Run(ListenAsync);
    }

    public async Task AdvertiseAsync(CancellationToken cancellationToken = default)
    {
        var identity = _identityService.GetOrCreate();
        var packet = new
        {
            protocolVersion = AuroraConnectorProtocol.ProtocolVersion,
            deviceId = identity.DeviceId,
            platform = identity.Platform,
            displayName = identity.DisplayName,
            port = Port,
            capabilitiesHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    AuroraConnectorProtocol.Serialize(_capabilities))).ToLowerInvariant()
        };
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet));
        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.MulticastLoopback = false;
        await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(MulticastAddress), Port));
    }

    private async Task ListenAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested && _listener != null)
            {
                var result = await _listener.ReceiveAsync(_stop.Token);
                using var document = JsonDocument.Parse(result.Buffer);
                var root = document.RootElement;
                if (!root.TryGetProperty("protocolVersion", out var version) ||
                    version.GetString() != AuroraConnectorProtocol.ProtocolVersion ||
                    !root.TryGetProperty("deviceId", out var deviceId) ||
                    !root.TryGetProperty("publicKeyBase64", out _))
                    continue;

                // Discovery is intentionally not trust. A future handshake must validate
                // the pinned identity before a session is accepted.
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Dispose();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _stop.Dispose();
    }
}
