using Ether.Application.Progression;
using Ether.Application.Tests.Fakes;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;

namespace Ether.Application.Tests.Progression;

public sealed class KillRewardServiceTests
{
    private static readonly CreatureDefinitionId SlimeId = new("creature.slime");

    private static Character Killer() =>
        Character.Create(
            Ether.Domain.Accounts.AccountId.New(),
            "Killer",
            CharacterClass.Warrior,
            new Ether.Domain.World.WorldPosition(new Ether.Domain.World.MapId(1), 0, 0),
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Grants_creature_experience()
    {
        var items = new FakeInventoryService();
        var service = new KillRewardService(items, new SequencedRandomSource(0.0, 0.0), TimeProvider.System);
        var definition = CreatureCatalog.Get(SlimeId);
        var killer = Killer();

        var reward = await service.GrantAsync(killer, definition, CancellationToken.None);

        Assert.Equal(definition.ExperienceReward, reward.ExperienceGained);
        Assert.Equal(definition.ExperienceReward, killer.Experience);
        Assert.Equal(1, killer.Level);
    }

    [Fact]
    public async Task Rolls_loot_into_the_killers_inventory()
    {
        var items = new FakeInventoryService();
        // chance 0.1 (< 0.8) then quantity 0.0 → 1 slime gel.
        var service = new KillRewardService(items, new SequencedRandomSource(0.1, 0.0), TimeProvider.System);
        var definition = CreatureCatalog.Get(SlimeId);
        var killer = Killer();

        var reward = await service.GrantAsync(killer, definition, CancellationToken.None);

        var drop = Assert.Single(reward.Items);
        Assert.Equal(ItemCatalog.SlimeGel, drop.DefinitionId);
        Assert.Equal(killer.Id, drop.OwnerCharacterId);
        Assert.Equal(ItemLocation.Inventory, drop.Location);
        Assert.Single(items.All);
    }

    [Fact]
    public async Task No_loot_when_the_roll_misses()
    {
        var items = new FakeInventoryService();
        var service = new KillRewardService(items, new SequencedRandomSource(0.99), TimeProvider.System);

        var reward = await service.GrantAsync(Killer(), CreatureCatalog.Get(SlimeId), CancellationToken.None);

        Assert.Empty(reward.Items);
        Assert.Empty(items.All);
    }

    [Fact]
    public async Task Level_up_is_reported()
    {
        var items = new FakeInventoryService();
        var service = new KillRewardService(items, new SequencedRandomSource(0.99), TimeProvider.System);
        var killer = Killer();

        // Give almost enough XP for level 2 first.
        var required = Ether.Domain.Progression.ExperienceCurve.ExperienceToAdvanceFrom(1);
        killer.GrantExperience(required - 1, DateTimeOffset.UtcNow);

        var reward = await service.GrantAsync(killer, CreatureCatalog.Get(SlimeId), CancellationToken.None);

        Assert.Equal(1, reward.LevelsGained);
        Assert.Equal(2, reward.Level);
    }

    [Fact]
    public async Task Creature_without_a_loot_table_drops_nothing()
    {
        var items = new FakeInventoryService();
        var service = new KillRewardService(items, new SequencedRandomSource(0.0, 0.0), TimeProvider.System);

        var definition = new CreatureDefinition(
            new CreatureDefinitionId("creature.test"),
            "Test",
            level: 1,
            maxHealth: 10,
            attackPower: 1,
            armor: 0,
            moveSpeed: 1,
            aggroRange: 3,
            attackRange: 1,
            attackCooldown: TimeSpan.FromSeconds(1),
            leashRange: 5,
            respawnDelay: TimeSpan.FromSeconds(5),
            resistances: new Dictionary<DamageType, double>(),
            experienceReward: 0);

        var reward = await service.GrantAsync(Killer(), definition, CancellationToken.None);

        Assert.Empty(reward.Items);
    }
}
