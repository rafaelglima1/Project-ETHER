using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.Application.Tests.Characters;

public sealed class EnterWorldDeadCharacterTests
{
    private static EnterWorldHandler CreateHandler(InMemoryPersistence persistence) =>
        new(persistence, persistence, TimeProvider.System);

    [Fact]
    public async Task Dead_character_cannot_enter_world()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, character) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0);
        character.ApplyDamage(character.MaxHealth, DateTimeOffset.UtcNow);

        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<CharacterDeadException>(() =>
            handler.HandleAsync(accountId, character.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Alive_character_enters_world()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, character) = await persistence.SeedInWorldCharacterAsync("Alive", 0, 0);

        var handler = CreateHandler(persistence);

        var response = await handler.HandleAsync(accountId, character.Id, CancellationToken.None);

        Assert.Equal(CharacterState.InWorld.ToString(), response.State);
    }
}
