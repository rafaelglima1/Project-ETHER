using Ether.Contracts.Realtime;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.DependencyInjection;

namespace Ether.GameServer.Protocol;

/// <summary>
/// Resolves the handler for a command name. Handlers are resolved from a fresh
/// scope per command so persistence/use-case dependencies stay request-scoped.
/// </summary>
public sealed class ProtocolDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProtocolDispatcher(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Returns false when no handler is registered for the message name.</summary>
    public async Task<bool> DispatchAsync(
        GameSession session,
        ProtocolEnvelope envelope,
        IProtocolResponder responder,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetServices<IProtocolCommandHandler>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, envelope.Name, StringComparison.Ordinal));

        if (handler is null)
        {
            return false;
        }

        await handler.HandleAsync(session, envelope, responder, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
