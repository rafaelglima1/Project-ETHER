using System.Collections.Concurrent;

using Ether.Application.Abstractions;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Infrastructure.Memory;

/// <summary>
/// Process-local creature world (ADR-0005). Creature instances are transient world
/// state and are not persisted per instance.
/// </summary>
public sealed class InMemoryCreatureWorld : ICreatureWorld
{
    private readonly ConcurrentDictionary<Guid, CreatureInstance> _creatures = new();

    public void Add(CreatureInstance creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _creatures[creature.Id.Value] = creature;
    }

    public CreatureInstance? Get(CreatureInstanceId id) =>
        _creatures.TryGetValue(id.Value, out var creature) ? creature : null;

    public IReadOnlyList<CreatureInstance> GetByMap(MapId mapId) =>
        _creatures.Values.Where(creature => creature.MapId == mapId).ToList();
}
