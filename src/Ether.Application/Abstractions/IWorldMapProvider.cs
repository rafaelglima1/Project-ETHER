using Ether.Domain.World;

namespace Ether.Application.Abstractions;

/// <summary>Provides read-only world map definitions.</summary>
public interface IWorldMapProvider
{
    WorldMap GetMap(MapId mapId);
}
