using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Creatures;

/// <summary>
/// One AI tick for a map: respawns due creatures, then lets each live creature
/// chase/attack the nearest eligible player or return home. All rules come from
/// <see cref="CreatureAi"/> and <see cref="DamageCalculator"/>; this handler only
/// orchestrates. Character changes are persisted once per tick.
/// </summary>
public sealed class CreatureAiTickHandler
{
    private readonly ICreatureWorld _world;
    private readonly CreatureSpawnService _spawner;
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICombatStatsProvider _characterStats;
    private readonly IAbilityCooldownStore _cooldowns;
    private readonly IEntityLockProvider _locks;
    private readonly IRandomSource _random;
    private readonly IWorldMapProvider _maps;
    private readonly CombatOptions _options;
    private readonly TimeProvider _timeProvider;

    public CreatureAiTickHandler(
        ICreatureWorld world,
        CreatureSpawnService spawner,
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        ICombatStatsProvider characterStats,
        IAbilityCooldownStore cooldowns,
        IEntityLockProvider locks,
        IRandomSource random,
        IWorldMapProvider maps,
        IOptions<CombatOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _world = world;
        _spawner = spawner;
        _characters = characters;
        _unitOfWork = unitOfWork;
        _characterStats = characterStats;
        _cooldowns = cooldowns;
        _locks = locks;
        _random = random;
        _maps = maps;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CreatureAiTickResult> HandleAsync(MapId mapId, CancellationToken cancellationToken)
    {
        _spawner.EnsureSpawned(mapId);

        var map = _maps.GetMap(mapId);
        var creatures = _world.GetByMap(mapId);
        var characters = await _characters.GetByMapAsync(mapId, cancellationToken).ConfigureAwait(false);

        var targets = characters
            .Where(character => character.IsAlive && character.State is CharacterState.InWorld or CharacterState.Combat)
            .ToList();

        var now = _timeProvider.GetUtcNow();
        var rules = new DamageRules(_options.MinimumDamage, _options.ResistanceCap);
        var moves = new List<CreatureMovedEvent>();
        var attacks = new List<CreatureAttackedCharacterEvent>();
        var charactersChanged = false;

        foreach (var creature in creatures)
        {
            var definition = CreatureCatalog.Get(creature.DefinitionId);

            if (!creature.IsAlive)
            {
                if (creature.RespawnAt is { } respawnAt && respawnAt <= now)
                {
                    creature.Respawn(now);
                    moves.Add(ToMovedEvent(creature, mapId));
                }

                continue;
            }

            var nearest = FindNearest(creature, targets);
            var decision = CreatureAi.Decide(definition, creature, nearest);

            switch (decision.Action)
            {
                case CreatureAiAction.Move when nearest is { } candidate:
                    creature.AcquireTarget(new CharacterId(candidate.CharacterId), now);
                    if (Step(creature, definition, map, decision.DestinationX, decision.DestinationY, now))
                    {
                        moves.Add(ToMovedEvent(creature, mapId));
                    }

                    break;

                case CreatureAiAction.Attack when nearest is { } attackTarget:
                    var attack = await TryAttackAsync(creature, definition, attackTarget, characters, now, rules, cancellationToken)
                        .ConfigureAwait(false);
                    if (attack is not null)
                    {
                        attacks.Add(attack);
                        charactersChanged = true;
                    }

                    break;

                case CreatureAiAction.Return:
                    creature.BeginReturn(now);
                    Step(creature, definition, map, decision.DestinationX, decision.DestinationY, now, CreatureState.Return);
                    if (creature.Position == creature.SpawnPosition)
                    {
                        creature.ReturnToIdle(now);
                    }

                    moves.Add(ToMovedEvent(creature, mapId));
                    break;
            }
        }

        if (charactersChanged)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new CreatureAiTickResult(moves, attacks);
    }

    private async Task<CreatureAttackedCharacterEvent?> TryAttackAsync(
        CreatureInstance creature,
        CreatureDefinition definition,
        CreatureTargetInfo target,
        IReadOnlyList<Character> characters,
        DateTimeOffset now,
        DamageRules rules,
        CancellationToken cancellationToken)
    {
        await using var handle = await _locks
            .AcquireAsync([creature.Id.Value, target.CharacterId], cancellationToken)
            .ConfigureAwait(false);

        var character = characters.FirstOrDefault(candidate => candidate.Id.Value == target.CharacterId);
        if (character is null || !character.IsAlive)
        {
            return null;
        }

        var ability = definition.BasicAttack();
        if (!_cooldowns.IsReady(creature.Id.Value, ability.Id, now))
        {
            return null;
        }

        _cooldowns.Record(creature.Id.Value, ability.Id, ability.Cooldown, now);
        creature.AcquireTarget(new CharacterId(target.CharacterId), now);
        creature.BeginAttack(now);

        var damage = DamageCalculator.Calculate(
            definition.AttackStats(),
            _characterStats.For(character),
            ability,
            _random.NextUnit(),
            rules);

        var defeated = character.ApplyDamage(damage.Damage, now);

        return new CreatureAttackedCharacterEvent(
            creature.Id.Value,
            character.Id.Value,
            ability.Id.Value,
            damage.RawDamage,
            damage.Damage,
            damage.Critical,
            character.Health,
            character.MaxHealth,
            character.State.ToString(),
            defeated);
    }

    private static bool Step(
        CreatureInstance creature,
        CreatureDefinition definition,
        WorldMap map,
        int destinationX,
        int destinationY,
        DateTimeOffset now,
        CreatureState moveState = CreatureState.Chase)
    {
        var tiles = Math.Max(1, (int)Math.Floor(definition.MoveSpeed));
        return creature.StepToward(new WorldPosition(creature.MapId, destinationX, destinationY), tiles, map, now, moveState);
    }

    private static CreatureTargetInfo? FindNearest(CreatureInstance creature, IReadOnlyList<Character> targets)
    {
        CreatureTargetInfo? nearest = null;
        var bestDistance = int.MaxValue;

        foreach (var character in targets)
        {
            var distance = Math.Max(
                Math.Abs(character.PositionX - creature.PositionX),
                Math.Abs(character.PositionY - creature.PositionY));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = new CreatureTargetInfo(character.Id.Value, character.PositionX, character.PositionY, character.IsAlive, true);
            }
        }

        return nearest;
    }

    private static CreatureMovedEvent ToMovedEvent(CreatureInstance creature, MapId mapId) =>
        new(
            creature.Id.Value,
            mapId.Value,
            creature.PositionX,
            creature.PositionY,
            creature.Health,
            creature.MaxHealth,
            creature.State.ToString(),
            creature.TargetCharacterId?.Value);
}
