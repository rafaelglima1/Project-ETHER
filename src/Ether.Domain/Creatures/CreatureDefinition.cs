using Ether.Domain.Combat;
using Ether.Domain.Common;

namespace Ether.Domain.Creatures;

/// <summary>
/// Data-driven creature definition (Blueprint v5.0 §18). The AI profile is embedded
/// for M6 (aggro/attack range, cooldown, movement, leash, respawn).
/// </summary>
public sealed record CreatureDefinition
{
    public CreatureDefinition(
        CreatureDefinitionId id,
        string name,
        int level,
        int maxHealth,
        double attackPower,
        double armor,
        double moveSpeed,
        int aggroRange,
        int attackRange,
        TimeSpan attackCooldown,
        int leashRange,
        TimeSpan respawnDelay,
        IReadOnlyDictionary<DamageType, double> resistances,
        long experienceReward = 0)
    {
        if (id.IsEmpty)
        {
            throw new DomainException("Creature definition requires an id.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Creature definition requires a name.");
        }

        if (level < 1)
        {
            throw new DomainException("Creature level must be at least 1.");
        }

        if (maxHealth < 1)
        {
            throw new DomainException("Creature max health must be positive.");
        }

        if (attackPower < 0 || armor < 0)
        {
            throw new DomainException("Creature power and armor must not be negative.");
        }

        if (moveSpeed < 0)
        {
            throw new DomainException("Creature move speed must not be negative.");
        }

        if (aggroRange < 1 || attackRange < 1 || leashRange < aggroRange)
        {
            throw new DomainException("Creature ranges are invalid (leash must cover aggro).");
        }

        if (attackCooldown < TimeSpan.Zero || respawnDelay < TimeSpan.Zero)
        {
            throw new DomainException("Creature timers must not be negative.");
        }

        if (experienceReward < 0)
        {
            throw new DomainException("Creature experience reward must not be negative.");
        }

        Id = id;
        Name = name;
        Level = level;
        MaxHealth = maxHealth;
        AttackPower = attackPower;
        Armor = armor;
        MoveSpeed = moveSpeed;
        AggroRange = aggroRange;
        AttackRange = attackRange;
        AttackCooldown = attackCooldown;
        LeashRange = leashRange;
        RespawnDelay = respawnDelay;
        Resistances = resistances;
        ExperienceReward = experienceReward;
    }

    public CreatureDefinitionId Id { get; }

    public string Name { get; }

    public int Level { get; }

    public int MaxHealth { get; }

    public double AttackPower { get; }

    public double Armor { get; }

    /// <summary>Tiles the creature may move per AI tick.</summary>
    public double MoveSpeed { get; }

    /// <summary>Distance at which the creature starts chasing (Chebyshev tiles).</summary>
    public int AggroRange { get; }

    /// <summary>Distance at which the creature can attack (Chebyshev tiles).</summary>
    public int AttackRange { get; }

    public TimeSpan AttackCooldown { get; }

    /// <summary>Distance from the spawn point beyond which the creature returns.</summary>
    public int LeashRange { get; }

    public TimeSpan RespawnDelay { get; }

    public IReadOnlyDictionary<DamageType, double> Resistances { get; }

    /// <summary>Experience granted to the killer.</summary>
    public long ExperienceReward { get; }

    /// <summary>Combat stats used when the creature attacks.</summary>
    public CombatStats AttackStats() =>
        new(AttackPower, Armor, CriticalChance: 0, CriticalMultiplier: 1, Resistances);

    /// <summary>Combat stats used when the creature is attacked.</summary>
    public CombatStats DefenseStats() =>
        new(0, Armor, CriticalChance: 0, CriticalMultiplier: 1, Resistances);

    /// <summary>The creature's basic attack, expressed as a reusable ability.</summary>
    public AbilityDefinition BasicAttack() =>
        new(
            new AbilityId($"{Id.Value}.attack"),
            $"{Name} Attack",
            DamageType.Physical,
            baseDamage: 0,
            powerScaling: 1.0,
            cooldown: AttackCooldown,
            range: AttackRange);
}
