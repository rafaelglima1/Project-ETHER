namespace Ether.Domain.Combat;

/// <summary>
/// Canonical damage pipeline (Blueprint v5.0 §22 / v4.0 §30).
/// Each step is explicit and independently testable:
/// raw damage → armor → resistance → critical → minimum damage.
/// </summary>
public static class DamageCalculator
{
    public static DamageResult Calculate(
        CombatStats attacker,
        CombatStats defender,
        AbilityDefinition ability,
        double criticalRoll,
        DamageRules rules)
    {
        ArgumentNullException.ThrowIfNull(ability);

        // RawDamage = AbilityBase + WeaponPower + AttributeScaling + SkillScaling.
        // Power aggregates the offensive contributions for M5.
        var rawDamage = ability.BaseDamage + (attacker.Power * ability.PowerScaling);

        // ArmorMultiplier = 100 / (100 + EffectiveArmor), armor floored at 0.
        var effectiveArmor = Math.Max(0, defender.Armor);
        var armorMultiplier = 100d / (100d + effectiveArmor);

        // ResistanceMultiplier = 1 - Clamp(resistance, 0, cap).
        var resistance = Math.Clamp(defender.ResistanceTo(ability.DamageType), 0, rules.ResistanceCap);
        var resistanceMultiplier = 1d - resistance;

        var critical = criticalRoll < attacker.CriticalChance;
        var criticalMultiplier = critical ? Math.Max(1, attacker.CriticalMultiplier) : 1d;

        var scaled = rawDamage * armorMultiplier * resistanceMultiplier * criticalMultiplier;
        var damage = Math.Max(rules.MinimumDamage, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));

        return new DamageResult(
            (int)Math.Round(rawDamage, MidpointRounding.AwayFromZero),
            damage,
            critical,
            armorMultiplier,
            resistanceMultiplier);
    }
}
