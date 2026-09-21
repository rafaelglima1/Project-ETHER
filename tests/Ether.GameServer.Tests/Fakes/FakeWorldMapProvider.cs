using Ether.Application.Abstractions;
using Ether.Domain.World;

namespace Ether.GameServer.Tests.Fakes;

/// <summary>Fixed 32x32 test map.</summary>
internal sealed class FakeWorldMapProvider : IWorldMapProvider
{
    private readonly WorldMap _map = new(new MapId(1), 32, 32);

    public WorldMap GetMap(MapId mapId) => _map;
}
