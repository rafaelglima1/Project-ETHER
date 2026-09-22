using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.Items;
using Ether.Domain.Loot;
using Ether.Domain.Tests.Fakes;

namespace Ether.Domain.Tests.Items;

public sealed class ItemCatalogTests
{
    [Fact]
    public void Catalog_contains_the_m7_materials()
    {
        Assert.True(ItemCatalog.TryGet(ItemCatalog.SlimeGel, out var gel));
        Assert.Equal("Slime Gel", gel.Name);
        Assert.Equal(ItemCategory.Material, gel.Category);
    }

    [Fact]
    public void Unknown_item_is_rejected()
    {
        Assert.Throws<DomainException>(() => ItemCatalog.Get(new ItemDefinitionId("item.nope")));
    }

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
        var definition = ItemCatalog.Get(ItemCatalog.SlimeGel);
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
        var definition = ItemCatalog.Get(ItemCatalog.SlimeGel);

        Assert.Throws<DomainException>(() => ItemInstance.CreateLoot(definition, 0, CharacterId.New()));
        Assert.Throws<DomainException>(() => ItemInstance.CreateLoot(definition, definition.MaxStack + 1, CharacterId.New()));
    }
}

public sealed class LootRollerTests
{
    private static LootTableDefinition Table() =>
        new LootTableDefinition("loot.test", new LootEntry[]
        {
            new(ItemCatalog.SlimeGel, Chance: 0.8, MinQuantity: 1, MaxQuantity: 2),
        }).Validate();

    [Fact]
    public void Drop_happens_when_the_roll_is_below_the_chance()
    {
        var drops = LootRoller.Roll(Table(), new SequencedRandomSource(0.5, 0.0));

        var drop = Assert.Single(drops);
        Assert.Equal(ItemCatalog.SlimeGel, drop.ItemDefinitionId);
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
        var never = new LootTableDefinition("t", new LootEntry[] { new(ItemCatalog.SlimeGel, 0, 1, 1) }).Validate();
        var always = new LootTableDefinition("t", new LootEntry[] { new(ItemCatalog.SlimeGel, 1, 1, 1) }).Validate();

        Assert.Empty(LootRoller.Roll(never, new SequencedRandomSource(0.0, 0.0)));
        Assert.Single(LootRoller.Roll(always, new SequencedRandomSource(0.99, 0.0)));
    }
}
