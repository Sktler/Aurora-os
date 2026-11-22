using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class PhoneLinkServiceTests
{
    [Fact]
    public async Task Pairs_with_pin_then_chats_and_rejects_wrong_pin_and_missing_token()
    {
        using var link = new PhoneLinkService((m, _) => Task.FromResult("echo: " + m));
        link.Start(45151);
        using var http = new HttpClient { BaseAddress = new System.Uri("http://127.0.0.1:45151") };

        var bad = await http.PostAsync("/pair", new StringContent("{\"pin\":\"abc\"}", Encoding.UTF8));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, bad.StatusCode);

        var noAuth = await http.PostAsync("/chat", new StringContent("{\"message\":\"hi\"}", Encoding.UTF8));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, noAuth.StatusCode);

        var ok = await http.PostAsync("/pair", new StringContent($"{{\"pin\":\"{link.CurrentPin}\"}}", Encoding.UTF8));
        Assert.True(ok.IsSuccessStatusCode);
        var token = System.Text.Json.JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();

        var req = new HttpRequestMessage(HttpMethod.Post, "/chat") { Content = new StringContent("{\"message\":\"hi\"}", Encoding.UTF8) };
        req.Headers.Add("Authorization", "Bearer " + token);
        var chat = await http.SendAsync(req);
        Assert.Contains("echo: hi", await chat.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Locks_out_after_repeated_wrong_pins()
    {
        using var link = new PhoneLinkService((m, _) => Task.FromResult(""));
        link.Start(45152);
        for (var i = 0; i < 5; i++) Assert.Null(link.TryPair("000000x"));
        Assert.Null(link.TryPair(link.CurrentPin));
    }
}
