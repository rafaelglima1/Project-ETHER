using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.Api.Tests;

public sealed class ProductionStartupGuardTests
{
    [Fact]
    public async Task Startup_fails_in_production_without_signing_key()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("Authentication:SigningKey", string.Empty);
            });

        var exception = await Record.ExceptionAsync(async () =>
        {
            using var client = factory.CreateClient();
            _ = await client.GetAsync(new Uri("/health", UriKind.Relative));
        });

        Assert.NotNull(exception);
        Assert.Contains("SigningKey", exception!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Startup_succeeds_in_development_without_signing_key()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
