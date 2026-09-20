using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.GameServer.Tests;

public sealed class GameServerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameServerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_returns_200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Game_endpoint_is_exposed_but_websocket_is_not_implemented_yet()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/game", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("websocket-not-implemented", body, StringComparison.Ordinal);
    }
}
