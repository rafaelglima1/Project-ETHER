namespace Ether.Contracts.Realtime;

/// <summary>
/// Payload of <see cref="ProtocolMessageNames.CombatAttack"/>.
/// The client only expresses intent: the server resolves damage, criticals and HP.
/// </summary>
public sealed record AttackCommandPayload(string AbilityId, Guid TargetId);
