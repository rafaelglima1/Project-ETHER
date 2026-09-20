using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.Dependencies;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Redis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Tests.Dependencies;

public sealed class DependencyReadinessProbeTests
{
    /// <summary>
    /// Builds a probe over an in-memory configuration. The service provider is kept
    /// alive on purpose: the probe uses its scope factory, so disposing it early
    /// would invalidate the probe. Test-scoped leak is acceptable.
    /// </summary>
    private static ServiceProvider BuildProvider(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                              ?? new DatabaseOptions();

        var services = new ServiceCollection();
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        if (databaseOptions.IsConfigured)
        {
            services.AddDbContext<EtherDbContext>(options =>
                options.UseNpgsql(databaseOptions.ConnectionString));
        }

        services.AddSingleton<RedisConnectionFactory>();
        services.AddSingleton<IDependencyReadinessProbe, DependencyReadinessProbe>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Reports_not_configured_dependencies_as_ready()
    {
        using var provider = BuildProvider();
        var probe = provider.GetRequiredService<IDependencyReadinessProbe>();

        var report = await probe.CheckAsync();

        Assert.True(report.IsReady);
        Assert.Equal(2, report.Dependencies.Count);
        Assert.All(report.Dependencies, dependency => Assert.False(dependency.Configured));
        Assert.All(report.Dependencies, dependency => Assert.True(dependency.Healthy));
    }

    [Fact]
    public async Task Reports_configured_but_unreachable_redis_as_not_ready()
    {
        using var provider = BuildProvider(
            ("Redis:ConnectionString", "127.0.0.1:1"),
            ("Redis:ConnectTimeoutMs", "500"));
        var probe = provider.GetRequiredService<IDependencyReadinessProbe>();

        var report = await probe.CheckAsync();

        Assert.False(report.IsReady);
        var redis = Assert.Single(report.Dependencies, dependency => dependency.Name == "Redis");
        Assert.True(redis.Configured);
        Assert.False(redis.Healthy);
    }

    [Fact]
    public void Redis_factory_reports_not_configured_for_empty_connection_string()
    {
        var factory = new RedisConnectionFactory(Options.Create(new RedisOptions()));

        Assert.False(factory.IsConfigured);
    }
}
