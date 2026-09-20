using Ether.Application.Abstractions;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Characters;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Characters;

/// <summary>Use case: move a character to a server-validated destination.</summary>
public sealed class MoveCharacterHandler
{
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorldMapProvider _maps;
    private readonly WorldOptions _options;
    private readonly TimeProvider _timeProvider;

    public MoveCharacterHandler(
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        IWorldMapProvider maps,
        IOptions<WorldOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _characters = characters;
        _unitOfWork = unitOfWork;
        _maps = maps;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CharacterResponse> HandleAsync(
        CharacterId characterId,
        MoveCharacterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var character = await _characters.GetByIdAsync(characterId, cancellationToken).ConfigureAwait(false)
                        ?? throw new Exceptions.CharacterNotFoundException(characterId);

        var map = _maps.GetMap(character.MapId);
        var destination = new WorldPosition(map.Id, request.X, request.Y);

        character.MoveTo(destination, _options.MaxMoveDistance, map, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CharacterMapping.ToResponse(character);
    }
}
