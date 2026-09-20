using Ether.Domain.Common;

namespace Ether.Domain.Accounts;

/// <summary>
/// Opaque password hash. The domain never sees the plaintext password; it only
/// carries the already-hashed representation produced by the infrastructure layer.
/// </summary>
public readonly record struct PasswordHash
{
    public const int MaxLength = 512;

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("PasswordHash must not be empty.");
        }

        if (value.Length > MaxLength)
        {
            throw new DomainException($"PasswordHash must be at most {MaxLength} characters.");
        }

        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static PasswordHash Empty => default;

    public override string ToString() => Value;
}
