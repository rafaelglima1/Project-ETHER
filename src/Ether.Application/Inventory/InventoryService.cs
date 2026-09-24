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
    private readonly IEntityLockProvider _locks;
    private readonly InventoryOptions _options;
    private readonly TimeProvider _timeProvider;

    public InventoryService(
        IItemInstanceRepository items,
        IUnitOfWork unitOfWork,
        IEntityLockProvider locks,
        IOptions<InventoryOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _items = items;
        _unitOfWork = unitOfWork;
        _locks = locks;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ItemInstance>> AddLootAsync(
        CharacterId owner,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken,
        IEntityLockLease? heldLocks = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (quantity < 1)
        {
            throw new DomainException("Loot quantity must be positive.");
        }

        if (heldLocks?.Covers(owner.Value) != true)
        {
            await using var ownerLock = await _locks
                .AcquireAsync([owner.Value], cancellationToken)
                .ConfigureAwait(false);

            return await AddLootUnderLockAsync(owner, definition, quantity, cancellationToken).ConfigureAwait(false);
        }

        return await AddLootUnderLockAsync(owner, definition, quantity, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ItemInstance>> AddLootUnderLockAsync(
        CharacterId owner,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var existing = await _items.GetByOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
        var inventoryStacks = existing.Where(item => item.Location == ItemLocation.Inventory).ToList();
        var results = new List<ItemInstance>();
        var mergeCapacity = definition.Stackable
            ? inventoryStacks
                .Where(item => item.DefinitionId == definition.Id)
                .Sum(item => (long)Math.Max(0, definition.MaxStack - item.Quantity))
            : 0;
        var remainingAfterMerge = Math.Max(0L, quantity - mergeCapacity);
        var newStackCount = remainingAfterMerge / definition.MaxStack +
                            (remainingAfterMerge % definition.MaxStack == 0 ? 0 : 1);

        if (inventoryStacks.Count + newStackCount > _options.MaxSlots)
        {
            throw new InventoryFullException(_options.MaxSlots);
        }

        // Merge into compatible stacks first; any remaining quantity occupies new slots.
        if (definition.Stackable)
        {
            foreach (var stack in inventoryStacks.Where(item => item.DefinitionId == definition.Id && item.Quantity < definition.MaxStack))
            {
                if (quantity == 0)
                {
                    break;
                }

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
