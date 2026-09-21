using Ether.GameServer.Sessions;

namespace Ether.GameServer.Tests.Sessions;

public sealed class GameSessionTests
{
    private static GameSession NewSession(int maxQueued = 16) =>
        new(maxQueued, DateTimeOffset.UtcNow);

    [Fact]
    public void Starts_in_connecting_state()
    {
        var session = NewSession();

        Assert.Equal(GameSessionState.Connecting, session.State);
        Assert.Null(session.AccountId);
        Assert.Null(session.CharacterId);
    }

    [Fact]
    public void Authenticate_then_enter_world_follows_the_lifecycle()
    {
        var session = NewSession();
        var accountId = Ether.Domain.Accounts.AccountId.New();
        var characterId = Ether.Domain.Characters.CharacterId.New();

        session.MarkAuthenticated(accountId, characterId, DateTimeOffset.UtcNow);
        Assert.Equal(GameSessionState.Authenticated, session.State);

        session.MarkInWorld(DateTimeOffset.UtcNow);
        Assert.Equal(GameSessionState.InWorld, session.State);

        session.MarkDisconnecting();
        Assert.Equal(GameSessionState.Disconnecting, session.State);

        session.MarkDisconnected();
        Assert.Equal(GameSessionState.Disconnected, session.State);
    }

    [Fact]
    public void Sequences_must_strictly_increase()
    {
        var session = NewSession();

        Assert.True(session.TryAcceptSequence(1));
        Assert.True(session.TryAcceptSequence(2));
        Assert.False(session.TryAcceptSequence(2));
        Assert.False(session.TryAcceptSequence(1));
        Assert.True(session.TryAcceptSequence(3));
        Assert.Equal(3, session.LastReceivedSequence);
    }

    [Fact]
    public void Outbound_sequence_is_monotonic()
    {
        var session = NewSession();

        Assert.Equal(1, session.NextOutboundSequence());
        Assert.Equal(2, session.NextOutboundSequence());
    }

    [Fact]
    public void Rate_limiter_allows_up_to_the_budget_per_second()
    {
        var session = NewSession();

        Assert.True(session.TryAcquireRateSlot(100, maxPerSecond: 2));
        Assert.True(session.TryAcquireRateSlot(100, maxPerSecond: 2));
        Assert.False(session.TryAcquireRateSlot(100, maxPerSecond: 2));

        // Next second resets the budget.
        Assert.True(session.TryAcquireRateSlot(101, maxPerSecond: 2));
    }
}

public sealed class GameSessionManagerTests
{
    private static GameSessionManager NewManager(int maxConnections)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new Ether.Contracts.Configuration.GameServerOptions { MaxConnections = maxConnections });

        return new GameSessionManager(options);
    }

    [Fact]
    public void Creates_and_removes_sessions()
    {
        var manager = NewManager(2);

        var session = manager.Create(TimeProvider.System);
        Assert.NotNull(session);
        Assert.Equal(1, manager.Count);

        manager.Remove(session!.SessionId);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Rejects_connections_beyond_the_limit()
    {
        var manager = NewManager(1);

        Assert.NotNull(manager.Create(TimeProvider.System));
        Assert.Null(manager.Create(TimeProvider.System));
    }
}
