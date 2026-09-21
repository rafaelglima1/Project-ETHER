using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Common;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Characters;

public sealed class CreateCharacterHandlerTests
{
    private static CreateCharacterHandler CreateHandler(
        InMemoryPersistence persistence,
        CharacterOptions? options = null) =>
        new(
            persistence,
            persistence,
            persistence,
            TimeProvider.System,
            Options.Create(options ?? new CharacterOptions()));

    private static async Task<AccountId> SeedAccountAsync(InMemoryPersistence persistence)
    {
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"),
            new PasswordHash("hashed:x"),
            DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);
        return account.Id;
    }

    [Fact]
    public async Task Creates_a_character_for_an_existing_account()
    {
        var persistence = new InMemoryPersistence();
        var accountId = await SeedAccountAsync(persistence);
        var handler = CreateHandler(persistence, new CharacterOptions { StartingMapId = 5, StartingX = 2, StartingY = 3 });

        var response = await handler.HandleAsync(
            accountId,
            accountId,
            new CreateCharacterRequest("Aragorn", "Warrior"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.CharacterId);
        Assert.Equal(accountId.Value, response.AccountId);
        Assert.Equal("Aragorn", response.Name);
        Assert.Equal("Warrior", response.CharacterClass);
        Assert.Equal("Offline", response.State);
        Assert.Equal(1, response.Level);
        Assert.Equal(0, response.Experience);
        Assert.Equal(5, response.MapId);
        Assert.Equal(2, response.PositionX);
        Assert.Equal(3, response.PositionY);
    }

    [Fact]
    public async Task Fails_when_the_account_does_not_exist()
    {
        var persistence = new InMemoryPersistence();
        var handler = CreateHandler(persistence);
        var accountId = AccountId.New();

        await Assert.ThrowsAsync<AccountNotFoundException>(() =>
            handler.HandleAsync(accountId, accountId, new CreateCharacterRequest("Aragorn", "Warrior"), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fails_for_an_invalid_name(string name)
    {
        var persistence = new InMemoryPersistence();
        var accountId = await SeedAccountAsync(persistence);
        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(accountId, accountId, new CreateCharacterRequest(name, "Warrior"), CancellationToken.None));
    }

    [Fact]
    public async Task Fails_for_an_invalid_class()
    {
        var persistence = new InMemoryPersistence();
        var accountId = await SeedAccountAsync(persistence);
        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(accountId, accountId, new CreateCharacterRequest("Aragorn", "Wizard"), CancellationToken.None));
    }

    [Fact]
    public async Task Fails_for_a_duplicate_name()
    {
        var persistence = new InMemoryPersistence();
        var accountId = await SeedAccountAsync(persistence);
        var handler = CreateHandler(persistence);

        await handler.HandleAsync(accountId, accountId, new CreateCharacterRequest("Aragorn", "Warrior"), CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateCharacterNameException>(() =>
            handler.HandleAsync(accountId, accountId, new CreateCharacterRequest("Aragorn", "Ranger"), CancellationToken.None));
    }
}
