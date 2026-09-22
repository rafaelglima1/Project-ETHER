namespace Ether.Application.Creatures;

/// <summary>A creature moved during an AI tick.</summary>
public sealed record CreatureMovedEvent(
    Guid CreatureId,
    int MapId,
    int X,
    int Y,
    int Health,
    int MaxHealth,
    string State,
    Guid? TargetCharacterId);

/// <summary>A creature hit a character during an AI tick.</summary>
public sealed record CreatureAttackedCharacterEvent(
    Guid CreatureId,
    Guid TargetCharacterId,
    string AbilityId,
    int RawDamage,
    int Damage,
    bool Critical,
    int TargetHealth,
    int TargetMaxHealth,
    string TargetState,
    bool TargetDefeated);

/// <summary>Everything that happened during one AI tick for a map.</summary>
public sealed record CreatureAiTickResult(
    IReadOnlyList<CreatureMovedEvent> Moves,
    IReadOnlyList<CreatureAttackedCharacterEvent> Attacks)
{
    public static CreatureAiTickResult Empty { get; } = new([], []);
}
