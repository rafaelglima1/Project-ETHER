using Ether.Domain.Common;

namespace Ether.Domain.Items;

/// <summary>Data-driven item definition (Blueprint v5.0 §34).</summary>
public sealed record ItemDefinition
{
    public ItemDefinition(
        ItemDefinitionId id,
        string name,
        ItemCategory category,
        bool stackable,
        int maxStack,
        long baseValue,
        ItemRarity rarity = ItemRarity.Common)
    {
        if (id.IsEmpty)
        {
            throw new DomainException("Item definition requires an id.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Item definition requires a name.");
        }

        if (maxStack < 1)
        {
            throw new DomainException("Item max stack must be at least 1.");
        }

        if (!stackable && maxStack != 1)
        {
            throw new DomainException("Non-stackable items must have a max stack of 1.");
        }

        if (baseValue < 0)
        {
            throw new DomainException("Item base value must not be negative.");
        }

        Id = id;
        Name = name;
        Category = category;
        Stackable = stackable;
        MaxStack = maxStack;
        BaseValue = baseValue;
        Rarity = rarity;
    }

    public ItemDefinitionId Id { get; }

    public string Name { get; }

    public ItemCategory Category { get; }

    public bool Stackable { get; }

    public int MaxStack { get; }

    public long BaseValue { get; }

    public ItemRarity Rarity { get; }
}
