using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Application.Inventory;
using Ether.Contracts.Configuration;
using Ether.Domain.Characters;
using Ether.Domain.Items;
using Ether.Infrastructure.Memory;

using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Tests.Inventory;

public sealed class InventoryServiceConcurrencyTests
{
    [Fact]
    public async Task Concurrent_direct_additions_merge_without_lost_quantity()
    {
        var owner = CharacterId.New();
        var repository = new TestItemRepository();
        var locks = new InMemoryEntityLockProvider();
        var service = CreateService(repository, locks, maxSlots: 2);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            service.AddLootAsync(owner, ItemCatalog.Get(ItemCatalog.SlimeGel), 1, CancellationToken.None)));

        var items = await repository.GetByOwnerAsync(owner, CancellationToken.None);
        var item = Assert.Single(items);
        Assert.Equal(16, item.Quantity);
        Assert.InRange(item.Quantity, 1, ItemCatalog.Get(ItemCatalog.SlimeGel).MaxStack);
    }

    [Fact]
    public async Task Nested_combat_lease_avoids_reacquiring_the_owner_lock()
    {
        var owner = CharacterId.New();
        var repository = new TestItemRepository();
        var locks = new InMemoryEntityLockProvider();
        var service = CreateService(repository, locks, maxSlots: 2);

        await using var combatLease = await locks.AcquireAsync([owner.Value, Guid.NewGuid()], CancellationToken.None);
        var added = await service.AddLootAsync(
                owner,
                ItemCatalog.Get(ItemCatalog.SlimeGel),
                1,
                CancellationToken.None,
                combatLease)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(added);
        Assert.Single(await repository.GetByOwnerAsync(owner, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_additions_cannot_race_past_inventory_capacity()
    {
        var owner = CharacterId.New();
        var repository = new TestItemRepository();
        var service = CreateService(repository, new InMemoryEntityLockProvider(), maxSlots: 1);
        var definition = new ItemDefinition(
            new ItemDefinitionId("item.test_unique"),
            "Test Unique",
            ItemCategory.Equipment,
            stackable: false,
            maxStack: 1,
            baseValue: 0);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await service.AddLootAsync(owner, definition, 1, CancellationToken.None);
                return true;
            }
            catch (InventoryFullException)
            {
                return false;
            }
        }));

        Assert.Single(attempts, succeeded => succeeded);
        var items = await repository.GetByOwnerAsync(owner, CancellationToken.None);
        Assert.Single(items);
        Assert.Equal(1, items[0].Quantity);
    }

    [Fact]
    public async Task Stack_merge_uses_one_slot_before_creating_overflow_stack()
    {
        var owner = CharacterId.New();
        var repository = new TestItemRepository();
        var locks = new InMemoryEntityLockProvider();
        var service = CreateService(repository, locks, maxSlots: 2);
        await repository.AddAsync(
            ItemInstance.CreateLoot(ItemCatalog.Get(ItemCatalog.SlimeGel), 50, owner),
            CancellationToken.None);

        await service.AddLootAsync(owner, ItemCatalog.Get(ItemCatalog.SlimeGel), 150, CancellationToken.None);

        var items = await repository.GetByOwnerAsync(owner, CancellationToken.None);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(100, item.Quantity));
    }

    [Fact]
    public async Task Capacity_failure_does_not_persist_a_partial_addition()
    {
        var owner = CharacterId.New();
        var repository = new TestItemRepository();
        var service = CreateService(repository, new InMemoryEntityLockProvider(), maxSlots: 1);

        await Assert.ThrowsAsync<InventoryFullException>(() =>
            service.AddLootAsync(owner, ItemCatalog.Get(ItemCatalog.SlimeGel), 101, CancellationToken.None));

        Assert.Empty(await repository.GetByOwnerAsync(owner, CancellationToken.None));
    }

    private static InventoryService CreateService(TestItemRepository repository, IEntityLockProvider locks, int maxSlots) =>
        new(repository, repository, locks, Options.Create(new InventoryOptions { MaxSlots = maxSlots }), TimeProvider.System);

    private sealed class TestItemRepository : IItemInstanceRepository, IUnitOfWork
    {
        private readonly List<ItemInstance> _items = [];

        public Task AddAsync(ItemInstance item, CancellationToken cancellationToken)
        {
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ItemInstance>> GetByOwnerAsync(CharacterId characterId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemInstance>>(
                _items.Where(item => item.OwnerCharacterId == characterId).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
