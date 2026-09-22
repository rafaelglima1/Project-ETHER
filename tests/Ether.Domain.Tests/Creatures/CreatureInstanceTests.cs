using Ether.Domain.Common;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Creatures;

public sealed class CreatureCatalogTests
{
    [Fact]
    public void Catalog_exposes_the_m6_creatures()
    {
        Assert.True(CreatureCatalog.TryGet(new CreatureDefinitionId("creature.slime"), out var slime));
        Assert.Equal("Slime", slime.Name);
        Assert.True(CreatureCatalog.All.Count >= 3);
    }

    [Fact]
    public void Unknown_definition_is_rejected()
    {
        Assert.Throws<DomainException>(() => CreatureCatalog.Get(new CreatureDefinitionId("creature.nope")));
    }

    [Fact]
    public void Basic_attack_reuses_the_ability_contract()
    {
        var definition = CreatureCatalog.Get(new CreatureDefinitionId("creature.wolf"));
        var attack = definition.BasicAttack();

        Assert.Equal(definition.AttackRange, attack.Range);
        Assert.Equal(definition.AttackCooldown, attack.Cooldown);
        Assert.Equal(definition.Id.Value + ".attack", attack.Id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_definition_id_is_rejected(string value)
    {
        Assert.Throws<DomainException>(() => { _ = new CreatureDefinitionId(value); });
    }

    [Fact]
    public void Spawn_catalog_targets_existing_definitions()
    {
        var spawns = CreatureSpawnCatalog.ForMap(new MapId(1));

        Assert.NotEmpty(spawns);
        Assert.All(spawns, spawn => Assert.True(CreatureCatalog.TryGet(spawn.DefinitionId, out _)));
    }
}

public sealed class CreatureInstanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly CreatureDefinition Slime = CreatureCatalog.Get(new CreatureDefinitionId("creature.slime"));

    private static CreatureInstance Spawn() =>
        new(CreatureInstanceId.New(), Slime.Id, new MapId(1), new WorldPosition(new MapId(1), 6, 6), Slime.MaxHealth);

    [Fact]
    public void Starts_idle_and_alive_at_spawn()
    {
        var creature = Spawn();

        Assert.Equal(CreatureState.Idle, creature.State);
        Assert.True(creature.IsAlive);
        Assert.Equal(6, creature.PositionX);
        Assert.Equal(6, creature.PositionY);
    }

    [Fact]
    public void Damage_death_and_respawn_cycle()
    {
        var creature = Spawn();

        Assert.False(creature.ApplyDamage(10, Now, Slime.RespawnDelay));
        Assert.Equal(Slime.MaxHealth - 10, creature.Health);

        Assert.True(creature.ApplyDamage(999, Now, Slime.RespawnDelay));
        Assert.Equal(0, creature.Health);
        Assert.Equal(CreatureState.Dead, creature.State);
        Assert.False(creature.IsAlive);
        Assert.NotNull(creature.RespawnAt);

        creature.Respawn(Now.Add(Slime.RespawnDelay));
        Assert.True(creature.IsAlive);
        Assert.Equal(Slime.MaxHealth, creature.Health);
        Assert.Equal(6, creature.PositionX);
        Assert.Equal(CreatureState.Idle, creature.State);
    }

    [Fact]
    public void Dead_creature_cannot_be_damaged_again()
    {
        var creature = Spawn();
        creature.ApplyDamage(999, Now, Slime.RespawnDelay);

        Assert.Throws<InvalidStateException>(() => creature.ApplyDamage(1, Now, Slime.RespawnDelay));
    }

    [Fact]
    public void Step_toward_moves_within_bounds_and_speed()
    {
        var creature = Spawn();
        var map = new WorldMap(new MapId(1), 32, 32);

        var moved = creature.StepToward(new WorldPosition(new MapId(1), 20, 6), maxTiles: 2, map, Now);

        Assert.True(moved);
        Assert.Equal(8, creature.PositionX);
        Assert.Equal(CreatureState.Chase, creature.State);
    }

    [Fact]
    public void Step_toward_never_leaves_the_map()
    {
        var creature = Spawn();
        var map = new WorldMap(new MapId(1), 8, 8);

        creature.StepToward(new WorldPosition(new MapId(1), 50, 50), maxTiles: 10, map, Now);

        Assert.InRange(creature.PositionX, 0, 7);
        Assert.InRange(creature.PositionY, 0, 7);
    }

    [Fact]
    public void Attack_requires_a_target()
    {
        var creature = Spawn();

        Assert.Throws<InvalidStateException>(() => creature.BeginAttack(Now));
    }
}
