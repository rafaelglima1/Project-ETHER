namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Combat</c> configuration section.
/// M5 keeps provisional combat values configurable; definitive balance belongs to
/// the balance milestone (ADR-0004).
/// </summary>
public sealed class CombatOptions
{
    public const string SectionName = "Combat";

    /// <summary>Minimum damage of any successful hit (Blueprint MinimumDamage = 1).</summary>
    public int MinimumDamage { get; set; } = 1;

    /// <summary>Upper bound applied to any single resistance (0..1).</summary>
    public double ResistanceCap { get; set; } = 0.9;

    /// <summary>Offensive power at level 1 (aggregates weapon/attribute/skill for M5).</summary>
    public double BasePower { get; set; } = 6;

    public double PowerPerLevel { get; set; } = 2;

    /// <summary>Armor at level 1.</summary>
    public double BaseArmor { get; set; } = 4;

    public double ArmorPerLevel { get; set; } = 1.5;

    public double BaseCriticalChance { get; set; } = 0.1;

    public double CriticalMultiplier { get; set; } = 1.5;

    /// <summary>Maximum attack commands per session per second.</summary>
    public int MaxAttacksPerSecond { get; set; } = 4;

    /// <summary>Max health of a newly created character at level 1.</summary>
    public int BaseMaxHealth { get; set; } = 100;

    public int MaxHealthPerLevel { get; set; }
}
