using Ether.Domain.Common;

namespace Ether.Domain.Characters;

/// <summary>
/// Strongly-typed identifier for a character.
/// </summary>
public readonly record struct CharacterId
{
    public CharacterId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("CharacterId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static CharacterId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static CharacterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
