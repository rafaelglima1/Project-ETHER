namespace Ether.Contracts.Combat;

/// <summary>Result of a resolved attack, produced entirely by the server.</summary>
/// <remarks>
/// <c>AttackerType</c> and <c>TargetType</c> are additive (M6): <c>character</c> or
/// <c>creature</c>. The progression fields and <c>Loot</c> are additive (M7) and are
/// populated only when a kill actually grants a reward.
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
    string TargetType = "character",
    long ExperienceGained = 0,
    int Level = 0,
    long Experience = 0,
    int LevelsGained = 0,
    IReadOnlyList<LootItemPayload>? Loot = null);
