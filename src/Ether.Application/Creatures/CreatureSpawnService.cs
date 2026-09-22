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

    public CreatureSpawnService(ICreatureWorld world)
    {
        _world = world;
    }

    public void EnsureSpawned(MapId mapId)
    {
        var existing = _world.GetByMap(mapId);
        var occupied = existing
            .Select(creature => (creature.DefinitionId, creature.SpawnPositionX, creature.SpawnPositionY))
            .ToHashSet();

        foreach (var spawn in CreatureSpawnCatalog.ForMap(mapId))
        {
            var key = (spawn.DefinitionId, spawn.X, spawn.Y);
            if (occupied.Contains(key))
            {
                continue;
            }

            var definition = CreatureCatalog.Get(spawn.DefinitionId);
            var position = new WorldPosition(mapId, spawn.X, spawn.Y);

            _world.Add(new CreatureInstance(
                CreatureInstanceId.New(),
                definition.Id,
                mapId,
                position,
                definition.MaxHealth));
        }
    }
}
