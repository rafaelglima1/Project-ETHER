using Ether.Application.Abstractions;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Characters;

/// <summary>
/// Use case: respawn a dead character at the server-chosen safe spawn point with
/// full health. The client never decides that death happened or where respawn is.
/// </summary>
public sealed class RespawnCharacterHandler
{
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorldMapProvider _maps;
    private readonly CharacterOptions _options;
    private readonly TimeProvider _timeProvider;

    public RespawnCharacterHandler(
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        IWorldMapProvider maps,
        IOptions<CharacterOptions> options,
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
        AccountId authenticatedAccountId,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        var character = await _characters.GetByIdAsync(characterId, cancellationToken).ConfigureAwait(false)
                        ?? throw new Exceptions.CharacterNotFoundException(characterId);

        if (character.AccountId != authenticatedAccountId)
        {
            throw new Exceptions.ForbiddenException("Character does not belong to the authenticated account.");
        }

        if (character.State is not (CharacterState.Dead or CharacterState.Respawning))
        {
            throw new Exceptions.CharacterNotDeadException();
        }

        var mapId = new MapId(_options.StartingMapId);
        var map = _maps.GetMap(mapId);
        var spawn = new WorldPosition(mapId, _options.StartingX, _options.StartingY);

        if (!map.Contains(spawn))
        {
            throw new DomainException("Configured respawn point is outside the map.");
        }

        character.Respawn(spawn, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CharacterMapping.ToResponse(character);
    }
}
