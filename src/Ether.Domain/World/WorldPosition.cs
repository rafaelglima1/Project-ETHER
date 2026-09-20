using System.Globalization;

using Ether.Domain.Common;

namespace Ether.Domain.World;

/// <summary>
/// A position on the 2D tile grid (Blueprint v5.0 §7).
/// Logical positions are integers; visual interpolation belongs to the client.
/// </summary>
public readonly record struct WorldPosition
{
    public WorldPosition(MapId mapId, int x, int y)
    {
        if (mapId.IsEmpty)
        {
            throw new DomainException("WorldPosition requires a valid map.");
        }

        if (x < 0 || y < 0)
        {
            throw new DomainException("WorldPosition coordinates must not be negative.");
        }

        MapId = mapId;
        X = x;
        Y = y;
    }

    public MapId MapId { get; }

    public int X { get; }

    public int Y { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({MapId.Value},{X},{Y})");
}
