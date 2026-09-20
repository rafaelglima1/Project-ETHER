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
}
