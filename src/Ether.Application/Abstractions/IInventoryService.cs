using Ether.Domain.Characters;
using Ether.Domain.Items;

namespace Ether.Application.Abstractions;

/// <summary>
/// Authoritative inventory operations: stacking, capacity, add/remove.
/// PostgreSQL remains the source of truth.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Adds items to the owner's inventory, merging into compatible stacks when
    /// possible and creating new stacks when needed. Throws
    /// <see cref="Exceptions.InventoryFullException"/> when capacity is exhausted.
    /// </summary>
    Task<IReadOnlyList<ItemInstance>> AddLootAsync(
        CharacterId owner,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken);

    /// <summary>Returns all inventory items for a character.</summary>
    Task<IReadOnlyList<ItemInstance>> GetInventoryAsync(
        CharacterId characterId,
        CancellationToken cancellationToken);
}
