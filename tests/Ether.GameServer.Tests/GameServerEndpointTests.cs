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
    public async Task Game_endpoint_requires_a_websocket_upgrade()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/game", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
