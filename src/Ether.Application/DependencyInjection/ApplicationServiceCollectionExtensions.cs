using Ether.Application.Accounts;
using Ether.Application.Auth;
using Ether.Application.Characters;
using Ether.Application.Combat;
using Ether.Application.Creatures;
using Ether.Application.Inventory;
using Ether.Application.Progression;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Ether.Application.Abstractions;

namespace Ether.Application.DependencyInjection;

/// <summary>
/// Registration entry point for the application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and use cases.</summary>
    public static IServiceCollection AddEtherApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<RegisterAccountHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateCharacterHandler>();
        services.AddScoped<GetCharacterHandler>();
        services.AddScoped<GetAccountCharactersHandler>();
        services.AddScoped<EnterWorldHandler>();
        services.AddScoped<MoveCharacterHandler>();
        services.AddScoped<RespawnCharacterHandler>();
        services.AddScoped<IssueGameTokenHandler>();

        // Combat (M5).
        services.TryAddSingleton<ICombatStatsProvider, LevelBasedCombatStatsProvider>();
        services.AddScoped<AttackCommandHandler>();

        // Creatures + AI (M6).
        services.AddSingleton<CreatureSpawnService>();
        services.AddScoped<AttackCreatureCommandHandler>();
        services.AddScoped<CreatureAiTickHandler>();

        // Progression + loot (M7).
        services.AddScoped<KillRewardService>();

        // Inventory (M8).
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<GetInventoryHandler>();

        return services;
    }
}
