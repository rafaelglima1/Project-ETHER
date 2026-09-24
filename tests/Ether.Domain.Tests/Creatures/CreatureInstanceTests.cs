using Ether.Domain.Common;
using Ether.Domain.Creatures;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Creatures;

public sealed class CreatureDefinitionTests
{
    [Fact]
    public void Basic_attack_reuses_the_ability_contract()
    {
        var definition = CreateCreatureDefinition();
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


    internal static CreatureDefinition CreateCreatureDefinition(
        string id = "creature.test",
        string name = "Test Creature",
        int maxHealth = 45,
        int attackRange = 1,
        TimeSpan? attackCooldown = null,
        long experienceReward = 12) =>
        new(
            new CreatureDefinitionId(id), name, level: 1, maxHealth, attackPower: 5, armor: 1, moveSpeed: 1,
            aggroRange: 6, attackRange, attackCooldown ?? TimeSpan.FromSeconds(2), leashRange: 10,
            respawnDelay: TimeSpan.FromSeconds(30), new Dictionary<Ether.Domain.Combat.DamageType, double>(), experienceReward);
}

public sealed class CreatureInstanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly CreatureDefinition Slime = CreatureDefinitionTests.CreateCreatureDefinition("creature.slime", "Slime", maxHealth: 30);

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
