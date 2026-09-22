using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;
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
    private readonly IItemInstanceRepository _items;
    private readonly IRandomSource _random;
    private readonly TimeProvider _timeProvider;

    public KillRewardService(IItemInstanceRepository items, IRandomSource random, TimeProvider timeProvider)
    {
        _items = items;
        _random = random;
        _timeProvider = timeProvider;
    }

    public async Task<KillRewardResult> GrantAsync(
        Character killer,
        CreatureDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(killer);
        ArgumentNullException.ThrowIfNull(definition);

        var levelsGained = killer.GrantExperience(definition.ExperienceReward, _timeProvider.GetUtcNow());

        var dropped = new List<ItemInstance>();
        var table = LootTableCatalog.For(definition.Id);

        if (table is not null)
        {
            foreach (var drop in LootRoller.Roll(table, _random))
            {
                var itemDefinition = ItemCatalog.Get(drop.ItemDefinitionId);
                var instance = ItemInstance.CreateLoot(itemDefinition, drop.Quantity, killer.Id);
                await _items.AddAsync(instance, cancellationToken).ConfigureAwait(false);
                dropped.Add(instance);
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
