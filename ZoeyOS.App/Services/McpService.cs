using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

namespace ZoeyOS.App.Services
{
    /// <summary>Permissioned MCP client manager. Servers are user-configured; tools are not exposed to the model until allowed.</summary>
    public sealed class McpService : IAsyncDisposable
    {
        private readonly List<McpServerConnection> _servers = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public IReadOnlyList<McpServerConnection> Servers => _servers;

        public async Task<McpServerConnection> ConnectStdioAsync(string name, string command, IEnumerable<string>? arguments = null)
        {
            await _gate.WaitAsync();
            try
            {
                var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = name,
                    Command = command,
                    Arguments = arguments?.ToList() ?? new List<string>()
                }));
                var connection = new McpServerConnection(name, command, client);
                _servers.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                _servers.Add(connection);
                return connection;
            }
            finally { _gate.Release(); }
        }

        public async Task<IReadOnlyList<McpToolInfo>> DiscoverToolsAsync(McpServerConnection server, CancellationToken cancellationToken = default)
        {
            var tools = await server.Client.ListToolsAsync(cancellationToken: cancellationToken);
            return tools.Select(t => new McpToolInfo(t.Name, t.Description ?? string.Empty)).ToArray();
        }

        public async Task DisconnectAsync(McpServerConnection server)
        {
            _servers.Remove(server);
            await server.Client.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var server in _servers.ToArray()) await server.Client.DisposeAsync();
            _servers.Clear();
            _gate.Dispose();
        }
    }

    public sealed record McpServerConnection(string Name, string Command, McpClient Client);
    public sealed record McpToolInfo(string Name, string Description);
}