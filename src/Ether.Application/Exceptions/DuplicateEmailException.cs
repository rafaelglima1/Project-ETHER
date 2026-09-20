namespace Ether.Application.Exceptions;

/// <summary>
/// Raised when an email is already registered. The guarantee is enforced by a
/// unique database constraint; this exception is the mapped outcome.
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string? email = null)
        : base(email is null
            ? "Email is already registered."
            : $"Email '{email}' is already registered.")
    {
        Email = email;
    }

    public string? Email { get; }
}
