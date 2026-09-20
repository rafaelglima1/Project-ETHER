using Ether.Contracts.Configuration;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.World;

/// <summary>
/// Placeholder for the authoritative world loop.
/// M0 wires the lifecycle only; commands, simulation, AI and replication arrive in
/// later milestones. The loop must never be tied to the client frame rate.
/// </summary>
public sealed class WorldLoopService : BackgroundService
{
    private readonly ILogger<WorldLoopService> _logger;
    private readonly GameServerOptions _options;

    public WorldLoopService(ILogger<WorldLoopService> logger, IOptions<GameServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "World loop placeholder initialized (tick {TickRateHz} Hz, AI {AiTickRateHz} Hz). No gameplay at M0.",
            _options.TickRateHz,
            _options.AiTickRateHz);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown.
        }
    }
}
