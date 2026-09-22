namespace Ether.Contracts.Realtime;

/// <summary>
/// Payload of <see cref="ProtocolMessageNames.CombatAttack"/>.
/// The client only expresses intent: the server resolves damage, criticals and HP.
/// <paramref name="TargetType"/> is <c>character</c> (default) or <c>creature</c>.
/// </summary>
public sealed record AttackCommandPayload(string AbilityId, Guid TargetId, string? TargetType = null);
