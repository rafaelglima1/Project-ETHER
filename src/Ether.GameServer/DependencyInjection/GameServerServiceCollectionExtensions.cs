using Ether.GameServer.Handlers;
using Ether.GameServer.Protocol;
using Ether.GameServer.Realtime;
using Ether.GameServer.Sessions;
using Ether.GameServer.World;

using Microsoft.Extensions.DependencyInjection;

namespace Ether.GameServer.DependencyInjection;

/// <summary>
/// Registration entry point for the game server layer.
/// </summary>
public static class GameServerServiceCollectionExtensions
{
    /// <summary>Registers the realtime protocol, sessions and command handlers.</summary>
    public static IServiceCollection AddEtherGameServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Stateless protocol services.
        services.AddSingleton<ProtocolSerializer>();
        services.AddSingleton<ProtocolDispatcher>();
        services.AddSingleton<GameSessionManager>();
        services.AddScoped<WorldSnapshotFactory>();

        // Command handlers (resolved from a scope per command).
        services.AddScoped<IProtocolCommandHandler, AuthenticateCommandHandler>();
        services.AddScoped<IProtocolCommandHandler, EnterWorldCommandHandler>();
        services.AddScoped<IProtocolCommandHandler, MovementCommandHandler>();
        services.AddScoped<IProtocolCommandHandler, CombatAttackCommandHandler>();
        services.AddScoped<IProtocolCommandHandler, RespawnCommandHandler>();
        services.AddScoped<IProtocolCommandHandler, PingCommandHandler>();

        // Lifecycle.
        services.AddHostedService<GameSessionHeartbeatService>();
        services.AddHostedService<CreatureAiTickService>();
        services.AddHostedService<WorldLoopService>();

        return services;
    }
}
