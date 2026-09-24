using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Configuration;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.Items;

using Microsoft.Extensions.Options;

namespace Ether.Application.Inventory;

/// <summary>
/// Authoritative inventory service: stacking, capacity, add.
/// Stacks are merged when compatible; new stacks are created on overflow.
/// Capacity counts distinct ItemInstance rows (stacks), not total quantity.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly IItemInstanceRepository _items;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InventoryOptions _options;
    private readonly TimeProvider _timeProvider;

    public InventoryService(
        IItemInstanceRepository items,
        IUnitOfWork unitOfWork,
        IOptions<InventoryOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _items = items;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ItemInstance>> AddLootAsync(
        CharacterId owner,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (quantity < 1)
        {
            throw new DomainException("Loot quantity must be positive.");
        }

        var now = _timeProvider.GetUtcNow();
        var existing = await _items.GetByOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
        var results = new List<ItemInstance>();

        // Merge into an existing compatible stack first.
        if (definition.Stackable)
        {
            var stack = existing.FirstOrDefault(item =>
                item.DefinitionId == definition.Id &&
                item.Location == ItemLocation.Inventory &&
                item.Quantity < definition.MaxStack);

            if (stack is not null)
            {
                var room = definition.MaxStack - stack.Quantity;
                var toAdd = Math.Min(room, quantity);
                stack.AddQuantity(toAdd, now);
                results.Add(stack);
                quantity -= toAdd;
            }
        }

        // Create new stacks for the remainder.
        while (quantity > 0)
        {
            var stackCount = existing.Count + results.Count(r => r.DefinitionId == definition.Id && r.Location == ItemLocation.Inventory);
            if (stackCount >= _options.MaxSlots)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new InventoryFullException(_options.MaxSlots);
            }

            var toCreate = Math.Min(quantity, definition.MaxStack);
            var instance = ItemInstance.CreateLoot(definition, toCreate, owner);
            await _items.AddAsync(instance, cancellationToken).ConfigureAwait(false);
            results.Add(instance);
            quantity -= toCreate;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    public async Task<IReadOnlyList<ItemInstance>> GetInventoryAsync(
        CharacterId characterId,
        CancellationToken cancellationToken) =>
        await _items.GetByOwnerAsync(characterId, cancellationToken).ConfigureAwait(false);
}
