using Ether.Domain.Common;

namespace Ether.Domain.Items;

/// <summary>
/// Strongly-typed identifier for a concrete item instance.
/// </summary>
public readonly record struct ItemInstanceId
{
    public ItemInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("ItemInstanceId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ItemInstanceId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static ItemInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
