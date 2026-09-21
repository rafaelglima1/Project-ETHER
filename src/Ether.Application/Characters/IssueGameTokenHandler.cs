using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Auth;
using Ether.Domain.Characters;

namespace Ether.Application.Characters;

/// <summary>
/// Use case: issue a short-lived game token for a character owned by the
/// authenticated account. The token is the credential the GameServer consumes
/// when the realtime connection is implemented.
/// </summary>
public sealed class IssueGameTokenHandler
{
    private readonly ICharacterRepository _characters;
    private readonly ITokenService _tokenService;

    public IssueGameTokenHandler(ICharacterRepository characters, ITokenService tokenService)
    {
        _characters = characters;
        _tokenService = tokenService;
    }

    public async Task<GameTokenResponse> HandleAsync(
        Ether.Domain.Accounts.AccountId authenticatedAccountId,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        var character = await _characters.GetByIdAsync(characterId, cancellationToken).ConfigureAwait(false)
                        ?? throw new CharacterNotFoundException(characterId);

        if (character.AccountId != authenticatedAccountId)
        {
            throw new ForbiddenException("Character does not belong to the authenticated account.");
        }

        var token = _tokenService.CreateGameToken(authenticatedAccountId, characterId.Value);

        return new GameTokenResponse(token.Token, token.ExpiresAt);
    }
}
