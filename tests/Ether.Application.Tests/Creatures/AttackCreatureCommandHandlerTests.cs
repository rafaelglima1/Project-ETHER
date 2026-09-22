using Ether.Application.Abstractions;
using Ether.Application.Combat;
using Ether.Application.Progression;
using Ether.Application.Creatures;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Creatures;

public sealed class AttackCreatureCommandHandlerTests
{
    private static readonly AbilityId BasicAttack = AbilityCatalog.BasicAttack;
    private static readonly AbilityId PowerStrike = AbilityCatalog.PowerStrike;

    private static readonly CombatOptions Options = new() { MinimumDamage = 1, ResistanceCap = 0.9, MaxAttacksPerSecond = 4 };

    private static AttackCreatureCommandHandler CreateHandler(
        InMemoryPersistence persistence,
        FakeCreatureWorld world,
        FakeCombatStatsProvider stats,
        IAbilityCooldownStore cooldowns,
        IRandomSource random,
        FakeItemInstanceRepository? items = null) =>
        new(persistence, world, persistence, stats, cooldowns, new NoopEntityLockProvider(), random,
            new KillRewardService(items ?? new FakeItemInstanceRepository(), random, TimeProvider.System),
            Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

    private static FakeCombatStatsProvider Neutral() => new();

    [Fact]
    public async Task Valid_attack_damages_the_creature()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None);

        Assert.Equal(4, result.Damage);
        Assert.Equal("creature", result.TargetType);
        Assert.Equal(creature.MaxHealth - 4, creature.Health);
    }

    [Fact]
    public async Task Unknown_ability_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, new AbilityId("warrior.nope"), creature.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.AbilityNotFound, rejection.Reason);
    }

    [Fact]
    public async Task Missing_creature_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, CreatureInstanceId.New(), CancellationToken.None));

        Assert.Equal(CombatRejectionReason.TargetNotFound, rejection.Reason);
    }

    [Fact]
    public async Task Out_of_range_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 9, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.OutOfRange, rejection.Reason);
    }

    [Fact]
    public async Task Dead_creature_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);
        creature.ApplyDamage(999, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.TargetDead, rejection.Reason);
    }

    [Fact]
    public async Task Cooldown_is_enforced_for_creature_targets()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.wolf"), 1, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        await handler.HandleAsync(account, attacker.Id, PowerStrike, creature.Id, CancellationToken.None);

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, PowerStrike, creature.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.CooldownActive, rejection.Reason);
    }

    [Fact]
    public async Task Attack_on_a_creature_from_another_account_is_forbidden()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (_, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);
        var handler = CreateHandler(persistence, world, Neutral(), new FakeCooldownStore(), new FixedRandomSource());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(AccountId.New(), attacker.Id, BasicAttack, creature.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Lethal_attack_defeats_the_creature()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);

        // High power so a single hit is lethal for the slime (30 HP).
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);
        var stats = new FakeCombatStatsProvider
        {
            Stats = new CombatStats(Power: 100, Armor: 0, CriticalChance: 0, CriticalMultiplier: 1, new Dictionary<DamageType, double>()),
        };
        var handler = CreateHandler(persistence, world, stats, new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None);

        Assert.True(result.TargetDefeated);
        Assert.Equal("Dead", result.TargetState);
        Assert.False(creature.IsAlive);
        Assert.NotNull(creature.RespawnAt);
    }

    [Fact]
    public async Task Lethal_attack_grants_experience_and_loot_once()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.slime"), 1, 0);

        var stats = new FakeCombatStatsProvider
        {
            Stats = new CombatStats(Power: 100, Armor: 0, CriticalChance: 0, CriticalMultiplier: 1, new Dictionary<DamageType, double>()),
        };
        var items = new FakeItemInstanceRepository();
        // crit roll (1.0 = no crit), loot chance 0.1 (< 0.8), quantity 0.0.
        var random = new SequencedRandomSource(1.0, 0.1, 0.0);
        var handler = CreateHandler(persistence, world, stats, new FakeCooldownStore(), random, items);

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None);

        var definition = CreatureCatalog.Get(creature.DefinitionId);
        Assert.Equal(definition.ExperienceReward, result.ExperienceGained);
        Assert.Equal(definition.ExperienceReward, attacker.Experience);
        Assert.NotNull(result.Loot);
        Assert.Single(result.Loot!);
        Assert.Equal(ItemCatalog.SlimeGel.Value, result.Loot![0].ItemDefinitionId);
        Assert.Single(items.All);
    }

    [Fact]
    public async Task Non_lethal_attack_grants_no_reward()
    {
        var persistence = new InMemoryPersistence();
        var world = new FakeCreatureWorld();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        var creature = world.Seed(new CreatureDefinitionId("creature.wolf"), 1, 0);
        var items = new FakeItemInstanceRepository();
        var handler = CreateHandler(persistence, world, new FakeCombatStatsProvider(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 }, items);

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, creature.Id, CancellationToken.None);

        Assert.False(result.TargetDefeated);
        Assert.Equal(0, result.ExperienceGained);
        Assert.Equal(0, attacker.Experience);
        Assert.Empty(items.All);
    }
}
