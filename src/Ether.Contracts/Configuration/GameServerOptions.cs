namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>GameServer</c> configuration section.
/// Tick rates are initial values and must be validated by profiling (Blueprint v5.0 §41/§59).
/// </summary>
public sealed class GameServerOptions
{
    public const string SectionName = "GameServer";

    /// <summary>World simulation tick rate, in Hz (initial: 20).</summary>
    public int TickRateHz { get; set; } = 20;

    /// <summary>Creature AI tick rate, in Hz (initial: 5).</summary>
    public int AiTickRateHz { get; set; } = 5;

    /// <summary>Radius, in tiles, used for interest management / replication.</summary>
    public int InterestRadius { get; set; } = 30;

    /// <summary>Grace period after disconnect before persisting safe state, in seconds.</summary>
    public int DisconnectGraceSeconds { get; set; } = 30;

    /// <summary>Server heartbeat interval, in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 20;

    /// <summary>Maximum silence before a session is considered dead, in seconds.</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 60;

    /// <summary>Maximum accepted inbound frame size, in bytes.</summary>
    public int MaxMessageSizeBytes { get; set; } = 16 * 1024;

    /// <summary>Maximum concurrent realtime sessions.</summary>
    public int MaxConnections { get; set; } = 1000;

    /// <summary>Maximum accepted movement commands per session per second.</summary>
    public int MaxMovementCommandsPerSecond { get; set; } = 20;

    /// <summary>Maximum queued commands per session before new ones are dropped.</summary>
    public int MaxQueuedCommands { get; set; } = 256;
}
