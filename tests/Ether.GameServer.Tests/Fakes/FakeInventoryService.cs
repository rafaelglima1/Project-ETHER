using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Items;

namespace Ether.GameServer.Tests.Fakes;

/// <summary>In-memory inventory service for GameServer tests.</summary>
internal sealed class FakeInventoryService : IInventoryService
{
    private readonly List<ItemInstance> _items = [];

    public IReadOnlyList<ItemInstance> All => _items;

    public Task<IReadOnlyList<ItemInstance>> AddLootAsync(
        CharacterId owner,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken,
        IEntityLockLease? heldLocks = null)
    {
        var instance = ItemInstance.CreateLoot(definition, quantity, owner);
        _items.Add(instance);
        return Task.FromResult<IReadOnlyList<ItemInstance>>([instance]);
    }

    public Task<IReadOnlyList<ItemInstance>> GetInventoryAsync(
        CharacterId characterId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemInstance>>(
            _items.Where(item => item.OwnerCharacterId == characterId).ToList());
}
