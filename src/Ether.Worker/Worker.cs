using System.Globalization;

namespace Ether.Worker;

/// <summary>
/// M0 placeholder worker. It keeps the host alive and publishes a liveness
/// heartbeat file so container health checks can observe the process.
/// Real asynchronous jobs (outbox, notifications, analytics) arrive in later milestones.
/// </summary>
public sealed class Worker : BackgroundService
{
    public const string HeartbeatFileName = "ether-worker.alive";

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<Worker> _logger;
    private readonly string _heartbeatPath;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _heartbeatPath = Path.Combine(Path.GetTempPath(), HeartbeatFileName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ether.Worker started (M0 placeholder).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                await File.WriteAllTextAsync(_heartbeatPath, timestamp, stoppingToken).ConfigureAwait(false);
                await Task.Delay(HeartbeatInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
