using Ether.Application.Abstractions;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Application.Tests.Fakes;

/// <summary>In-memory creature world for application tests.</summary>
internal sealed class FakeCreatureWorld : ICreatureWorld
{
    private readonly List<CreatureInstance> _creatures = [];

    public IReadOnlyList<CreatureInstance> All => _creatures;

    public void Add(CreatureInstance creature) => _creatures.Add(creature);

    public CreatureInstance? Get(CreatureInstanceId id) =>
        _creatures.FirstOrDefault(creature => creature.Id == id);

    public IReadOnlyList<CreatureInstance> GetByMap(MapId mapId) =>
        _creatures.Where(creature => creature.MapId == mapId).ToList();

    public CreatureInstance Seed(CreatureDefinitionId definitionId, int x, int y)
    {
        var definition = TestGameContentCatalog.Instance.Get(definitionId);
        var creature = new CreatureInstance(
            CreatureInstanceId.New(), definitionId, new MapId(1), new WorldPosition(new MapId(1), x, y), definition.MaxHealth);
        _creatures.Add(creature);
        return creature;
    }
}
