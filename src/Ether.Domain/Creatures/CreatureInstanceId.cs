using Ether.Domain.Common;

namespace Ether.Domain.Creatures;

/// <summary>
/// Strongly-typed identifier for a live creature instance in the world.
/// </summary>
public readonly record struct CreatureInstanceId
{
    public CreatureInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("CreatureInstanceId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static CreatureInstanceId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static CreatureInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
