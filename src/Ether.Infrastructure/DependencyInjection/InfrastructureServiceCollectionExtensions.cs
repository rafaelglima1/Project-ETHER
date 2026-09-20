using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.Authentication;
using Ether.Infrastructure.Dependencies;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Redis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="isProduction">
    /// When true, Production-only validation is enforced (e.g. a real signing key is
    /// required). Hosts pass their environment flag; defaults to false for convenience.
    /// </param>
    public static IServiceCollection AddEtherInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<WebSocketOptions>(configuration.GetSection(WebSocketOptions.SectionName));
        services.Configure<GameServerOptions>(configuration.GetSection(GameServerOptions.SectionName));

        // Authentication options are validated at startup: in Production a usable
        // signing key is mandatory and the development placeholder is rejected.
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthenticationOptions>>(
            new AuthenticationOptionsValidator(isProduction));

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
