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
        var owner = Account.Create(DateTimeOffset.UtcNow);
        var other = Account.Create(DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(owner);
        await persistence.SeedAccountAsync(other);

        var create = new CreateCharacterHandler(
            persistence, persistence, persistence, TimeProvider.System, Options.Create(new CharacterOptions()));

        await create.HandleAsync(owner.Id, new CreateCharacterRequest("Frodo", "Warrior"), CancellationToken.None);
        await create.HandleAsync(owner.Id, new CreateCharacterRequest("Sam", "Warrior"), CancellationToken.None);
        await create.HandleAsync(other.Id, new CreateCharacterRequest("Gollum", "Ranger"), CancellationToken.None);

        var handler = new GetAccountCharactersHandler(persistence, persistence);

        var response = await handler.HandleAsync(owner.Id, CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.All(response, character => Assert.Equal(owner.Id.Value, character.AccountId));
    }

    [Fact]
    public async Task Returns_an_empty_list_for_an_account_without_characters()
    {
        var persistence = new InMemoryPersistence();
        var account = Account.Create(DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);

        var handler = new GetAccountCharactersHandler(persistence, persistence);

        var response = await handler.HandleAsync(account.Id, CancellationToken.None);

        Assert.Empty(response);
    }

    [Fact]
    public async Task Fails_when_the_account_does_not_exist()
    {
        var persistence = new InMemoryPersistence();
        var handler = new GetAccountCharactersHandler(persistence, persistence);

        await Assert.ThrowsAsync<AccountNotFoundException>(() =>
            handler.HandleAsync(AccountId.New(), CancellationToken.None));
    }
}
