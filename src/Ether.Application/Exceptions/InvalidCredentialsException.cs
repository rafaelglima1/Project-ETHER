namespace Ether.Application.Exceptions;

/// <summary>Raised when an email/password combination is invalid.</summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}
