using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Application.Abstractions;

/// <summary>Persistence abstraction for the <see cref="Character"/> aggregate.</summary>
public interface ICharacterRepository
{
    Task AddAsync(Character character, CancellationToken cancellationToken);

    Task<Character?> GetByIdAsync(CharacterId characterId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Character>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken);
}
