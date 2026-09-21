using Ether.Contracts.Realtime;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Protocol;

/// <summary>Writes responses to a session's outbound queue.</summary>
internal sealed class SessionResponder : IProtocolResponder
{
    private readonly GameSession _session;
    private readonly ProtocolSerializer _serializer;

    public SessionResponder(GameSession session, ProtocolSerializer serializer)
    {
        _session = session;
        _serializer = serializer;
    }

    public Task SendEventAsync(string name, object? payload, Guid? requestId, CancellationToken cancellationToken)
    {
        var envelope = _serializer.Build(
            ProtocolMessageTypes.Event,
            name,
            payload,
            requestId,
            _session.NextOutboundSequence());

        _session.Outbound.Writer.TryWrite(_serializer.Serialize(envelope));
        return Task.CompletedTask;
    }

    public Task SendErrorAsync(string name, string code, string message, Guid? requestId, CancellationToken cancellationToken)
    {
        var envelope = _serializer.Error(
            name,
            code,
            message,
            requestId,
            _session.NextOutboundSequence());

        _session.Outbound.Writer.TryWrite(_serializer.Serialize(envelope));
        return Task.CompletedTask;
    }
}
