using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Domain.Creatures;

/// <summary>Where a creature (re)spawns. Content-defined; see ADR-0005.</summary>
public sealed record CreatureSpawnDefinition(int MapId, int X, int Y, CreatureDefinitionId DefinitionId);

/// <summary>M6 spawn table for the first playable map.</summary>
public static class CreatureSpawnCatalog
{
    private static readonly CreatureSpawnDefinition[] Spawns =
    [
        new(1, 6, 6, new CreatureDefinitionId("creature.slime")),
        new(1, 9, 4, new CreatureDefinitionId("creature.slime")),
        new(1, 14, 11, new CreatureDefinitionId("creature.wolf")),
        new(1, 22, 18, new CreatureDefinitionId("creature.spider")),
    ];

    public static IReadOnlyList<CreatureSpawnDefinition> ForMap(MapId mapId) =>
        Spawns.Where(spawn => spawn.MapId == mapId.Value).ToList();
}
