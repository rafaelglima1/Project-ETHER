using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Creatures;

/// <summary>
/// A live creature in the world (Blueprint v5.0 §18). Instances are transient world
/// state, not per-instance database rows (ADR-0005): they are recreated from spawn
/// definitions on world start and respawn after death.
/// </summary>
public sealed class CreatureInstance
{
    public CreatureInstance(
        CreatureInstanceId id,
        CreatureDefinitionId definitionId,
        MapId mapId,
        WorldPosition spawn,
        int maxHealth)
    {
        if (maxHealth < 1)
        {
            throw new DomainException("Creature max health must be positive.");
        }

        Id = id;
        DefinitionId = definitionId;
        MapId = mapId;
        SpawnPositionX = spawn.X;
        SpawnPositionY = spawn.Y;
        PositionX = spawn.X;
        PositionY = spawn.Y;
        MaxHealth = maxHealth;
        Health = maxHealth;
        State = CreatureState.Idle;
    }

    public CreatureInstanceId Id { get; }

    public CreatureDefinitionId DefinitionId { get; }

    public MapId MapId { get; }

    public int MaxHealth { get; }

    public int Health { get; private set; }

    public CreatureState State { get; private set; }

    public int PositionX { get; private set; }

    public int PositionY { get; private set; }

    public int SpawnPositionX { get; }

    public int SpawnPositionY { get; }

    public CharacterId? TargetCharacterId { get; private set; }

    public DateTimeOffset? LastAttackAt { get; private set; }

    public DateTimeOffset? RespawnAt { get; private set; }

    public bool IsAlive => State is not (CreatureState.Dead or CreatureState.Respawning) && Health > 0;

    public WorldPosition Position => new(MapId, PositionX, PositionY);

    public WorldPosition SpawnPosition => new(MapId, SpawnPositionX, SpawnPositionY);

    public void AcquireTarget(CharacterId target, DateTimeOffset now)
    {
        EnsureAlive();
        TargetCharacterId = target;
        TransitionTo(CreatureState.Chase, now);
    }

    public void BeginAttack(DateTimeOffset now)
    {
        EnsureAlive();
        if (TargetCharacterId is null)
        {
            throw new InvalidStateException("Creature cannot attack without a target.");
        }

        TransitionTo(CreatureState.Attack, now);
        LastAttackAt = now;
    }

    public void BeginReturn(DateTimeOffset now)
    {
        EnsureAlive();
        TargetCharacterId = null;
        TransitionTo(CreatureState.Return, now);
    }

    public void ReturnToIdle(DateTimeOffset now)
    {
        EnsureAlive();
        TargetCharacterId = null;
        TransitionTo(CreatureState.Idle, now);
    }

    /// <summary>Moves up to <paramref name="maxTiles"/> tiles toward a destination (Chebyshev).</summary>
    public bool StepToward(
        WorldPosition destination,
        int maxTiles,
        WorldMap map,
        DateTimeOffset now,
        CreatureState moveState = CreatureState.Chase)
    {
        EnsureAlive();

        if (maxTiles < 1 || destination.MapId != MapId)
        {
            return false;
        }

        var dx = Math.Sign(destination.X - PositionX);
        var dy = Math.Sign(destination.Y - PositionY);
        var steps = 0;

        while (steps < maxTiles && (PositionX != destination.X || PositionY != destination.Y))
        {
            var nextX = PositionX + dx;
            var nextY = PositionY + dy;
            var next = new WorldPosition(MapId, nextX, nextY);

            if (!map.Contains(next))
            {
                break;
            }

            PositionX = nextX;
            PositionY = nextY;
            steps++;
        }

        if (steps > 0)
        {
            TransitionTo(moveState, now);
            return true;
        }

        return false;
    }

    /// <summary>Applies damage; returns true when this hit killed the creature.</summary>
    public bool ApplyDamage(int damage, DateTimeOffset now, TimeSpan respawnDelay)
    {
        EnsureAlive();

        if (damage < 0)
        {
            throw new DomainException("Damage must not be negative.");
        }

        Health = Math.Max(0, Health - damage);

        if (Health > 0)
        {
            return false;
        }

        TargetCharacterId = null;
        TransitionTo(CreatureState.Dead, now);
        RespawnAt = now + respawnDelay;
        return true;
    }

    /// <summary>Brings the creature back at its spawn point.</summary>
    public void Respawn(DateTimeOffset now)
    {
        if (State is not (CreatureState.Dead or CreatureState.Respawning))
        {
            throw new InvalidStateException($"Creature cannot respawn from state {State}.");
        }

        Health = MaxHealth;
        PositionX = SpawnPositionX;
        PositionY = SpawnPositionY;
        TargetCharacterId = null;
        LastAttackAt = null;
        RespawnAt = null;
        TransitionTo(CreatureState.Idle, now);
    }

    private void EnsureAlive()
    {
        if (!IsAlive)
        {
            throw new InvalidStateException("Creature is not alive.");
        }
    }

    private void TransitionTo(CreatureState next, DateTimeOffset now)
    {
        if (next == State)
        {
            return;
        }

        if (!IsTransitionAllowed(State, next))
        {
            throw new InvalidStateException($"Creature state cannot transition from {State} to {next}.");
        }

        State = next;
    }

    private static bool IsTransitionAllowed(CreatureState from, CreatureState to) =>
        (from, to) switch
        {
            (CreatureState.Idle, CreatureState.Chase) => true,
            (CreatureState.Idle, CreatureState.Return) => true,
            (CreatureState.Patrol, CreatureState.Chase) => true,
            (CreatureState.Patrol, CreatureState.Return) => true,
            (CreatureState.Investigate, CreatureState.Chase) => true,
            (CreatureState.Investigate, CreatureState.Return) => true,
            (CreatureState.Chase, CreatureState.Attack) => true,
            (CreatureState.Chase, CreatureState.Return) => true,
            (CreatureState.Chase, CreatureState.Idle) => true,
            (CreatureState.Attack, CreatureState.Chase) => true,
            (CreatureState.Attack, CreatureState.Return) => true,
            (CreatureState.Attack, CreatureState.Idle) => true,
            (CreatureState.Return, CreatureState.Idle) => true,
            (CreatureState.Return, CreatureState.Chase) => true,
            (CreatureState.Flee, CreatureState.Return) => true,
            (CreatureState.Flee, CreatureState.Idle) => true,
            // Any alive state can die; death can lead to respawn.
            (_, CreatureState.Dead) => from is not (CreatureState.Dead or CreatureState.Respawning),
            (CreatureState.Dead, CreatureState.Respawning) => true,
            (CreatureState.Dead, CreatureState.Idle) => true,
            (CreatureState.Respawning, CreatureState.Idle) => true,
            _ => false,
        };
}
