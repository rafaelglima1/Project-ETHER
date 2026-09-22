namespace Ether.Contracts.Combat;

/// <summary>Result of a resolved attack, produced entirely by the server.</summary>
public sealed record CombatResultResponse(
    Guid AttackerId,
    Guid TargetId,
    string AbilityId,
    int RawDamage,
    int Damage,
    bool Critical,
    int TargetHealth,
    int TargetMaxHealth,
    string TargetState,
    bool TargetDefeated);
