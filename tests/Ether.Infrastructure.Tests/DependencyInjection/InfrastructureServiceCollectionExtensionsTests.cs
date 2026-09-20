using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.DependencyInjection;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Redis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ether.Infrastructure.Tests.DependencyInjection;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationWith(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Without_connection_strings_DbContext_is_not_registered()
    {
        var services = new ServiceCollection();
        services.AddEtherInfrastructure(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<EtherDbContext>());
        Assert.NotNull(provider.GetService<RedisConnectionFactory>());
        Assert.NotNull(provider.GetService<IDependencyReadinessProbe>());
    }

    [Fact]
    public void With_connection_string_DbContext_is_registered()
    {
        var configuration = ConfigurationWith(
            ("Database:ConnectionString", "Host=localhost;Database=ether;Username=ether;Password=ether"));

        var services = new ServiceCollection();
        services.AddEtherInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<EtherDbContext>());
    }

    [Fact]
    public void Binds_configuration_values_into_options()
    {
        var configuration = ConfigurationWith(
            ("Redis:ConnectionString", "localhost:6379"),
            ("Redis:InstanceName", "test:"),
            ("GameServer:TickRateHz", "30"));

        var services = new ServiceCollection();
        services.AddEtherInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        var redis = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
        var gameServer = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GameServerOptions>>().Value;

        Assert.True(redis.IsConfigured);
        Assert.Equal("test:", redis.InstanceName);
        Assert.Equal(30, gameServer.TickRateHz);
    }

    [Fact]
    public void Redis_factory_is_not_configured_when_no_connection_string()
    {
        var services = new ServiceCollection();
        services.AddEtherInfrastructure(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<RedisConnectionFactory>().IsConfigured);
    }
}
