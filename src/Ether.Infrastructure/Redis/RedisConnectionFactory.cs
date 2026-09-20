using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Ether.Contracts.Configuration;

namespace Ether.Infrastructure.Redis;

/// <summary>
/// Lazily creates and caches the Redis connection.
/// Redis is coordination/transient storage only — never a source of truth for
/// gold, items, XP, character or item ownership (Blueprint v5.0 §45).
/// </summary>
public sealed class RedisConnectionFactory : IDisposable, IAsyncDisposable
{
    private readonly RedisOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private bool _disposed;

    public RedisConnectionFactory(IOptions<RedisOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>
    /// Returns a connected multiplexer, creating it on first use.
    /// Throws when the dependency is configured but unreachable; callers that must
    /// not fail (e.g. readiness) are expected to handle that.
    /// </summary>
    public async Task<ConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsConnected: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsConnected: true })
            {
                return _connection;
            }

            var configuration = ConfigurationOptions.Parse(_options.ConnectionString);
            configuration.AbortOnConnectFail = _options.AbortOnConnectFail;
            configuration.ConnectTimeout = _options.ConnectTimeoutMs;

            _connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _connection?.Dispose();
        _connection = null;
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _gate.Dispose();
    }
}
