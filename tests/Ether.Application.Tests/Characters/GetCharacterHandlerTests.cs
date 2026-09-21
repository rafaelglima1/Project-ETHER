using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Characters;

public sealed class GetCharacterHandlerTests
{
    private static async Task<(InMemoryPersistence Persistence, AccountId AccountId, CharacterId CharacterId)> SeedAsync()
    {
        var persistence = new InMemoryPersistence();
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"),
            new PasswordHash("hashed:x"),
            DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);

        var create = new CreateCharacterHandler(
            persistence,
            persistence,
            persistence,
            TimeProvider.System,
            Options.Create(new CharacterOptions()));

        var response = await create.HandleAsync(
            account.Id,
            account.Id,
            new CreateCharacterRequest("Legolas", "Ranger"),
            CancellationToken.None);

        return (persistence, account.Id, new CharacterId(response.CharacterId));
    }

    [Fact]
    public async Task Returns_the_character_when_it_exists()
    {
        var (persistence, accountId, characterId) = await SeedAsync();
        var handler = new GetCharacterHandler(persistence);

        var response = await handler.HandleAsync(accountId, characterId, CancellationToken.None);

        Assert.Equal(characterId.Value, response.CharacterId);
        Assert.Equal("Legolas", response.Name);
        Assert.Equal("Ranger", response.CharacterClass);
    }

    [Fact]
    public async Task Fails_when_the_character_does_not_exist()
    {
        var persistence = new InMemoryPersistence();
        var handler = new GetCharacterHandler(persistence);

        await Assert.ThrowsAsync<CharacterNotFoundException>(() =>
            handler.HandleAsync(AccountId.New(), CharacterId.New(), CancellationToken.None));
    }

    [Fact]
    public async Task Fails_when_the_character_belongs_to_another_account()
    {
        var (persistence, _, characterId) = await SeedAsync();
        var handler = new GetCharacterHandler(persistence);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(AccountId.New(), characterId, CancellationToken.None));
    }
}
