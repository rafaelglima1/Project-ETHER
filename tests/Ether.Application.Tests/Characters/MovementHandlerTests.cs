using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;

using Microsoft.Extensions.Options;

namespace Ether.Application.Tests.Characters;

public sealed class MovementHandlerTests
{
    private static readonly WorldOptions WorldOptions = new() { Width = 32, Height = 32, MaxMoveDistance = 12 };

    private static async Task<(InMemoryPersistence Persistence, CharacterId CharacterId)> SeedInWorldCharacterAsync()
    {
        var persistence = new InMemoryPersistence();
        var account = Account.Create(
            new Email($"acct-{Guid.NewGuid():N}@ether.local"), new PasswordHash("hashed:x"), DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);

        var create = new CreateCharacterHandler(
            persistence, persistence, persistence, TimeProvider.System, Options.Create(new CharacterOptions()));

        var created = await create.HandleAsync(
            account.Id, new CreateCharacterRequest("Mover", "Warrior"), CancellationToken.None);

        var characterId = new CharacterId(created.CharacterId);
        var enter = new EnterWorldHandler(persistence, persistence, TimeProvider.System);
        await enter.HandleAsync(characterId, CancellationToken.None);

        return (persistence, characterId);
    }

    private static MoveCharacterHandler MoveHandler(InMemoryPersistence persistence) =>
        new(
            persistence,
            persistence,
            new FakeWorldMapProvider(),
            Options.Create(WorldOptions),
            TimeProvider.System);

    [Fact]
    public async Task Enter_world_returns_the_in_world_state()
    {
        var (persistence, characterId) = await SeedInWorldCharacterAsync();

        var response = await new GetCharacterHandler(persistence).HandleAsync(characterId, CancellationToken.None);

        Assert.Equal("InWorld", response.State);
    }

    [Fact]
    public async Task Valid_move_updates_the_position()
    {
        var (persistence, characterId) = await SeedInWorldCharacterAsync();

        var response = await MoveHandler(persistence)
            .HandleAsync(characterId, new MoveCharacterRequest(4, 6), CancellationToken.None);

        Assert.Equal(4, response.PositionX);
        Assert.Equal(6, response.PositionY);
    }

    [Fact]
    public async Task Move_beyond_max_distance_is_rejected()
    {
        var (persistence, characterId) = await SeedInWorldCharacterAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            MoveHandler(persistence).HandleAsync(characterId, new MoveCharacterRequest(30, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Move_outside_the_map_is_rejected()
    {
        var (persistence, characterId) = await SeedInWorldCharacterAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            MoveHandler(persistence).HandleAsync(characterId, new MoveCharacterRequest(100, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Move_unknown_character_is_rejected()
    {
        var persistence = new InMemoryPersistence();

        await Assert.ThrowsAsync<CharacterNotFoundException>(() =>
            MoveHandler(persistence).HandleAsync(CharacterId.New(), new MoveCharacterRequest(1, 1), CancellationToken.None));
    }
}
