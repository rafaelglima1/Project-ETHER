using Ether.Domain.Creatures;

namespace Ether.Application.Abstractions;

/// <summary>Read-only creature definitions owned by the creature bounded context.</summary>
public interface ICreatureCatalog
{
    CreatureDefinition Get(CreatureDefinitionId id);

    IReadOnlyCollection<CreatureDefinition> AllCreatures { get; }
}
