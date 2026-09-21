using Ether.Application.Characters;
using Ether.Contracts.Configuration;
using Ether.Contracts.Realtime;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.GameServer.Handlers;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;
using Ether.GameServer.Tests.Fakes;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Tests.Handlers;

public sealed class EnterWorldCommandHandlerTests
{
    private static ProtocolEnvelope Command(long sequence = 1) =>
        new ProtocolSerializer().Build(ProtocolMessageTypes.Command, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), Guid.NewGuid(), sequence);

    private static (EnterWorldCommandHandler Handler, GameSession Session, CapturingResponder Responder) Create(
        InMemoryPersistence persistence,
        AccountId accountId,
        CharacterId characterId)
    {
        var enterWorld = new EnterWorldHandler(persistence, persistence, TimeProvider.System);
        var handler = new EnterWorldCommandHandler(enterWorld, new FakeWorldMapProvider(), new ProtocolSerializer(), TimeProvider.System);

        var session = new GameSession(16, DateTimeOffset.UtcNow);
        session.MarkAuthenticated(accountId, characterId, DateTimeOffset.UtcNow);

        return (handler, session, new CapturingResponder());
    }

    [Fact]
    public async Task Unauthenticated_session_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var enterWorld = new EnterWorldHandler(persistence, persistence, TimeProvider.System);
        var handler = new EnterWorldCommandHandler(enterWorld, new FakeWorldMapProvider(), new ProtocolSerializer(), TimeProvider.System);
        var session = new GameSession(16, DateTimeOffset.UtcNow);
        var responder = new CapturingResponder();

        await handler.HandleAsync(session, Command(), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotAuthenticated, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Enter_world_returns_a_snapshot()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, characterId) = await persistence.SeedCharacterAsync();
        var (handler, session, responder) = Create(persistence, accountId, characterId);

        await handler.HandleAsync(session, Command(), responder, CancellationToken.None);

        Assert.Equal(GameSessionState.InWorld, session.State);
        var sent = Assert.Single(responder.Events);
        Assert.Equal(ProtocolMessageNames.WorldSnapshot, sent.Name);

        var snapshot = Assert.IsType<WorldSnapshotPayload>(sent.Payload);
        Assert.Equal(1, snapshot.MapId);
        Assert.Equal(characterId.Value, snapshot.Player.CharacterId);
        Assert.Equal("InWorld", snapshot.Player.State);
    }

    [Fact]
    public async Task Foreign_character_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (_, _) = await persistence.SeedCharacterAsync();
        var (handler, session, responder) = Create(persistence, AccountId.New(), CharacterId.New());

        await handler.HandleAsync(session, Command(), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.InvalidState, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Cannot_enter_world_twice()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, characterId) = await persistence.SeedCharacterAsync();
        var (handler, session, responder) = Create(persistence, accountId, characterId);

        await handler.HandleAsync(session, Command(), responder, CancellationToken.None);
        await handler.HandleAsync(session, Command(sequence: 2), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.AlreadyInWorld, responder.Errors[^1].Code);
    }
}
