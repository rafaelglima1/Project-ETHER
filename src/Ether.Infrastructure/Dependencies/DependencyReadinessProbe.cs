using Ether.Application.Abstractions;
using Ether.Application.Readiness;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.Persistence;
using Ether.Infrastructure.Redis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Dependencies;

/// <summary>
/// Readiness probe for configured dependencies.
/// Reports failures instead of throwing, so <c>/ready</c> can fail gracefully
/// and visibly when PostgreSQL or Redis is unavailable (M0 environment blocker).
/// </summary>
public sealed class DependencyReadinessProbe : IDependencyReadinessProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly DatabaseOptions _databaseOptions;
    private readonly RedisOptions _redisOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisConnectionFactory _redisConnectionFactory;

    public DependencyReadinessProbe(
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<RedisOptions> redisOptions,
        IServiceScopeFactory scopeFactory,
        RedisConnectionFactory redisConnectionFactory)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(redisOptions);

        _databaseOptions = databaseOptions.Value;
        _redisOptions = redisOptions.Value;
        _scopeFactory = scopeFactory;
        _redisConnectionFactory = redisConnectionFactory;
    }

    public async Task<ReadinessReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var dependencies = new List<DependencyStatus>
        {
            await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false),
            await CheckRedisAsync(cancellationToken).ConfigureAwait(false),
        };

        return ReadinessReport.FromDependencies(dependencies);
    }

    private async Task<DependencyStatus> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!_databaseOptions.IsConfigured)
        {
            return DependencyStatus.NotConfigured("PostgreSQL");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EtherDbContext>();

            var canConnect = await context.Database.CanConnectAsync(timeout.Token).ConfigureAwait(false);

            return canConnect
                ? DependencyStatus.Ok("PostgreSQL")
                : DependencyStatus.Failed("PostgreSQL", "connection refused");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DependencyStatus.Failed("PostgreSQL", "connection timed out");
        }
        catch (Exception ex)
        {
            return DependencyStatus.Failed("PostgreSQL", ex.GetType().Name);
        }
    }

    private async Task<DependencyStatus> CheckRedisAsync(CancellationToken cancellationToken)
    {
        if (!_redisOptions.IsConfigured)
        {
            return DependencyStatus.NotConfigured("Redis");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);

            var connection = await _redisConnectionFactory.GetConnectionAsync(timeout.Token).ConfigureAwait(false);
            var database = connection.GetDatabase();
            await database.PingAsync().ConfigureAwait(false);

            return DependencyStatus.Ok("Redis");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DependencyStatus.Failed("Redis", "connection timed out");
        }
        catch (Exception ex)
        {
            return DependencyStatus.Failed("Redis", ex.GetType().Name);
        }
    }
}
