using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Loot;
using Ether.Domain.Progression;

namespace Ether.Application.Progression;

/// <summary>
/// Grants the rewards of a creature kill: experience from the canonical curve and
/// loot rolled server-side. Called exactly once, on the death transition, so a
/// reward cannot be duplicated by retries. The caller persists atomically with the
/// kill (single unit of work).
/// </summary>
public sealed class KillRewardService
{
    private readonly IInventoryService _inventory;
    private readonly ILootTableCatalog _lootTables;
    private readonly IItemCatalog _items;
    private readonly IRandomSource _random;
    private readonly TimeProvider _timeProvider;

    public KillRewardService(
        IInventoryService inventory,
        ILootTableCatalog lootTables,
        IItemCatalog items,
        IRandomSource random,
        TimeProvider timeProvider)
    {
        _inventory = inventory;
        _lootTables = lootTables;
        _items = items;
        _random = random;
        _timeProvider = timeProvider;
    }

    public async Task<KillRewardResult> GrantAsync(
        Character killer,
        CreatureDefinition definition,
        CancellationToken cancellationToken,
        IEntityLockLease? heldLocks = null)
    {
        ArgumentNullException.ThrowIfNull(killer);
        ArgumentNullException.ThrowIfNull(definition);

        var levelsGained = killer.GrantExperience(definition.ExperienceReward, _timeProvider.GetUtcNow());

        var dropped = new List<Domain.Items.ItemInstance>();
        var table = _lootTables.For(definition.Id);

        if (table is not null)
        {
            foreach (var drop in LootRoller.Roll(table, _random))
            {
                var itemDefinition = _items.Get(drop.ItemDefinitionId);
                var instances = await _inventory.AddLootAsync(killer.Id, itemDefinition, drop.Quantity, cancellationToken, heldLocks)
                    .ConfigureAwait(false);
                dropped.AddRange(instances);
            }
        }

        var remaining = ExperienceCurve.ExperienceToAdvanceFrom(killer.Level) - killer.Experience;

        return new KillRewardResult(
            definition.ExperienceReward,
            levelsGained,
            killer.Level,
            killer.Experience,
            remaining > 0 ? remaining : 0,
            dropped);
    }
}
