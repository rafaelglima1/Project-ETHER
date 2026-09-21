using Ether.Contracts.Configuration;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Realtime;

/// <summary>
/// Closes sessions that stopped sending anything within the configured timeout.
/// Client pings (system.ping) and any other message refresh the heartbeat.
/// </summary>
internal sealed class GameSessionHeartbeatService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

    private readonly GameSessionManager _sessions;
    private readonly TimeProvider _timeProvider;
    private readonly GameServerOptions _options;
    private readonly ILogger<GameSessionHeartbeatService> _logger;

    public GameSessionHeartbeatService(
        GameSessionManager sessions,
        TimeProvider timeProvider,
        IOptions<GameServerOptions> options,
        ILogger<GameSessionHeartbeatService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _sessions = sessions;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();

            foreach (var session in _sessions.Snapshot())
            {
                if (session.State == GameSessionState.Disconnected)
                {
                    continue;
                }

                if (now - session.LastHeartbeat > timeout)
                {
                    _logger.LogWarning("Heartbeat timeout {SessionId}", session.SessionId);
                    session.MarkDisconnecting();

                    try
                    {
                        session.Lifetime.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Session already cleaned up.
                    }
                }
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
