using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Application.Characters;

/// <summary>Use case: retrieve a character by id.</summary>
public sealed class GetCharacterHandler
{
    private readonly ICharacterRepository _characters;

    public GetCharacterHandler(ICharacterRepository characters)
    {
        _characters = characters;
    }

    public async Task<CharacterResponse> HandleAsync(
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        var character = await _characters.GetByIdAsync(characterId, cancellationToken).ConfigureAwait(false)
                        ?? throw new CharacterNotFoundException(characterId);

        return CharacterMapping.ToResponse(character);
    }
}
