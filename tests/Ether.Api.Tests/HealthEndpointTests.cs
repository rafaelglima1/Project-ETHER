using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task Ready_endpoint_is_ready_when_no_dependency_is_configured()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
    }
}
