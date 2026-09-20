using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Common;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.World;

/// <summary>
/// In-memory world map provider for the first playable. A single bounded map is
/// described by configuration; the content pipeline replaces this later.
/// </summary>
public sealed class StaticWorldMapProvider : IWorldMapProvider
{
    private readonly WorldMap _map;

    public StaticWorldMapProvider(IOptions<WorldOptions> options, IOptions<CharacterOptions> characterOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(characterOptions);

        _map = new WorldMap(
            new MapId(characterOptions.Value.StartingMapId),
            options.Value.Width,
            options.Value.Height);
    }

    public WorldMap GetMap(MapId mapId) =>
        mapId == _map.Id
            ? _map
            : throw new DomainException($"Map '{mapId.Value}' does not exist.");
}
