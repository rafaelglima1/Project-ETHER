using Ether.Application.Abstractions;
using Ether.Application.Combat;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Combat;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Combat;

public sealed class AttackCommandHandlerTests
{
    private static readonly AbilityId BasicAttack = new("warrior.basic_attack");
    private static readonly AbilityId PowerStrike = new("warrior.power_strike");

    private static readonly CombatOptions Options = new()
    {
        MinimumDamage = 1,
        ResistanceCap = 0.9,
        MaxAttacksPerSecond = 4,
    };

    private static AttackCommandHandler CreateHandler(
        InMemoryPersistence persistence,
        FakeCombatStatsProvider stats,
        IAbilityCooldownStore cooldowns,
        IRandomSource random) =>
        new(persistence, persistence, stats, TestGameContentCatalog.Instance, cooldowns, new NoopEntityLockProvider(), random,
            Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

    private static FakeCombatStatsProvider NeutralStats() => new();

    [Fact]
    public async Task Valid_basic_attack_applies_server_damage()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None);

        Assert.Equal(4, result.Damage);
        Assert.False(result.Critical);
        Assert.Equal(96, target.Health);
        Assert.Equal("Combat", target.State.ToString());
        Assert.Equal(CharacterState.Combat, attacker.State);
    }

    [Fact]
    public async Task Unknown_ability_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, new AbilityId("warrior.nope"), target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.AbilityNotFound, rejection.Reason);
    }

    [Fact]
    public async Task Self_target_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, attacker.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.SelfTarget, rejection.Reason);
    }

    [Fact]
    public async Task Missing_target_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, CharacterId.New(), CancellationToken.None));

        Assert.Equal(CombatRejectionReason.TargetNotFound, rejection.Reason);
    }

    [Fact]
    public async Task Out_of_range_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 8, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.OutOfRange, rejection.Reason);
    }

    [Fact]
    public async Task Dead_target_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0, maxHealth: 4);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        await handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None);
        Assert.False(target.IsAlive);

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.TargetDead, rejection.Reason);
    }

    [Fact]
    public async Task Dead_attacker_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0, maxHealth: 5);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        attacker.ApplyDamage(5, DateTimeOffset.UtcNow);

        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.AttackerDead, rejection.Reason);
    }

    [Fact]
    public async Task Attacker_not_in_world_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        attacker.LeaveWorld(DateTimeOffset.UtcNow);

        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.InvalidState, rejection.Reason);
    }

    [Fact]
    public async Task Ability_cooldown_is_enforced_server_side()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        await handler.HandleAsync(account, attacker.Id, PowerStrike, target.Id, CancellationToken.None);

        var rejection = await Assert.ThrowsAsync<CombatRejectedException>(() =>
            handler.HandleAsync(account, attacker.Id, PowerStrike, target.Id, CancellationToken.None));

        Assert.Equal(CombatRejectionReason.CooldownActive, rejection.Reason);
    }

    [Fact]
    public async Task Attack_on_another_accounts_character_is_forbidden()
    {
        var persistence = new InMemoryPersistence();
        var (_, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(AccountId.New(), attacker.Id, BasicAttack, target.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Lethal_damage_defeats_the_target()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0, maxHealth: 3);
        var handler = CreateHandler(persistence, NeutralStats(), new FakeCooldownStore(), new FixedRandomSource { Value = 1 });

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None);

        Assert.True(result.TargetDefeated);
        Assert.Equal(0, result.TargetHealth);
        Assert.Equal("Dead", result.TargetState);
        Assert.False(target.IsAlive);
    }

    [Fact]
    public async Task Critical_is_deterministic_through_the_random_source()
    {
        var persistence = new InMemoryPersistence();
        var (account, attacker) = await persistence.SeedInWorldCharacterAsync("Attacker", 0, 0);
        var (_, target) = await persistence.SeedInWorldCharacterAsync("Target", 1, 0);

        var stats = new FakeCombatStatsProvider
        {
            Stats = new CombatStats(0, 0, CriticalChance: 1.0, CriticalMultiplier: 2.0, new Dictionary<DamageType, double>()),
        };

        var handler = CreateHandler(persistence, stats, new FakeCooldownStore(), new FixedRandomSource { Value = 0 });

        var result = await handler.HandleAsync(account, attacker.Id, BasicAttack, target.Id, CancellationToken.None);

        Assert.True(result.Critical);
        Assert.Equal(8, result.Damage);
    }
}
