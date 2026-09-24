using Ether.Application.Abstractions;
using Ether.Contracts.Characters;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Items;

namespace Ether.Application.Inventory;

/// <summary>Use case: query a character's inventory.</summary>
public sealed class GetInventoryHandler
{
    private readonly IInventoryService _inventory;

    public GetInventoryHandler(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    public async Task<InventoryResponse> HandleAsync(
        AccountId authenticatedAccountId,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        var items = await _inventory.GetInventoryAsync(characterId, cancellationToken).ConfigureAwait(false);

        return new InventoryResponse(
            characterId.Value,
            items.Select(item => new InventoryItemResponse(
                item.DefinitionId.Value,
                ItemCatalog.Get(item.DefinitionId).Name,
                item.Quantity,
                ItemCatalog.Get(item.DefinitionId).MaxStack,
                ItemCatalog.Get(item.DefinitionId).Stackable,
                item.Location.ToString())).ToList());
    }
}
