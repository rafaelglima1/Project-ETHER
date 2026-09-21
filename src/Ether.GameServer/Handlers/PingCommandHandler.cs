using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Handlers;

/// <summary>Heartbeat: answers system.ping with system.pong.</summary>
public sealed class PingCommandHandler : IProtocolCommandHandler
{
    private readonly TimeProvider _timeProvider;

    public PingCommandHandler(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.SystemPing;

    public Task HandleAsync(
        GameSession session,
        ProtocolEnvelope envelope,
        IProtocolResponder responder,
        CancellationToken cancellationToken) =>
        responder.SendEventAsync(
            ProtocolMessageNames.SystemPong,
            new { serverTime = _timeProvider.GetUtcNow() },
            envelope.RequestId,
            cancellationToken);
}
