using Ether.Domain.Characters;
using Ether.Domain.Common;

namespace Ether.Domain.Items;

/// <summary>
/// A concrete owned item (Blueprint v5.0 §35). Items created by loot live in the
/// owner's inventory; each instance has exactly one location.
/// </summary>
public sealed class ItemInstance
{
    private ItemInstance()
    {
        // Required by the persistence layer.
    }

    private ItemInstance(
        ItemInstanceId id,
        ItemDefinitionId definitionId,
        int quantity,
        CharacterId ownerCharacterId,
        ItemLocation location)
    {
        Id = id;
        DefinitionId = definitionId;
        Quantity = quantity;
        OwnerCharacterId = ownerCharacterId;
        Location = location;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public ItemInstanceId Id { get; private set; }

    public ItemDefinitionId DefinitionId { get; private set; }

    public int Quantity { get; private set; }

    public CharacterId OwnerCharacterId { get; private set; }

    public ItemLocation Location { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a looted item in a character's inventory.</summary>
    public static ItemInstance CreateLoot(
        ItemDefinition definition,
        int quantity,
        CharacterId ownerCharacterId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (quantity < 1)
        {
            throw new DomainException("Loot quantity must be at least 1.");
        }

        if (quantity > definition.MaxStack)
        {
            throw new DomainException($"Loot quantity exceeds the item's max stack ({definition.MaxStack}).");
        }

        if (ownerCharacterId.IsEmpty)
        {
            throw new DomainException("Loot must have an owner.");
        }

        return new ItemInstance(
            ItemInstanceId.New(),
            definition.Id,
            quantity,
            ownerCharacterId,
            ItemLocation.Inventory);
    }

    /// <summary>Adds quantity to an existing stack (caller must ensure it does not exceed MaxStack).</summary>
    public void AddQuantity(int amount, DateTimeOffset nowUtc)
    {
        if (amount < 1)
        {
            throw new DomainException("Quantity must be positive.");
        }

        Quantity += amount;
        UpdatedAt = nowUtc;
    }

    /// <summary>Removes quantity from this stack (floors at 0).</summary>
    public bool RemoveQuantity(int amount, DateTimeOffset nowUtc)
    {
        if (amount < 1)
        {
            throw new DomainException("Quantity must be positive.");
        }

        Quantity = Math.Max(0, Quantity - amount);
        UpdatedAt = nowUtc;
        return Quantity == 0;
    }
}
