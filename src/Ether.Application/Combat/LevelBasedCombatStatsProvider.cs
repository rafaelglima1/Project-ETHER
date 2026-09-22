using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Characters;
using Ether.Domain.Combat;

using Microsoft.Extensions.Options;

namespace Ether.Application.Combat;

/// <summary>
/// Provisional level-based combat stats (ADR-0004). Attributes, skills and
/// equipment will replace this source without changing the damage pipeline.
/// </summary>
public sealed class LevelBasedCombatStatsProvider : ICombatStatsProvider
{
    private readonly CombatOptions _options;
    private readonly IReadOnlyDictionary<DamageType, double> _resistances = new Dictionary<DamageType, double>();

    public LevelBasedCombatStatsProvider(IOptions<CombatOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public CombatStats For(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var level = Math.Max(Character.MinLevel, character.Level);

        return new CombatStats(
            Power: _options.BasePower + (_options.PowerPerLevel * (level - 1)),
            Armor: _options.BaseArmor + (_options.ArmorPerLevel * (level - 1)),
            CriticalChance: _options.BaseCriticalChance,
            CriticalMultiplier: _options.CriticalMultiplier,
            Resistances: _resistances);
    }
}
