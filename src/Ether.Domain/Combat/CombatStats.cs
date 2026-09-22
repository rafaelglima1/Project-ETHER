using Ether.Domain.Combat;

namespace Ether.Domain.Combat;

/// <summary>
/// Aggregated combat statistics for one side of an attack.
/// Attribute/skill/equipment contributions are folded into <see cref="Power"/> for
/// M5; those systems will feed finer-grained values in later milestones.
/// </summary>
public readonly record struct CombatStats(
    double Power,
    double Armor,
    double CriticalChance,
    double CriticalMultiplier,
    IReadOnlyDictionary<DamageType, double> Resistances)
{
    public static CombatStats Neutral { get; } =
        new(0, 0, 0, 1, new Dictionary<DamageType, double>());

    public double ResistanceTo(DamageType damageType) =>
        Resistances.TryGetValue(damageType, out var value) ? value : 0;
}
