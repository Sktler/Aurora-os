using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

namespace ZoeyOS.App.Services
{
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
                var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions { Name = name, Command = command, Arguments = arguments?.ToList() ?? new List<string>() }));
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

        public async Task<string> CallToolAsync(string serverName, string toolName, JsonElement arguments, CancellationToken cancellationToken = default)
        {
            if (!App.Settings.WindowsMcpEnabled) return "MCP permission is disabled in Aurora Settings.";
            var server = _servers.FirstOrDefault(s => string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));
            if (server == null) return $"MCP server '{serverName}' is not connected.";
            var tools = await server.Client.ListToolsAsync(cancellationToken: cancellationToken);
            var tool = tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (tool == null) return $"MCP tool '{toolName}' was not found on server '{serverName}'.";
            var clientTool = tools.First(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            var mcpTool = (await server.Client.ListToolsAsync(cancellationToken: cancellationToken)).First(t => t.Name == clientTool.Name);
            var dictionary = new Dictionary<string, object?>();
            if (arguments.ValueKind == JsonValueKind.Object)
                foreach (var property in arguments.EnumerateObject()) dictionary[property.Name] = property.Value.Clone();
            var result = await mcpTool.CallAsync(dictionary, cancellationToken: cancellationToken);
            if (result.IsError == true) return $"MCP tool error: {string.Join("\n", result.Content.Select(c => c.ToString()))}";
            return string.Join("\n", result.Content.Select(c => c.ToString()));
        }

        public async Task DisconnectAsync(McpServerConnection server) { _servers.Remove(server); await server.Client.DisposeAsync(); }
        public async ValueTask DisposeAsync() { foreach (var server in _servers.ToArray()) await server.Client.DisposeAsync(); _servers.Clear(); _gate.Dispose(); }
    }

    public sealed record McpServerConnection(string Name, string Command, McpClient Client);
    public sealed record McpToolInfo(string Name, string Description);
}