using Ether.Domain.Common;

namespace Ether.Domain.Social;

/// <summary>
/// Strongly-typed identifier for a party.
/// </summary>
public readonly record struct PartyId
{
    public PartyId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("PartyId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static PartyId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static PartyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
