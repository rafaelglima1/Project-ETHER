using Ether.Domain.Characters;
using Ether.Domain.World;

namespace Ether.Domain.Creatures;

/// <summary>What the AI wants to do on this tick.</summary>
public enum CreatureAiAction
{
    None = 0,
    Move = 1,
    Attack = 2,
    Return = 3,
}

/// <summary>A resolved AI decision (pure output, no side effects).</summary>
public readonly record struct CreatureAiDecision(
    CreatureAiAction Action,
    int DestinationX,
    int DestinationY,
    CharacterId? TargetCharacterId)
{
    public static CreatureAiDecision None { get; } = new(CreatureAiAction.None, 0, 0, null);
}

/// <summary>Knowledge about a possible target (a player character).</summary>
public readonly record struct CreatureTargetInfo(Guid CharacterId, int X, int Y, bool Alive, bool InWorld);

/// <summary>
/// Pure creature AI: given the definition, the instance and the nearest candidate
/// target, decides the next action. No I/O, no randomness — fully testable.
/// </summary>
public static class CreatureAi
{
    public static CreatureAiDecision Decide(
        CreatureDefinition definition,
        CreatureInstance creature,
        CreatureTargetInfo? target)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(creature);

        if (!creature.IsAlive)
        {
            return CreatureAiDecision.None;
        }

        var distanceFromSpawn = Chebyshev(creature.PositionX, creature.PositionY, creature.SpawnPositionX, creature.SpawnPositionY);

        if (target is not { Alive: true, InWorld: true } candidate)
        {
            // No valid target: return home if we strayed, otherwise idle.
            return distanceFromSpawn > definition.LeashRange
                ? new CreatureAiDecision(CreatureAiAction.Return, creature.SpawnPositionX, creature.SpawnPositionY, null)
                : CreatureAiDecision.None;
        }

        var distance = Chebyshev(creature.PositionX, creature.PositionY, candidate.X, candidate.Y);

        if (distance <= definition.AttackRange)
        {
            return new CreatureAiDecision(CreatureAiAction.Attack, candidate.X, candidate.Y, new CharacterId(candidate.CharacterId));
        }

        if (distance <= definition.AggroRange)
        {
            return new CreatureAiDecision(CreatureAiAction.Move, candidate.X, candidate.Y, new CharacterId(candidate.CharacterId));
        }

        // Target escaped: leash back if we wandered too far.
        return distanceFromSpawn > definition.LeashRange
            ? new CreatureAiDecision(CreatureAiAction.Return, creature.SpawnPositionX, creature.SpawnPositionY, null)
            : CreatureAiDecision.None;
    }

    private static int Chebyshev(int ax, int ay, int bx, int by) =>
        Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
}
