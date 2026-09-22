namespace Ether.Domain.Items;

/// <summary>
/// M7 item catalog. Content data files replace the catalog when the content
/// pipeline lands (same convention as abilities and creatures).
/// </summary>
public static class ItemCatalog
{
    // Field order matters: ids are initialized before the definitions that use them.
    public static ItemDefinitionId SlimeGel { get; } = new("item.slime_gel");

    public static ItemDefinitionId WolfPelt { get; } = new("item.wolf_pelt");

    public static ItemDefinitionId SpiderSilk { get; } = new("item.spider_silk");

    public static ItemDefinitionId HealthPotion { get; } = new("item.health_potion");

    public static ItemDefinitionId CopperOre { get; } = new("item.copper_ore");

    private static readonly Dictionary<ItemDefinitionId, ItemDefinition> Definitions = Build();

    public static bool TryGet(ItemDefinitionId id, out ItemDefinition definition) =>
        Definitions.TryGetValue(id, out definition!);

    public static ItemDefinition Get(ItemDefinitionId id) =>
        Definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new Common.DomainException($"Item definition '{id.Value}' does not exist.");

    public static IReadOnlyCollection<ItemDefinition> All => Definitions.Values;

    private static Dictionary<ItemDefinitionId, ItemDefinition> Build()
    {
        var definitions = new[]
        {
            new ItemDefinition(SlimeGel, "Slime Gel", ItemCategory.Material, stackable: true, maxStack: 100, baseValue: 2),
            new ItemDefinition(WolfPelt, "Wolf Pelt", ItemCategory.Material, stackable: true, maxStack: 100, baseValue: 6),
            new ItemDefinition(SpiderSilk, "Spider Silk", ItemCategory.Material, stackable: true, maxStack: 100, baseValue: 5),
            new ItemDefinition(HealthPotion, "Health Potion", ItemCategory.Consumable, stackable: true, maxStack: 20, baseValue: 15),
            new ItemDefinition(CopperOre, "Copper Ore", ItemCategory.Material, stackable: true, maxStack: 100, baseValue: 3),
        };

        return definitions.ToDictionary(definition => definition.Id);
    }
}
