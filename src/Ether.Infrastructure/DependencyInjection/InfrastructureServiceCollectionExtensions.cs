using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.Accounts;
using Ether.Infrastructure.Authentication;
using Ether.Infrastructure.Characters;
using Ether.Infrastructure.Dependencies;
using Ether.Infrastructure.Memory;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Random;
using Ether.Infrastructure.Redis;
using Ether.Infrastructure.Security;
using Ether.Infrastructure.World;

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
        services.Configure<CharacterOptions>(configuration.GetSection(CharacterOptions.SectionName));
        services.Configure<WorldOptions>(configuration.GetSection(WorldOptions.SectionName));
        services.Configure<CombatOptions>(configuration.GetSection(CombatOptions.SectionName));

        // Authentication options are validated at startup: in Production a usable
        // signing key is mandatory and the development placeholder is rejected.
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthenticationOptions>>(
            new AuthenticationOptionsValidator(isProduction));

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                              ?? new DatabaseOptions();

        // The context is always registered so the object graph is valid even when the
        // database is not configured; readiness reports it as "not configured" and the
        // provider only needs a connection string once a query actually runs.
        services.AddDbContext<EtherDbContext>(options =>
        {
            if (databaseOptions.IsConfigured)
            {
                options.UseNpgsql(databaseOptions.ConnectionString, npgsql =>
                {
                    npgsql.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    npgsql.EnableRetryOnFailure(databaseOptions.MaxRetryCount);
                });
            }
            else
            {
                options.UseNpgsql();
            }

            if (databaseOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddSingleton<RedisConnectionFactory>();
        services.AddSingleton<IDependencyReadinessProbe, DependencyReadinessProbe>();

        // Security services (M2).
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IWorldMapProvider, StaticWorldMapProvider>();

        // Combat runtime coordination (M5): process-local cooldowns, entity locks and RNG.
        services.AddSingleton<IAbilityCooldownStore, InMemoryAbilityCooldownStore>();
        services.AddSingleton<IEntityLockProvider, InMemoryEntityLockProvider>();
        services.AddSingleton<Ether.Domain.Combat.IRandomSource, SharedRandomSource>();

        // Creature world state (M6): transient instances, not per-instance persistence.
        services.AddSingleton<ICreatureWorld, InMemoryCreatureWorld>();

        // Repositories and unit of work depend on the always-registered EF Core context.
        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<ICharacterRepository, EfCharacterRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
