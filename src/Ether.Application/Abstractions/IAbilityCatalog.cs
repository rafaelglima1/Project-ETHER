using Ether.Domain.Combat;

namespace Ether.Application.Abstractions;

/// <summary>Read-only ability definitions owned by the combat bounded context.</summary>
public interface IAbilityCatalog
{
    bool TryGet(AbilityId id, out AbilityDefinition definition);

    IReadOnlyCollection<AbilityDefinition> AllAbilities { get; }
}
