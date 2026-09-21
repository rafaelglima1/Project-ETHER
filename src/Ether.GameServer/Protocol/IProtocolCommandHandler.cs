using Ether.Contracts.Realtime;

namespace Ether.GameServer.Protocol;

/// <summary>Sends messages back to the client of a session.</summary>
public interface IProtocolResponder
{
    Task SendEventAsync(string name, object? payload, Guid? requestId, CancellationToken cancellationToken);

    Task SendErrorAsync(string name, string code, string message, Guid? requestId, CancellationToken cancellationToken);
}

/// <summary>Handles a single realtime command name.</summary>
public interface IProtocolCommandHandler
{
    string Name { get; }

    Task HandleAsync(
        Sessions.GameSession session,
        ProtocolEnvelope envelope,
        IProtocolResponder responder,
        CancellationToken cancellationToken);
}
