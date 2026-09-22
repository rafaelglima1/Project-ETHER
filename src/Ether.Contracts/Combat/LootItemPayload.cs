namespace Ether.Contracts.Combat;

/// <summary>An item that dropped for the player (server-rolled).</summary>
public sealed record LootItemPayload(
    string ItemDefinitionId,
    string Name,
    int Quantity,
    Guid ItemInstanceId);
