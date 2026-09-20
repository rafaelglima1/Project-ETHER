namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Redis</c> configuration section.
/// Redis is coordination/transient storage only — never a source of truth for
/// gold, items, XP, character or item ownership (Blueprint v5.0 §45).
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string. Empty means "not configured".</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Optional key prefix applied to all keys.</summary>
    public string InstanceName { get; set; } = "ether:";

    /// <summary>When false, connection failures do not throw on first use.</summary>
    public bool AbortOnConnectFail { get; set; } = true;

    /// <summary>Connect timeout in milliseconds.</summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>Indicates whether a usable connection string was provided.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
