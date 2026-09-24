using Ether.Application.Abstractions;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Application.Creatures;

/// <summary>
/// Materializes spawn definitions into live creature instances. Idempotent: a spawn
/// point that already has a live instance is not duplicated.
/// </summary>
public sealed class CreatureSpawnService
{
    private readonly ICreatureWorld _world;
    private readonly ICreatureSpawnCatalog _spawns;
    private readonly ICreatureCatalog _creatures;

    public CreatureSpawnService(ICreatureWorld world, ICreatureSpawnCatalog spawns, ICreatureCatalog creatures)
    {
        _world = world;
        _spawns = spawns;
        _creatures = creatures;
    }

    public void EnsureSpawned(MapId mapId)
    {
        var existing = _world.GetByMap(mapId);
        var occupied = existing
            .Select(creature => (creature.DefinitionId, creature.SpawnPositionX, creature.SpawnPositionY))
            .ToHashSet();

        foreach (var spawn in _spawns.ForMap(mapId))
        {
            var key = (spawn.DefinitionId, spawn.X, spawn.Y);
            if (occupied.Contains(key))
            {
                continue;
            }

            var definition = _creatures.Get(spawn.DefinitionId);
            var position = new WorldPosition(spawn.MapId, spawn.X, spawn.Y);

            _world.Add(new CreatureInstance(
                CreatureInstanceId.New(),
                definition.Id,
                mapId,
                position,
                definition.MaxHealth));
        }
    }
}
