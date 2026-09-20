namespace Ether.Domain.Common;

/// <summary>
/// Base exception for domain rule violations.
/// Thrown when an invariant, state transition or business rule is not satisfied.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
