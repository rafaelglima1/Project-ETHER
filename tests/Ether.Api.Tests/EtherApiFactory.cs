using Ether.Api.Tests.Fakes;
using Ether.Application.Abstractions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ether.Api.Tests;

/// <summary>
/// Builds the API with the real HTTP pipeline but in-memory persistence, so HTTP
/// contracts can be tested without a database. EF persistence is validated on the
/// Oracle runtime instead.
/// </summary>
internal static class EtherApiFactory
{
    public static WebApplicationFactory<Program> Create() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<ICharacterRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddSingleton<InMemoryPersistence>();
                services.AddSingleton<IAccountRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<ICharacterRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryPersistence>());
            });
        });
}
