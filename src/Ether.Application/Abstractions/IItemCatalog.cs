using Ether.Domain.Items;

namespace Ether.Application.Abstractions;

/// <summary>Read-only item definitions owned by the inventory bounded context.</summary>
public interface IItemCatalog
{
    ItemDefinition Get(ItemDefinitionId id);

    IReadOnlyCollection<ItemDefinition> AllItems { get; }
}
