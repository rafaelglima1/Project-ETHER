namespace Ether.Contracts.Combat;

/// <summary>Result of a resolved attack, produced entirely by the server.</summary>
/// <remarks>
/// <c>AttackerType</c> and <c>TargetType</c> are additive (M6): <c>character</c> or
/// <c>creature</c>. They default to <c>character</c> so M5 clients keep working.
/// </remarks>
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
    bool TargetDefeated,
    string AttackerType = "character",
    string TargetType = "character");
