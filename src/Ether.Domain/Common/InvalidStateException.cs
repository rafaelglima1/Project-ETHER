namespace Ether.Domain.Common;

/// <summary>
/// Raised when an operation is not valid for the current entity/session state
/// (Blueprint v4.0 §144). Distinct from a generic invariant violation so callers
/// can map it to a specific outcome.
/// </summary>
public sealed class InvalidStateException : DomainException
{
    public InvalidStateException(string message) : base(message)
    {
    }
}
