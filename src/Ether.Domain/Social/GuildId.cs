using Ether.Domain.Common;

namespace Ether.Domain.Social;

/// <summary>
/// Strongly-typed identifier for a guild.
/// </summary>
public readonly record struct GuildId
{
    public GuildId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("GuildId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static GuildId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static GuildId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
