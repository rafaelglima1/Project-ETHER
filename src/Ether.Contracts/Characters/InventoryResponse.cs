namespace Ether.Contracts.Characters;

/// <summary>A single item in the inventory.</summary>
public sealed record InventoryItemResponse(
    string ItemDefinitionId,
    string Name,
    int Quantity,
    int MaxStack,
    bool Stackable,
    string Location);

/// <summary>Inventory snapshot for a character.</summary>
public sealed record InventoryResponse(
    Guid CharacterId,
    IReadOnlyList<InventoryItemResponse> Items);
