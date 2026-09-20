using Ether.Domain.Common;

namespace Ether.Domain.World;

/// <summary>
/// A rectangular 2D tile map. For the first playable the map is a simple bounded
/// grid; tile-level collision and line of sight arrive with richer world content.
/// </summary>
public sealed class WorldMap
{
    public WorldMap(MapId id, int width, int height)
    {
        if (id.IsEmpty)
        {
            throw new DomainException("Map requires a valid id.");
        }

        if (width < 1 || height < 1)
        {
            throw new DomainException("Map dimensions must be positive.");
        }

        Id = id;
        Width = width;
        Height = height;
    }

    public MapId Id { get; }

    public int Width { get; }

    public int Height { get; }

    public bool Contains(WorldPosition position) =>
        position.MapId == Id && position.X < Width && position.Y < Height;
}
