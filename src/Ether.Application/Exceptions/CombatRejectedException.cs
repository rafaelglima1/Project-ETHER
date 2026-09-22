namespace Ether.Application.Exceptions;

/// <summary>Deterministic reasons a combat action can be rejected.</summary>
public enum CombatRejectionReason
{
    AbilityNotFound = 0,
    TargetNotFound = 1,
    TargetDead = 2,
    AttackerDead = 3,
    OutOfRange = 4,
    CooldownActive = 5,
    SelfTarget = 6,
    InvalidState = 7,
}

/// <summary>
/// Raised when a combat action is refused. Carries a machine-readable reason so
/// transports can map it to their own error codes without duplicating rules.
/// </summary>
public sealed class CombatRejectedException : Exception
{
    public CombatRejectedException(CombatRejectionReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public CombatRejectionReason Reason { get; }
}
