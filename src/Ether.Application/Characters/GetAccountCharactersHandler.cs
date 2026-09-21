using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Domain.Accounts;

namespace Ether.Application.Characters;

/// <summary>Use case: list the characters owned by an account.</summary>
public sealed class GetAccountCharactersHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;

    public GetAccountCharactersHandler(IAccountRepository accounts, ICharacterRepository characters)
    {
        _accounts = accounts;
        _characters = characters;
    }

    public async Task<IReadOnlyList<CharacterResponse>> HandleAsync(
        AccountId authenticatedAccountId,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        if (authenticatedAccountId != accountId)
        {
            throw new ForbiddenException("Cannot read characters of another account.");
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false)
                      ?? throw new AccountNotFoundException(accountId);

        var characters = await _characters.GetByAccountIdAsync(account.Id, cancellationToken).ConfigureAwait(false);

        return characters.Select(CharacterMapping.ToResponse).ToList();
    }
}
