using Ether.Application.Abstractions;
using Ether.Contracts.Characters;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Application.Inventory;

/// <summary>Use case: query a character's inventory.</summary>
public sealed class GetInventoryHandler
{
    private readonly IInventoryService _inventory;
    private readonly IItemCatalog _items;

    public GetInventoryHandler(IInventoryService inventory, IItemCatalog items)
    {
        _inventory = inventory;
        _items = items;
    }

    public async Task<InventoryResponse> HandleAsync(
        AccountId authenticatedAccountId,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        var items = await _inventory.GetInventoryAsync(characterId, cancellationToken).ConfigureAwait(false);

        return new InventoryResponse(
            characterId.Value,
            items.Select(item =>
            {
                var definition = _items.Get(item.DefinitionId);
                return new InventoryItemResponse(
                    item.DefinitionId.Value,
                    definition.Name,
                    item.Quantity,
                    definition.MaxStack,
                    definition.Stackable,
                    item.Location.ToString());
            }).ToList());
    }
}
