using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Application.Abstractions;

/// <summary>
/// Live creature world state. Instances are transient (ADR-0005): they are
/// materialized from spawn definitions at world start and live in memory.
/// </summary>
public interface ICreatureWorld
{
    IReadOnlyList<CreatureInstance> GetByMap(MapId mapId);

    CreatureInstance? Get(CreatureInstanceId id);

    void Add(CreatureInstance creature);
}
