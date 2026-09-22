namespace Ether.Domain.Combat;

/// <summary>
/// Tunable damage rules. Kept as an explicit value object so the domain does not
/// depend on the configuration layer.
/// </summary>
public readonly record struct DamageRules(int MinimumDamage, double ResistanceCap)
{
    public static DamageRules Default { get; } = new(MinimumDamage: 1, ResistanceCap: 0.9);
}
