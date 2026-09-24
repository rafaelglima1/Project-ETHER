using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Application.Tests.Characters;

public sealed class RespawnCharacterHandlerTests
{
    private static readonly CharacterOptions Options = new() { StartingMapId = 1, StartingX = 0, StartingY = 0 };

    private static RespawnCharacterHandler CreateHandler(InMemoryPersistence persistence) =>
        new(
            persistence,
            persistence,
            new FakeWorldMapProvider(),
            Microsoft.Extensions.Options.Options.Create(Options),
            TimeProvider.System);

    private static async Task<(InMemoryPersistence Persistence, AccountId AccountId, Character Character)> SeedDeadAsync()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, character) = await persistence.SeedInWorldCharacterAsync("Hero", 0, 0, maxHealth: 100);
        character.ApplyDamage(character.MaxHealth, DateTimeOffset.UtcNow);
        return (persistence, accountId, character);
    }

    [Fact]
    public async Task Respawn_restores_health_and_state()
    {
        var (persistence, accountId, character) = await SeedDeadAsync();
        var handler = CreateHandler(persistence);

        var response = await handler.HandleAsync(accountId, character.Id, CancellationToken.None);

        Assert.Equal(CharacterState.InWorld.ToString(), response.State);
        Assert.Equal(character.MaxHealth, response.Health);
    }

    [Fact]
    public async Task Respawn_requires_dead_state()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, character) = await persistence.SeedInWorldCharacterAsync("Alive", 0, 0);
        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<CharacterNotDeadException>(() =>
            handler.HandleAsync(accountId, character.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Respawn_requires_matching_account()
    {
        var (persistence, _, deadCharacter) = await SeedDeadAsync();
        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(AccountId.New(), deadCharacter.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Respawn_unknown_character_returns_not_found()
    {
        var (persistence, accountId, _) = await SeedDeadAsync();
        var handler = CreateHandler(persistence);

        await Assert.ThrowsAsync<CharacterNotFoundException>(() =>
            handler.HandleAsync(accountId, CharacterId.New(), CancellationToken.None));
    }
}
