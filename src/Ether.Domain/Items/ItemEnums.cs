namespace Ether.Domain.Items;

/// <summary>Item category (Blueprint v3.0 §165).</summary>
public enum ItemCategory
{
    Misc = 0,
    Consumable = 1,
    Material = 2,
    Equipment = 3,
    Quest = 4,
}

/// <summary>Item rarity (Blueprint v5.0 §166).</summary>
public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

/// <summary>Where an item instance logically lives (Blueprint v5.0 §26).</summary>
public enum ItemLocation
{
    Inventory = 0,
    Equipment = 1,
    World = 2,
    MarketEscrow = 3,
    TradeEscrow = 4,
    Crafting = 5,
    Destroyed = 6,
}
