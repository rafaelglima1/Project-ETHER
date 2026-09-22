using Ether.Domain.Common;

namespace Ether.Domain.Items;

/// <summary>Content-defined item identifier (stable key, e.g. <c>item.slime_gel</c>).</summary>
public readonly record struct ItemDefinitionId
{
    public const int MaxLength = 100;

    public ItemDefinitionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("ItemDefinitionId must not be empty.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"ItemDefinitionId must be at most {MaxLength} characters.");
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static ItemDefinitionId Empty => default;

    public override string ToString() => Value;
}
