using Ether.Domain.Common;

namespace Ether.Domain.Combat;

/// <summary>
/// Strongly-typed ability identifier. Abilities are content-defined, so the id is a
/// stable key (e.g. <c>warrior.basic_attack</c>) rather than a random Guid.
/// </summary>
public readonly record struct AbilityId
{
    public const int MaxLength = 64;

    public AbilityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("AbilityId must not be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"AbilityId must be at most {MaxLength} characters.");
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static AbilityId Empty => default;

    public override string ToString() => Value;
}
