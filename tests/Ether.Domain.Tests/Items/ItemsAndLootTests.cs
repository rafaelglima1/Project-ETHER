using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.Items;
using Ether.Domain.Loot;
using Ether.Domain.Tests.Fakes;

namespace Ether.Domain.Tests.Items;

public sealed class ItemDefinitionTests
{
    internal static ItemDefinition SlimeGel { get; } =
        new(new ItemDefinitionId("item.slime_gel"), "Slime Gel", ItemCategory.Material, stackable: true, maxStack: 100, baseValue: 2);

    [Fact]
    public void Non_stackable_items_must_have_max_stack_one()
    {
        Assert.Throws<DomainException>(() => new ItemDefinition(
            new ItemDefinitionId("item.sword"), "Sword", ItemCategory.Equipment, stackable: false, maxStack: 5, baseValue: 10));
    }
}

public sealed class ItemInstanceTests
{
    [Fact]
    public void Loot_creates_an_inventory_instance()
    {
        var definition = ItemDefinitionTests.SlimeGel;
        var owner = CharacterId.New();

        var item = ItemInstance.CreateLoot(definition, quantity: 2, owner);

        Assert.Equal(ItemCategory.Material, definition.Category);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(owner, item.OwnerCharacterId);
        Assert.Equal(ItemLocation.Inventory, item.Location);
    }

    [Fact]
    public void Loot_quantity_is_validated()
    {
        var definition = ItemDefinitionTests.SlimeGel;

        Assert.Throws<DomainException>(() => ItemInstance.CreateLoot(definition, 0, CharacterId.New()));
        Assert.Throws<DomainException>(() => ItemInstance.CreateLoot(definition, definition.MaxStack + 1, CharacterId.New()));
    }
}

public sealed class LootRollerTests
{
    private static LootTableDefinition Table() =>
        new LootTableDefinition("loot.test", new LootEntry[]
        {
            new(ItemDefinitionTests.SlimeGel.Id, Chance: 0.8, MinQuantity: 1, MaxQuantity: 2),
        }).Validate();

    [Fact]
    public void Drop_happens_when_the_roll_is_below_the_chance()
    {
        var drops = LootRoller.Roll(Table(), new SequencedRandomSource(0.5, 0.0));

        var drop = Assert.Single(drops);
        Assert.Equal(ItemDefinitionTests.SlimeGel.Id, drop.ItemDefinitionId);
        Assert.Equal(1, drop.Quantity);
    }

    [Fact]
    public void No_drop_when_the_roll_is_above_the_chance()
    {
        var drops = LootRoller.Roll(Table(), new SequencedRandomSource(0.9, 0.0));

        Assert.Empty(drops);
    }

    [Fact]
    public void Quantity_stays_within_the_configured_range()
    {
        var drops = LootRoller.Roll(Table(), new SequencedRandomSource(0.1, 0.999));

        var drop = Assert.Single(drops);
        Assert.InRange(drop.Quantity, 1, 2);
    }

    [Fact]
    public void Zero_chance_never_drops_and_full_chance_always_drops()
    {
        var never = new LootTableDefinition("t", new LootEntry[] { new(ItemDefinitionTests.SlimeGel.Id, 0, 1, 1) }).Validate();
        var always = new LootTableDefinition("t", new LootEntry[] { new(ItemDefinitionTests.SlimeGel.Id, 1, 1, 1) }).Validate();

        Assert.Empty(LootRoller.Roll(never, new SequencedRandomSource(0.0, 0.0)));
        Assert.Single(LootRoller.Roll(always, new SequencedRandomSource(0.99, 0.0)));
    }
}
