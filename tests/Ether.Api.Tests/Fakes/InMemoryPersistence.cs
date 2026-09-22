using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Api.Tests.Fakes;

/// <summary>In-memory persistence double used by the HTTP contract tests.</summary>
internal sealed class InMemoryPersistence : IAccountRepository, ICharacterRepository, IUnitOfWork
{
    private readonly List<Account> _accounts = [];
    private readonly List<Character> _characters = [];
    private readonly List<Character> _pendingCharacters = [];

    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task<Account?> GetByIdAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.SingleOrDefault(account => account.Id == accountId));

    public Task<Account?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.SingleOrDefault(account => account.Email == email));

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

    public Task<IReadOnlyList<Character>> GetByMapAsync(
        Ether.Domain.World.MapId mapId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Character>>(_characters.Where(character => character.MapId == mapId).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var account in _accounts)
        {
            if (_accounts.Count(existing => existing.Email == account.Email) > 1)
            {
                throw new DuplicateEmailException(account.Email.Value);
            }
        }

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
}
