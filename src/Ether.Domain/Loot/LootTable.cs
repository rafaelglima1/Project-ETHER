using Ether.Domain.Common;
using Ether.Domain.Items;

namespace Ether.Domain.Loot;

/// <summary>One possible drop (Blueprint v5.0 §98).</summary>
public sealed record LootEntry(ItemDefinitionId ItemDefinitionId, double Chance, int MinQuantity, int MaxQuantity)
{
    public LootEntry Validate()
    {
        if (ItemDefinitionId.IsEmpty)
        {
            throw new DomainException("Loot entry requires an item.");
        }

        if (Chance < 0 || Chance > 1)
        {
            throw new DomainException("Loot chance must be between 0 and 1.");
        }

        if (MinQuantity < 1 || MaxQuantity < MinQuantity)
        {
            throw new DomainException("Loot quantity range is invalid.");
        }

        return this;
    }
}

/// <summary>A creature's loot table.</summary>
public sealed record LootTableDefinition(string Id, IReadOnlyList<LootEntry> Entries)
{
    public LootTableDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new DomainException("Loot table requires an id.");
        }

        foreach (var entry in Entries)
        {
            entry.Validate();
        }

        return this;
    }
}

/// <summary>Rolled loot: what a kill actually produced.</summary>
public readonly record struct LootDrop(ItemDefinitionId ItemDefinitionId, int Quantity);

/// <summary>
/// Pure loot roller. Randomness is injected so drops are deterministic in tests
/// (two draws per entry: one for the chance, one for the quantity).
/// </summary>
public static class LootRoller
{
    public static IReadOnlyList<LootDrop> Roll(LootTableDefinition table, Combat.IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(random);

        var drops = new List<LootDrop>();

        foreach (var entry in table.Entries)
        {
            if (random.NextUnit() >= entry.Chance)
            {
                continue;
            }

            var span = entry.MaxQuantity - entry.MinQuantity + 1;
            var quantity = entry.MinQuantity + (int)Math.Floor(random.NextUnit() * span);
            if (quantity > entry.MaxQuantity)
            {
                quantity = entry.MaxQuantity;
            }

            drops.Add(new LootDrop(entry.ItemDefinitionId, quantity));
        }

        return drops;
    }
}
