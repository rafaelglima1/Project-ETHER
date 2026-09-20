namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Database</c> configuration section.
/// Values must come from environment variables / local configuration — never versioned.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>PostgreSQL connection string. Empty means "not configured".</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum retry count for transient failures.</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Enables detailed EF Core errors. Must stay false outside Development.</summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>Indicates whether a usable connection string was provided.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
