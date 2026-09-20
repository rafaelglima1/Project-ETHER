using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.Dependencies;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Redis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ether.Infrastructure.DependencyInjection;

/// <summary>
/// Registration entry point for the infrastructure layer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers configuration options and technical infrastructure.
    /// PostgreSQL is only wired when a connection string is configured; no gameplay
    /// tables or migrations are created at M0.
    /// </summary>
    public static IServiceCollection AddEtherInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));
        services.Configure<WebSocketOptions>(configuration.GetSection(WebSocketOptions.SectionName));
        services.Configure<GameServerOptions>(configuration.GetSection(GameServerOptions.SectionName));

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                              ?? new DatabaseOptions();

        if (databaseOptions.IsConfigured)
        {
            services.AddDbContext<EtherDbContext>(options =>
            {
                options.UseNpgsql(databaseOptions.ConnectionString, npgsql =>
                {
                    npgsql.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    npgsql.EnableRetryOnFailure(databaseOptions.MaxRetryCount);
                });

                if (databaseOptions.EnableDetailedErrors)
                {
                    options.EnableDetailedErrors();
                }
            });
        }

        services.AddSingleton<RedisConnectionFactory>();
        services.AddSingleton<IDependencyReadinessProbe, DependencyReadinessProbe>();

        return services;
    }
}
