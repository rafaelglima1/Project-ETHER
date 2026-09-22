using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Creatures;

public sealed class CreatureAiTests
{
    private static readonly CreatureDefinition Slime = CreatureCatalog.Get(new CreatureDefinitionId("creature.slime"));

    private static CreatureInstance Spawn(int x = 6, int y = 6)
    {
        var creature = new CreatureInstance(
            CreatureInstanceId.New(), Slime.Id, new MapId(1), new WorldPosition(new MapId(1), x, y), Slime.MaxHealth);
        return creature;
    }

    private static CreatureTargetInfo Target(int x, int y) =>
        new(Guid.NewGuid(), x, y, Alive: true, InWorld: true);

    [Fact]
    public void Idles_when_there_is_no_target()
    {
        var decision = CreatureAi.Decide(Slime, Spawn(), null);

        Assert.Equal(CreatureAiAction.None, decision.Action);
    }

    [Fact]
    public void Chases_a_target_inside_aggro_range()
    {
        // Slime aggro 6, attack range 1 → target 4 tiles away.
        var decision = CreatureAi.Decide(Slime, Spawn(), Target(10, 6));

        Assert.Equal(CreatureAiAction.Move, decision.Action);
        Assert.Equal(10, decision.DestinationX);
    }

    [Fact]
    public void Attacks_a_target_inside_attack_range()
    {
        var decision = CreatureAi.Decide(Slime, Spawn(), Target(7, 6));

        Assert.Equal(CreatureAiAction.Attack, decision.Action);
        Assert.NotNull(decision.TargetCharacterId);
    }

    [Fact]
    public void Returns_home_when_leashed()
    {
        // Move the creature far from its spawn (spawn 6,6 → now 6,25, leash 10).
        var creature = Spawn();
        creature.AcquireTarget(Ether.Domain.Characters.CharacterId.New(), DateTimeOffset.UtcNow);
        var map = new WorldMap(new MapId(1), 32, 32);
        creature.StepToward(new WorldPosition(new MapId(1), 6, 25), maxTiles: 30, map, DateTimeOffset.UtcNow);

        var decision = CreatureAi.Decide(Slime, creature, null);

        Assert.Equal(CreatureAiAction.Return, decision.Action);
        Assert.Equal(creature.SpawnPositionX, decision.DestinationX);
        Assert.Equal(creature.SpawnPositionY, decision.DestinationY);
    }

    [Fact]
    public void Ignores_a_dead_target()
    {
        var decision = CreatureAi.Decide(
            Slime,
            Spawn(),
            new CreatureTargetInfo(Guid.NewGuid(), 7, 6, Alive: false, InWorld: true));

        Assert.Equal(CreatureAiAction.None, decision.Action);
    }

    [Fact]
    public void Dead_creature_does_nothing()
    {
        var creature = Spawn();
        creature.ApplyDamage(999, DateTimeOffset.UtcNow, Slime.RespawnDelay);

        var decision = CreatureAi.Decide(Slime, creature, Target(7, 6));

        Assert.Equal(CreatureAiAction.None, decision.Action);
    }
}
