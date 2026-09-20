using System.Globalization;

using Ether.Domain.Common;

namespace Ether.Domain.World;

/// <summary>
/// Strongly-typed identifier for a map (Blueprint v5.0 §5).
/// Map identifiers are content-defined positive integers, not UUIDs.
/// </summary>
public readonly record struct MapId
{
    public MapId(int value)
    {
        if (value < 1)
        {
            throw new DomainException("MapId must be a positive integer.");
        }

        Value = value;
    }

    public int Value { get; }

    public static MapId Empty => default;

    public bool IsEmpty => Value < 1;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
