namespace Ether.Application.Exceptions;

/// <summary>
/// Raised when an authenticated account attempts to access a resource it does
/// not own. Mapped to HTTP 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You are not allowed to access this resource.")
        : base(message)
    {
    }
}
