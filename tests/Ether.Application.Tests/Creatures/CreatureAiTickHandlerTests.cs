using Ether.Application.Abstractions;
using Ether.Application.Creatures;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Configuration;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Creatures;

public sealed class CreatureSpawnServiceTests
{
    [Fact]
    public void Spawns_the_catalog_creatures_for_a_map()
    {
        var world = new FakeCreatureWorld();
        var service = new CreatureSpawnService(world);

        service.EnsureSpawned(new MapId(1));

        var expected = CreatureSpawnCatalog.ForMap(new MapId(1)).Count;
        Assert.Equal(expected, world.GetByMap(new MapId(1)).Count);
    }

    [Fact]
    public void Spawning_twice_does_not_duplicate()
    {
        var world = new FakeCreatureWorld();
        var service = new CreatureSpawnService(world);

        service.EnsureSpawned(new MapId(1));
        var first = world.GetByMap(new MapId(1)).Count;
        service.EnsureSpawned(new MapId(1));

        Assert.Equal(first, world.GetByMap(new MapId(1)).Count);
    }

    [Fact]
    public void Unknown_map_spawns_nothing()
    {
        var world = new FakeCreatureWorld();
        var service = new CreatureSpawnService(world);

        service.EnsureSpawned(new MapId(99));

        Assert.Empty(world.GetByMap(new MapId(99)));
    }
}

public sealed class CreatureAiTickHandlerTests
{
    private static readonly CombatOptions Options = new() { MinimumDamage = 1, ResistanceCap = 0.9 };

    private static CreatureAiTickHandler CreateHandler(
        InMemoryPersistence persistence,
        FakeCreatureWorld world,
        FakeCombatStatsProvider stats,
        IAbilityCooldownStore cooldowns,
        IRandomSource random,
        TimeProvider? clock = null) =>
        new(
            world,
            new CreatureSpawnService(world),
            persistence,
            persistence,
            stats,
            cooldowns,
            new NoopEntityLockProvider(),
            random,
            new FakeWorldMapProvider(),
            Microsoft.Extensions.Options.Options.Create(Options),
            clock ?? TimeProvider.System);

    [Fact]
    public async Task Spawns_creatures_on_the_first_tick()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource());

        await handler.HandleAsync(new MapId(1), CancellationToken.None);

        Assert.NotEmpty(world.GetByMap(new MapId(1)));
    }

    [Fact]
    public async Task Creature_adjacent_to_a_player_attacks_it()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        // Player away from catalog spawn points so only the seeded slime can attack.
        var (_, player) = await persistence.SeedInWorldCharacterAsync("Hero", 2, 2);
        var slime = world.Seed(new CreatureDefinitionId("creature.slime"), 3, 2);

        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });
        var result = await handler.HandleAsync(new MapId(1), CancellationToken.None);

        Assert.Single(result.Attacks);
        var attack = result.Attacks[0];
        Assert.Equal(slime.Id.Value, attack.CreatureId);
        Assert.Equal(player.Id.Value, attack.TargetCharacterId);
        Assert.True(attack.Damage >= 1);
        Assert.Equal(player.MaxHealth - attack.Damage, player.Health);
    }

    [Fact]
    public async Task Distant_creature_moves_toward_the_player()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        await persistence.SeedInWorldCharacterAsync("Hero", 10, 6);
        var slime = world.Seed(new CreatureDefinitionId("creature.slime"), 6, 6);

        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource());
        var result = await handler.HandleAsync(new MapId(1), CancellationToken.None);

        Assert.Contains(result.Moves, move => move.CreatureId == slime.Id.Value);
        Assert.True(slime.PositionX > 6);
    }

    [Fact]
    public async Task Creature_attack_respects_its_cooldown()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        await persistence.SeedInWorldCharacterAsync("Hero", 2, 2);
        world.Seed(new CreatureDefinitionId("creature.slime"), 3, 2);

        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        var first = await handler.HandleAsync(new MapId(1), CancellationToken.None);
        var second = await handler.HandleAsync(new MapId(1), CancellationToken.None);

        Assert.Single(first.Attacks);
        Assert.Empty(second.Attacks);
    }

    [Fact]
    public async Task Dead_creature_respawns_when_due()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        await persistence.SeedInWorldCharacterAsync("Hero", 20, 20);
        var slime = world.Seed(new CreatureDefinitionId("creature.slime"), 6, 6);

        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var definition = CreatureCatalog.Get(slime.DefinitionId);
        slime.ApplyDamage(999, clock.GetUtcNow(), TimeSpan.FromSeconds(30));

        // Advance beyond the respawn delay.
        clock.Advance(TimeSpan.FromSeconds(31));

        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource(), clock);
        var result = await handler.HandleAsync(new MapId(1), CancellationToken.None);

        Assert.True(slime.IsAlive);
        Assert.Equal(definition.MaxHealth, slime.Health);
        Assert.Contains(result.Moves, move => move.CreatureId == slime.Id.Value);
    }
}
