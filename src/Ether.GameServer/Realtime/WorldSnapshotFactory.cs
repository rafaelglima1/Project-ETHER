using Ether.Application.Abstractions;
using Ether.Application.Creatures;
using Ether.Contracts.Characters;
using Ether.Contracts.Realtime;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.GameServer.Realtime;

/// <summary>
/// Builds the world snapshot for a character (map, player state and living
/// creatures). Shared by world.enter and character.respawn so the payload stays
/// identical after either transition.
/// </summary>
public sealed class WorldSnapshotFactory
{
    private readonly IWorldMapProvider _maps;
    private readonly ICreatureWorld _creatures;
    private readonly ICreatureCatalog _creatureCatalog;
    private readonly IItemCatalog _itemCatalog;
    private readonly CreatureSpawnService _spawner;
    private readonly IInventoryService _inventory;
    private readonly TimeProvider _timeProvider;

    public WorldSnapshotFactory(
        IWorldMapProvider maps,
        ICreatureWorld creatures,
        ICreatureCatalog creatureCatalog,
        IItemCatalog itemCatalog,
        CreatureSpawnService spawner,
        IInventoryService inventory,
        TimeProvider timeProvider)
    {
        _maps = maps;
        _creatures = creatures;
        _creatureCatalog = creatureCatalog;
        _itemCatalog = itemCatalog;
        _spawner = spawner;
        _inventory = inventory;
        _timeProvider = timeProvider;
    }

    public async Task<WorldSnapshotPayload> BuildAsync(CharacterResponse character, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(character);

        var mapId = new MapId(character.MapId);
        var map = _maps.GetMap(mapId);

        _spawner.EnsureSpawned(mapId);
        var creatures = _creatures.GetByMap(mapId)
            .Where(creature => creature.IsAlive)
            .Select(ToSnapshot)
            .ToList();

        var characterId = new Ether.Domain.Characters.CharacterId(character.CharacterId);
        var inventory = await _inventory.GetInventoryAsync(characterId, cancellationToken).ConfigureAwait(false);

        var inventoryItems = inventory
            .Select(item =>
            {
                var definition = _itemCatalog.Get(item.DefinitionId);
                return new InventoryItemResponse(
                    item.DefinitionId.Value,
                    definition.Name,
                    item.Quantity,
                    definition.MaxStack,
                    definition.Stackable,
                    item.Location.ToString());
            })
            .ToList();

        return new WorldSnapshotPayload(
            character.MapId,
            map.Width,
            map.Height,
            new PlayerSnapshotPayload(
                character.CharacterId,
                character.PositionX,
                character.PositionY,
                character.State,
                character.Level,
                character.Experience,
                character.ExperienceToNextLevel,
                character.Health,
                character.MaxHealth),
            _timeProvider.GetUtcNow(),
            creatures,
            inventoryItems);
    }

    private CreatureSnapshotPayload ToSnapshot(CreatureInstance creature)
    {
        var definition = _creatureCatalog.Get(creature.DefinitionId);

        return new CreatureSnapshotPayload(
            creature.Id.Value,
            creature.DefinitionId.Value,
            definition.Name,
            creature.PositionX,
            creature.PositionY,
            creature.Health,
            creature.MaxHealth,
            creature.State.ToString());
    }
}
