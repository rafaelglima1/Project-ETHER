using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;

namespace Ether.GameServer.Tests.Fakes;

/// <summary>Captures protocol responses for assertions.</summary>
internal sealed class CapturingResponder : IProtocolResponder
{
    public sealed record SentEvent(string Name, object? Payload, Guid? RequestId);

    public sealed record SentError(string Name, string Code, string Message, Guid? RequestId);

    public List<SentEvent> Events { get; } = [];

    public List<SentError> Errors { get; } = [];

    public Task SendEventAsync(string name, object? payload, Guid? requestId, CancellationToken cancellationToken)
    {
        Events.Add(new SentEvent(name, payload, requestId));
        return Task.CompletedTask;
    }

    public Task SendErrorAsync(string name, string code, string message, Guid? requestId, CancellationToken cancellationToken)
    {
        Errors.Add(new SentError(name, code, message, requestId));
        return Task.CompletedTask;
    }
}
