using Ether.Domain.Common;

namespace Ether.Domain.Combat;

/// <summary>
/// Data-driven ability definition (Blueprint v5.0 §12).
/// M5 uses a domain-side catalog; the content pipeline replaces this later.
/// </summary>
public sealed record AbilityDefinition
{
    public AbilityDefinition(
        AbilityId id,
        string name,
        DamageType damageType,
        double baseDamage,
        double powerScaling,
        TimeSpan cooldown,
        int range)
    {
        if (id.IsEmpty)
        {
            throw new DomainException("Ability requires an id.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Ability requires a name.");
        }

        if (baseDamage < 0)
        {
            throw new DomainException("Ability base damage must not be negative.");
        }

        if (powerScaling < 0)
        {
            throw new DomainException("Ability power scaling must not be negative.");
        }

        if (cooldown < TimeSpan.Zero)
        {
            throw new DomainException("Ability cooldown must not be negative.");
        }

        if (range < 1)
        {
            throw new DomainException("Ability range must be at least 1 tile.");
        }

        Id = id;
        Name = name;
        DamageType = damageType;
        BaseDamage = baseDamage;
        PowerScaling = powerScaling;
        Cooldown = cooldown;
        Range = range;
    }

    public AbilityId Id { get; }

    public string Name { get; }

    public DamageType DamageType { get; }

    /// <summary>Flat contribution (Blueprint <c>AbilityBase</c>).</summary>
    public double BaseDamage { get; }

    /// <summary>Multiplier applied to the attacker's offensive power.</summary>
    public double PowerScaling { get; }

    public TimeSpan Cooldown { get; }

    /// <summary>Maximum Chebyshev distance in tiles (matches the world convention).</summary>
    public int Range { get; }
}
