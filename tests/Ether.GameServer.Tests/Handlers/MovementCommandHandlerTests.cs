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

public sealed class MovementCommandHandlerTests
{
    private static readonly WorldOptions World = new() { Width = 32, Height = 32, MaxMoveDistance = 12 };

    private static ProtocolEnvelope Command(int x, int y, long sequence = 1) =>
        new ProtocolSerializer().Build(
            ProtocolMessageTypes.Command,
            ProtocolMessageNames.MovementMove,
            new MovementMoveCommandPayload(x, y),
            Guid.NewGuid(),
            sequence);

    private static MovementCommandHandler CreateHandler(InMemoryPersistence persistence, int maxPerSecond = 20)
    {
        var move = new MoveCharacterHandler(
            persistence,
            persistence,
            new FakeWorldMapProvider(),
            Options.Create(World),
            TimeProvider.System);

        return new MovementCommandHandler(
            move,
            new ProtocolSerializer(),
            Options.Create(new GameServerOptions { MaxMovementCommandsPerSecond = maxPerSecond }),
            TimeProvider.System);
    }

    private static async Task<(MovementCommandHandler Handler, GameSession Session, CapturingResponder Responder)>
        SetupInWorldAsync(InMemoryPersistence persistence, int maxPerSecond = 20)
    {
        var (accountId, characterId) = await persistence.SeedCharacterAsync();

        // Put the authoritative character into the world (the real flow does this
        // through world.enter; the session-level test seeds it directly).
        var character = await persistence.GetByIdAsync(characterId, CancellationToken.None);
        character!.EnterWorld(DateTimeOffset.UtcNow);

        var session = new GameSession(64, DateTimeOffset.UtcNow);
        session.MarkAuthenticated(accountId, characterId, DateTimeOffset.UtcNow);
        session.MarkInWorld(DateTimeOffset.UtcNow);

        return (CreateHandler(persistence, maxPerSecond), session, new CapturingResponder());
    }

    [Fact]
    public async Task Valid_movement_is_accepted()
    {
        var persistence = new InMemoryPersistence();
        var (handler, session, responder) = await SetupInWorldAsync(persistence);

        await handler.HandleAsync(session, Command(3, 5), responder, CancellationToken.None);

        var sent = Assert.Single(responder.Events);
        Assert.Equal(ProtocolMessageNames.MovementAccepted, sent.Name);
        var payload = Assert.IsType<MovementAcceptedEventPayload>(sent.Payload);
        Assert.Equal(3, payload.X);
        Assert.Equal(5, payload.Y);
    }

    [Fact]
    public async Task Unauthenticated_session_cannot_move()
    {
        var persistence = new InMemoryPersistence();
        var handler = CreateHandler(persistence);
        var session = new GameSession(16, DateTimeOffset.UtcNow);
        var responder = new CapturingResponder();

        await handler.HandleAsync(session, Command(1, 1), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotAuthenticated, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Not_in_world_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (accountId, characterId) = await persistence.SeedCharacterAsync();
        var session = new GameSession(16, DateTimeOffset.UtcNow);
        session.MarkAuthenticated(accountId, characterId, DateTimeOffset.UtcNow);
        var responder = new CapturingResponder();

        await CreateHandler(persistence).HandleAsync(session, Command(1, 1), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotInWorld, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Out_of_bounds_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (handler, session, responder) = await SetupInWorldAsync(persistence);

        await handler.HandleAsync(session, Command(100, 100), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.OutOfBounds, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Excessive_distance_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var (handler, session, responder) = await SetupInWorldAsync(persistence);

        await handler.HandleAsync(session, Command(30, 0), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.TooFar, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Rate_limit_is_enforced()
    {
        var persistence = new InMemoryPersistence();
        var (handler, session, responder) = await SetupInWorldAsync(persistence, maxPerSecond: 1);

        await handler.HandleAsync(session, Command(1, 1), responder, CancellationToken.None);
        await handler.HandleAsync(session, Command(1, 1), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.RateLimited, responder.Errors[^1].Code);
    }
}
