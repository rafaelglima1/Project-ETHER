using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Application.Tests.Fakes;

/// <summary>
/// In-memory test double implementing the persistence abstractions.
/// Mirrors persistence behaviour: entities are committed on SaveChanges and
/// uniqueness rules are enforced at commit time.
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

    public Task SeedAccountAsync(Account account, CancellationToken cancellationToken = default) =>
        AddAsync(account, cancellationToken);

    /// <summary>Seeds an account with an in-world character at the given tile.</summary>
    public async Task<(AccountId AccountId, Character Character)> SeedInWorldCharacterAsync(
        string name,
        int x,
        int y,
        int maxHealth = Character.DefaultMaxHealth,
        CancellationToken cancellationToken = default)
    {
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"),
            new PasswordHash("hashed:x"),
            DateTimeOffset.UtcNow);
        await AddAsync(account, cancellationToken);

        var character = Character.Create(
            account.Id,
            name,
            CharacterClass.Warrior,
            new Ether.Domain.World.WorldPosition(new Ether.Domain.World.MapId(1), x, y),
            DateTimeOffset.UtcNow,
            maxHealth);

        await AddAsync(character, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        character.EnterWorld(DateTimeOffset.UtcNow);

        return (account.Id, character);
    }
}
