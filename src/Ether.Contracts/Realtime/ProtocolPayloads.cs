namespace Ether.Contracts.Realtime;

// Client -> server command payloads.

/// <summary>Payload of <see cref="ProtocolMessageNames.GameAuthenticate"/>.</summary>
public sealed record AuthenticateCommandPayload(string GameToken);

/// <summary>Payload of <see cref="ProtocolMessageNames.WorldEnter"/> (currently empty).</summary>
public sealed record WorldEnterCommandPayload;

/// <summary>Payload of <see cref="ProtocolMessageNames.MovementMove"/>.</summary>
public sealed record MovementMoveCommandPayload(int X, int Y);

// Server -> client event payloads.

/// <summary>Payload of <see cref="ProtocolMessageNames.GameAuthenticated"/>.</summary>
public sealed record AuthenticatedEventPayload(Guid SessionId, Guid AccountId, Guid CharacterId);

/// <summary>Player state inside a world snapshot.</summary>
public sealed record PlayerSnapshotPayload(Guid CharacterId, int X, int Y, string State);

/// <summary>Creature state inside a world snapshot.</summary>
public sealed record CreatureSnapshotPayload(
    Guid CreatureId,
    string DefinitionId,
    string Name,
    int X,
    int Y,
    int Health,
    int MaxHealth,
    string State);

/// <summary>Payload of <see cref="ProtocolMessageNames.WorldSnapshot"/>.</summary>
public sealed record WorldSnapshotPayload(
    int MapId,
    int Width,
    int Height,
    PlayerSnapshotPayload Player,
    DateTimeOffset ServerTime,
    IReadOnlyList<CreatureSnapshotPayload>? Creatures = null);

/// <summary>Payload of <see cref="ProtocolMessageNames.WorldCreatureMoved"/>.</summary>
public sealed record WorldCreatureMovedEventPayload(
    Guid CreatureId,
    int MapId,
    int X,
    int Y,
    int Health,
    int MaxHealth,
    string State);

/// <summary>Payload of <see cref="ProtocolMessageNames.MovementAccepted"/>.</summary>
public sealed record MovementAcceptedEventPayload(Guid CharacterId, int MapId, int X, int Y);

/// <summary>Payload of <see cref="ProtocolMessageNames.ProtocolError"/>.</summary>
public sealed record ProtocolErrorPayload(string Code, string Message);
