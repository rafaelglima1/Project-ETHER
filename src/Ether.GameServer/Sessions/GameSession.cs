using System.Threading.Channels;

using Ether.Contracts.Realtime;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;

namespace Ether.GameServer.Sessions;

/// <summary>
/// One realtime connection. Commands are processed sequentially through
/// <see cref="Commands"/> so a single session can never race against itself,
/// while sessions remain independent (no global lock).
/// </summary>
public sealed class GameSession
{
    private readonly object _rateGate = new();
    private long _outboundSequence;
    private long _rateWindowSecond = -1;
    private int _rateCount;

    public GameSession(int maxQueuedCommands, DateTimeOffset connectedAt)
    {
        if (maxQueuedCommands < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueuedCommands));
        }

        ConnectedAt = connectedAt;
        LastHeartbeat = connectedAt;
        Commands = Channel.CreateBounded<ProtocolEnvelope>(
            new BoundedChannelOptions(maxQueuedCommands)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        Outbound = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public Guid SessionId { get; } = Guid.NewGuid();

    public GameSessionState State { get; private set; } = GameSessionState.Connecting;

    public AccountId? AccountId { get; private set; }

    public CharacterId? CharacterId { get; private set; }

    public DateTimeOffset ConnectedAt { get; }

    public DateTimeOffset LastHeartbeat { get; private set; }

    public long LastReceivedSequence { get; private set; }

    /// <summary>Queue of inbound commands processed in order by the session pump.</summary>
    public Channel<ProtocolEnvelope> Commands { get; }

    /// <summary>Queue of outbound serialized frames written by the session writer.</summary>
    public Channel<string> Outbound { get; }

    /// <summary>Cancelled to terminate the session (timeout or shutdown).</summary>
    public CancellationTokenSource Lifetime { get; } = new();

    /// <summary>
    /// Simple per-session rate limiter (counts within an epoch-second window).
    /// Returns false when the budget for the current second is exhausted.
    /// </summary>
    public bool TryAcquireRateSlot(long epochSecond, int maxPerSecond)
    {
        lock (_rateGate)
        {
            if (_rateWindowSecond != epochSecond)
            {
                _rateWindowSecond = epochSecond;
                _rateCount = 0;
            }

            if (_rateCount >= maxPerSecond)
            {
                return false;
            }

            _rateCount++;
            return true;
        }
    }

    public long NextOutboundSequence() => Interlocked.Increment(ref _outboundSequence);

    public void MarkAuthenticated(AccountId accountId, CharacterId characterId, DateTimeOffset now)
    {
        AccountId = accountId;
        CharacterId = characterId;
        State = GameSessionState.Authenticated;
        Touch(now);
    }

    public void MarkInWorld(DateTimeOffset now)
    {
        State = GameSessionState.InWorld;
        Touch(now);
    }

    public void MarkDisconnecting() => State = GameSessionState.Disconnecting;

    public void MarkDisconnected() => State = GameSessionState.Disconnected;

    public void Touch(DateTimeOffset now) => LastHeartbeat = now;

    /// <summary>
    /// Accepts a strictly increasing sequence. Duplicate or stale sequences are
    /// rejected so a client cannot replay a command.
    /// </summary>
    public bool TryAcceptSequence(long sequence)
    {
        if (sequence <= LastReceivedSequence)
        {
            return false;
        }

        LastReceivedSequence = sequence;
        return true;
    }
}
