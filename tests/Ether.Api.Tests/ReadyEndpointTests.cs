using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.Api.Tests;

public sealed class ReadyEndpointTests
{
    [Fact]
    public async Task Ready_returns_503_when_configured_dependency_is_unreachable()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Redis:ConnectionString", "127.0.0.1:1");
                builder.UseSetting("Redis:ConnectTimeoutMs", "500");
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
