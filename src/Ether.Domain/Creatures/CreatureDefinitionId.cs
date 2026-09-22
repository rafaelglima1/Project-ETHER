using Ether.Domain.Common;

namespace Ether.Domain.Creatures;

/// <summary>
/// Content-defined creature identifier (stable key, e.g. <c>creature.slime</c>),
/// matching the ability-id convention.
/// </summary>
public readonly record struct CreatureDefinitionId
{
    public const int MaxLength = 64;

    public CreatureDefinitionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("CreatureDefinitionId must not be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"CreatureDefinitionId must be at most {MaxLength} characters.");
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static CreatureDefinitionId Empty => default;

    public override string ToString() => Value;
}
