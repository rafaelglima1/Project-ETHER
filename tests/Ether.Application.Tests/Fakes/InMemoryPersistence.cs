using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Application.Tests.Fakes;

/// <summary>
/// In-memory test double implementing the persistence abstractions.
/// Mirrors the M1 behaviour: characters are committed on SaveChanges and the
/// name uniqueness rule is enforced at commit time.
/// </summary>
internal sealed class InMemoryPersistence : IAccountRepository, ICharacterRepository, IUnitOfWork
{
    private readonly List<Account> _accounts = [];
    private readonly List<Character> _characters = [];
    private readonly List<Character> _pendingCharacters = [];

    public IReadOnlyList<Character> Characters => _characters;

    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task<Account?> GetByIdAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.SingleOrDefault(account => account.Id == accountId));

    public Task AddAsync(Character character, CancellationToken cancellationToken)
    {
        _pendingCharacters.Add(character);
        return Task.CompletedTask;
    }

    public Task<Character?> GetByIdAsync(CharacterId characterId, CancellationToken cancellationToken) =>
        Task.FromResult(_characters.SingleOrDefault(character => character.Id == characterId));

    public Task<IReadOnlyList<Character>> GetByAccountIdAsync(
        AccountId accountId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Character>>(
            _characters.Where(character => character.AccountId == accountId)
                .OrderBy(character => character.CreatedAt)
                .ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var pending in _pendingCharacters)
        {
            if (_characters.Exists(character => string.Equals(character.Name, pending.Name, StringComparison.Ordinal)))
            {
                throw new DuplicateCharacterNameException(pending.Name);
            }
        }

        _characters.AddRange(_pendingCharacters);
        _pendingCharacters.Clear();
        return Task.CompletedTask;
    }

    public Task SeedAccountAsync(Account account, CancellationToken cancellationToken = default) =>
        AddAsync(account, cancellationToken);
}
