using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Items;

namespace Ether.GameServer.Tests.Fakes;

/// <summary>In-memory persistence used by GameServer unit and integration tests.</summary>
internal sealed class InMemoryPersistence : IAccountRepository, ICharacterRepository, IItemInstanceRepository, IUnitOfWork
{
    private readonly List<Account> _accounts = [];
    private readonly List<Character> _characters = [];
    private readonly List<Character> _pendingCharacters = [];
    private readonly List<ItemInstance> _items = [];

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
            _characters.Where(character => character.AccountId == accountId).ToList());

    public Task<IReadOnlyList<Character>> GetByMapAsync(
        Ether.Domain.World.MapId mapId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Character>>(_characters.Where(character => character.MapId == mapId).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {        foreach (var pending in _pendingCharacters)
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

    public Task AddAsync(ItemInstance item, CancellationToken cancellationToken)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ItemInstance>> GetByOwnerAsync(
        CharacterId characterId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemInstance>>(
            _items.Where(item => item.OwnerCharacterId == characterId).ToList());

    public Task<IReadOnlyList<ItemInstance>> GetItemsByOwnerAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default) =>
        GetByOwnerAsync(characterId, cancellationToken);

    public async Task<(AccountId AccountId, CharacterId CharacterId)> SeedCharacterAsync(string name = "Hero")
    {
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"),
            new PasswordHash("hashed:x"),
            DateTimeOffset.UtcNow);
        await AddAsync(account, CancellationToken.None);

        var character = Character.Create(
            account.Id,
            name,
            CharacterClass.Warrior,
            new Ether.Domain.World.WorldPosition(new Ether.Domain.World.MapId(1), 0, 0),
            DateTimeOffset.UtcNow);
        await AddAsync(character, CancellationToken.None);
        await SaveChangesAsync(CancellationToken.None);

        return (account.Id, character.Id);
    }
}
