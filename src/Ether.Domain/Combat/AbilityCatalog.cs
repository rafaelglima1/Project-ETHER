using Ether.Domain.Combat;

namespace Ether.Domain.Combat;

/// <summary>
/// M5 ability catalog for the Warrior vertical slice. Content-defined abilities
/// replace this catalog when the content pipeline lands.
/// </summary>
public static class AbilityCatalog
{
    public static AbilityId BasicAttack { get; } = new("warrior.basic_attack");

    public static AbilityId PowerStrike { get; } = new("warrior.power_strike");

    private static readonly Dictionary<AbilityId, AbilityDefinition> Definitions = new()
    {
        [BasicAttack] = new AbilityDefinition(
            BasicAttack,
            "Basic Attack",
            DamageType.Physical,
            baseDamage: 4,
            powerScaling: 1.0,
            cooldown: TimeSpan.Zero,
            range: 1),
        [PowerStrike] = new AbilityDefinition(
            PowerStrike,
            "Power Strike",
            DamageType.Physical,
            baseDamage: 8,
            powerScaling: 1.5,
            cooldown: TimeSpan.FromSeconds(6),
            range: 1),
    };

    public static bool TryGet(AbilityId id, out AbilityDefinition definition) =>
        Definitions.TryGetValue(id, out definition!);

    public static IReadOnlyCollection<AbilityDefinition> All => Definitions.Values;
}
