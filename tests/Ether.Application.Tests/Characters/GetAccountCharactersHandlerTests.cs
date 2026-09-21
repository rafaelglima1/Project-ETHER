using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Characters;

public sealed class GetAccountCharactersHandlerTests
{
    [Fact]
    public async Task Returns_only_the_characters_of_the_account()
    {
        var persistence = new InMemoryPersistence();
        var owner = Account.Create(
            new Email($"owner-{Guid.NewGuid():N}@ether.local"), new PasswordHash("hashed:x"), DateTimeOffset.UtcNow);
        var other = Account.Create(
            new Email($"other-{Guid.NewGuid():N}@ether.local"), new PasswordHash("hashed:x"), DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(owner);
        await persistence.SeedAccountAsync(other);

        var create = new CreateCharacterHandler(
            persistence, persistence, persistence, TimeProvider.System, Options.Create(new CharacterOptions()));

        await create.HandleAsync(owner.Id, owner.Id, new CreateCharacterRequest("Frodo", "Warrior"), CancellationToken.None);
        await create.HandleAsync(owner.Id, owner.Id, new CreateCharacterRequest("Sam", "Warrior"), CancellationToken.None);
        await create.HandleAsync(other.Id, other.Id, new CreateCharacterRequest("Gollum", "Ranger"), CancellationToken.None);

        var handler = new GetAccountCharactersHandler(persistence, persistence);

        var response = await handler.HandleAsync(owner.Id, owner.Id, CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.All(response, character => Assert.Equal(owner.Id.Value, character.AccountId));
    }

    [Fact]
    public async Task Returns_an_empty_list_for_an_account_without_characters()
    {
        var persistence = new InMemoryPersistence();
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"), new PasswordHash("hashed:x"), DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);

        var handler = new GetAccountCharactersHandler(persistence, persistence);

        var response = await handler.HandleAsync(account.Id, account.Id, CancellationToken.None);

        Assert.Empty(response);
    }

    [Fact]
    public async Task Fails_when_the_account_does_not_exist()
    {
        var persistence = new InMemoryPersistence();
        var handler = new GetAccountCharactersHandler(persistence, persistence);
        var accountId = AccountId.New();

        await Assert.ThrowsAsync<AccountNotFoundException>(() =>
            handler.HandleAsync(accountId, accountId, CancellationToken.None));
    }

    [Fact]
    public async Task Fails_when_requesting_another_account()
    {
        var persistence = new InMemoryPersistence();
        var handler = new GetAccountCharactersHandler(persistence, persistence);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(AccountId.New(), AccountId.New(), CancellationToken.None));
    }
}
