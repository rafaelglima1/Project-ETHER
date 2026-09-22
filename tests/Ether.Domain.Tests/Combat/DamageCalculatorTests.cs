using Ether.Domain.Combat;

namespace Ether.Domain.Tests.Combat;

public sealed class DamageCalculatorTests
{
    private static readonly DamageRules Rules = DamageRules.Default;

    private static AbilityDefinition Ability(
        double baseDamage = 10,
        double scaling = 1.0,
        DamageType type = DamageType.Physical,
        int range = 1) =>
        new(new AbilityId("test.ability"), "Test", type, baseDamage, scaling, TimeSpan.Zero, range);

    private static CombatStats Stats(
        double power = 0,
        double armor = 0,
        double critChance = 0,
        double critMultiplier = 1.5,
        DamageType resistanceType = DamageType.Physical,
        double resistance = 0)
    {
        var resistances = new Dictionary<DamageType, double> { [resistanceType] = resistance };
        return new CombatStats(power, armor, critChance, critMultiplier, resistances);
    }

    [Fact]
    public void Raw_damage_is_ability_base_plus_scaled_power()
    {
        var result = DamageCalculator.Calculate(Stats(power: 6), Stats(), Ability(baseDamage: 4), criticalRoll: 1, Rules);

        // 4 + 6*1.0 = 10, no armor/resist/crit.
        Assert.Equal(10, result.RawDamage);
        Assert.Equal(10, result.Damage);
        Assert.False(result.Critical);
    }

    [Fact]
    public void Armor_applies_the_canonical_multiplier()
    {
        var result = DamageCalculator.Calculate(Stats(power: 100), Stats(armor: 100), Ability(baseDamage: 0), 1, Rules);

        // 100 damage * (100 / (100 + 100)) = 50.
        Assert.Equal(100, result.RawDamage);
        Assert.Equal(50, result.Damage);
        Assert.Equal(0.5, result.ArmorMultiplier, 3);
    }

    [Fact]
    public void Resistance_reduces_damage()
    {
        var result = DamageCalculator.Calculate(
            Stats(power: 100),
            Stats(resistance: 0.25),
            Ability(baseDamage: 0),
            1,
            Rules);

        Assert.Equal(75, result.Damage);
        Assert.Equal(0.75, result.ResistanceMultiplier, 3);
    }

    [Fact]
    public void Resistance_is_capped()
    {
        var result = DamageCalculator.Calculate(
            Stats(power: 100),
            Stats(resistance: 5),
            Ability(baseDamage: 0),
            1,
            new DamageRules(1, ResistanceCap: 0.9));

        // Cap 0.9 → 10% of 100 remains.
        Assert.Equal(10, result.Damage);
    }

    [Fact]
    public void Negative_armor_and_resistance_never_increase_damage()
    {
        var result = DamageCalculator.Calculate(
            Stats(power: 100),
            Stats(armor: -50, resistance: -1),
            Ability(baseDamage: 0),
            1,
            Rules);

        Assert.Equal(100, result.Damage);
    }

    [Fact]
    public void Critical_hit_multiplies_damage_when_roll_is_below_chance()
    {
        var critical = DamageCalculator.Calculate(
            Stats(power: 100, critChance: 0.5, critMultiplier: 2),
            Stats(),
            Ability(baseDamage: 0),
            criticalRoll: 0.1,
            Rules);

        Assert.True(critical.Critical);
        Assert.Equal(200, critical.Damage);
    }

    [Fact]
    public void Non_critical_roll_does_not_multiply()
    {
        var normal = DamageCalculator.Calculate(
            Stats(power: 100, critChance: 0.5, critMultiplier: 2),
            Stats(),
            Ability(baseDamage: 0),
            criticalRoll: 0.9,
            Rules);

        Assert.False(normal.Critical);
        Assert.Equal(100, normal.Damage);
    }

    [Fact]
    public void Minimum_damage_applies_even_against_extreme_armor()
    {
        var result = DamageCalculator.Calculate(
            Stats(power: 1),
            Stats(armor: 1_000_000),
            Ability(baseDamage: 1),
            1,
            Rules);

        Assert.Equal(1, result.Damage);
    }
}
