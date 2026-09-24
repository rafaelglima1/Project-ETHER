using Ether.Domain.Creatures;
using Ether.Domain.Loot;

namespace Ether.Application.Abstractions;

/// <summary>Read-only loot tables keyed by creature definition.</summary>
public interface ILootTableCatalog
{
    LootTableDefinition? For(CreatureDefinitionId creatureDefinitionId);

    IReadOnlyCollection<LootTableDefinition> AllLootTables { get; }
}
