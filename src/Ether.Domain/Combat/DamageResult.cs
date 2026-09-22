namespace Ether.Domain.Combat;

/// <summary>Result of the pure damage pipeline (no HP applied yet).</summary>
public readonly record struct DamageResult(
    int RawDamage,
    int Damage,
    bool Critical,
    double ArmorMultiplier,
    double ResistanceMultiplier);
