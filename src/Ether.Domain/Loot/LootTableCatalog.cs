using Ether.Domain.Creatures;
using Ether.Domain.Items;
using Ether.Domain.Loot;

namespace Ether.Domain.Loot;

/// <summary>M7 loot tables, keyed by creature definition id.</summary>
public static class LootTableCatalog
{
    private static readonly Dictionary<string, LootTableDefinition> Tables = new(StringComparer.Ordinal)
    {
        ["creature.slime"] = new LootTableDefinition(
            "loot.slime",
            new LootEntry[]
            {
                new(ItemCatalog.SlimeGel, Chance: 0.8, MinQuantity: 1, MaxQuantity: 2),
            }).Validate(),

        ["creature.wolf"] = new LootTableDefinition(
            "loot.wolf",
            new LootEntry[]
            {
                new(ItemCatalog.WolfPelt, Chance: 0.7, MinQuantity: 1, MaxQuantity: 1),
                new(ItemCatalog.HealthPotion, Chance: 0.2, MinQuantity: 1, MaxQuantity: 1),
            }).Validate(),

        ["creature.spider"] = new LootTableDefinition(
            "loot.spider",
            new LootEntry[]
            {
                new(ItemCatalog.SpiderSilk, Chance: 0.75, MinQuantity: 1, MaxQuantity: 2),
                new(ItemCatalog.CopperOre, Chance: 0.3, MinQuantity: 1, MaxQuantity: 1),
            }).Validate(),
    };

    public static LootTableDefinition? For(CreatureDefinitionId creatureDefinitionId) =>
        Tables.TryGetValue(creatureDefinitionId.Value, out var table) ? table : null;

    public static IReadOnlyCollection<LootTableDefinition> All => Tables.Values;
}
