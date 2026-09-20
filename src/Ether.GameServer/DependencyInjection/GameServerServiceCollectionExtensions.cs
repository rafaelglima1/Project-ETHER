using Ether.GameServer.World;

using Microsoft.Extensions.DependencyInjection;

namespace Ether.GameServer.DependencyInjection;

/// <summary>
/// Registration entry point for the game server layer.
/// </summary>
public static class GameServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers game server services. The world loop is a placeholder at M0.
    /// </summary>
    public static IServiceCollection AddEtherGameServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<WorldLoopService>();

        return services;
    }
}
