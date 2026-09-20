using Ether.Domain.Common;

namespace Ether.Domain.Accounts;

/// <summary>
/// Account email address. Normalized to lower case on construction.
/// </summary>
public readonly record struct Email
{
    public const int MaxLength = 254;

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Email must not be empty.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"Email must be at most {MaxLength} characters.");
        }

        if (!IsValid(trimmed))
        {
            throw new DomainException("Email format is invalid.");
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static Email Empty => default;

    private static bool IsValid(string value)
    {
        if (value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0;
    }

    public override string ToString() => Value;
}
