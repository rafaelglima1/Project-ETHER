using Ether.Domain.Common;

namespace Ether.Domain.Accounts;

/// <summary>
/// Strongly-typed identifier for an account.
/// </summary>
public readonly record struct AccountId
{
    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("AccountId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AccountId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static AccountId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
