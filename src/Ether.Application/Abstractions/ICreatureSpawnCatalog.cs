using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Application.Abstractions;

/// <summary>Read-only creature spawn definitions for a map.</summary>
public interface ICreatureSpawnCatalog
{
    IReadOnlyList<CreatureSpawnDefinition> ForMap(MapId mapId);
}
