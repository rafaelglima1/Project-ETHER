namespace Ether.Application.Exceptions;

/// <summary>Raised when a refresh or game token is missing, expired or invalid.</summary>
public sealed class InvalidTokenException : Exception
{
    public InvalidTokenException(string message = "Token is invalid or expired.")
        : base(message)
    {
    }
}
