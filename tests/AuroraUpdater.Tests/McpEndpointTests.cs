using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests
{
    public class McpEndpointTests
    {
        [Theory]
        [InlineData("http://localhost:5050/sse", true)]
        [InlineData(" https://example.com/mcp ", true)]
        [InlineData("npx", false)]
        [InlineData("C:\\tools\\server.exe", false)]
        [InlineData("ftp://host/x", false)]
        [InlineData("", false)]
        public void TryGetRemoteEndpoint_DetectsHttpUrlsOnly(string command, bool expected)
        {
            Assert.Equal(expected, McpService.TryGetRemoteEndpoint(command, out _));
        }
    }
}
