using System.Net.WebSockets;
using System.Text;

using Ether.Contracts.Configuration;
using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Realtime;

/// <summary>
/// WebSocket transport for the realtime protocol. Transport only: it authenticates,
/// deserializes, dispatches and writes back. Gameplay rules live in Application/Domain.
/// </summary>
internal static class GameWebSocketEndpoint
{
    private const int ReceiveBufferSize = 4096;

    public static void MapGameWebSocket(this WebApplication app, string path)
    {
        app.Map(path, async context => { await HandleAsync(context).ConfigureAwait(false); });
    }

    private static async Task HandleAsync(HttpContext context)
    {
        var services = context.RequestServices;
        var sessions = services.GetRequiredService<GameSessionManager>();
        var serializer = services.GetRequiredService<ProtocolSerializer>();
        var dispatcher = services.GetRequiredService<ProtocolDispatcher>();
        var options = services.GetRequiredService<IOptions<GameServerOptions>>().Value;
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Ether.GameServer.Realtime");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var session = sessions.Create(timeProvider);
        if (session is null)
        {
            logger.LogWarning("Connection rejected: maximum concurrent sessions reached");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        logger.LogInformation("Connection accepted {SessionId}", session.SessionId);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, session.Lifetime.Token);
        var cancellationToken = linked.Token;

        var writer = WriteLoopAsync(socket, session, cancellationToken);
        var pump = PumpLoopAsync(session, dispatcher, serializer, logger, cancellationToken);

        try
        {
            await ReceiveLoopAsync(socket, session, serializer, timeProvider, logger, options.MaxMessageSizeBytes, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Session cancelled (timeout, shutdown or client abort).
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug("Socket dropped {SessionId}: {Reason}", session.SessionId, exception.Message);
        }
        finally
        {
            session.MarkDisconnecting();
            session.Commands.Writer.TryComplete();
            session.Outbound.Writer.TryComplete();
            sessions.Remove(session.SessionId);

            try
            {
                await Task.WhenAll(writer, pump).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogDebug("Session loops ended with: {Reason}", exception.Message);
            }

            session.MarkDisconnected();
            logger.LogInformation("Session closed {SessionId}", session.SessionId);
            session.Lifetime.Dispose();
        }
    }

    private static async Task ReceiveLoopAsync(
        WebSocket socket,
        GameSession session,
        ProtocolSerializer serializer,
        TimeProvider timeProvider,
        ILogger logger,
        int maxMessageSizeBytes,
        CancellationToken cancellationToken)
    {
        var responder = new SessionResponder(session, serializer);
        var buffer = new byte[ReceiveBufferSize];
        using var message = new MemoryStream();

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await responder.SendErrorAsync(
                    ProtocolMessageNames.ProtocolError,
                    ProtocolErrorCodes.InvalidEnvelope,
                    "Only text frames are supported.",
                    null,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            message.Write(buffer, 0, result.Count);

            if (message.Length > maxMessageSizeBytes)
            {
                await responder.SendErrorAsync(
                    ProtocolMessageNames.ProtocolError,
                    ProtocolErrorCodes.MessageTooLarge,
                    "Message exceeds the maximum allowed size.",
                    null,
                    cancellationToken).ConfigureAwait(false);

                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);

            var envelope = serializer.TryDeserialize(json, out var failureReason);
            if (envelope is null)
            {
                logger.LogDebug("Protocol error {SessionId}: {Reason}", session.SessionId, failureReason);
                await responder.SendErrorAsync(
                    ProtocolMessageNames.ProtocolError,
                    failureReason ?? ProtocolErrorCodes.InvalidEnvelope,
                    "Malformed message.",
                    null,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            session.Touch(timeProvider.GetUtcNow());

            if (!session.TryAcceptSequence(envelope.Sequence))
            {
                await responder.SendErrorAsync(
                    envelope.Name,
                    ProtocolErrorCodes.InvalidSequence,
                    "Sequence is duplicate or stale.",
                    envelope.RequestId,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!session.Commands.Writer.TryWrite(envelope))
            {
                await responder.SendErrorAsync(
                    envelope.Name,
                    ProtocolErrorCodes.ServerBusy,
                    "Command queue is full.",
                    envelope.RequestId,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task PumpLoopAsync(
        GameSession session,
        ProtocolDispatcher dispatcher,
        ProtocolSerializer serializer,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var responder = new SessionResponder(session, serializer);

        try
        {
            await foreach (var envelope in session.Commands.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var handled = await dispatcher.DispatchAsync(session, envelope, responder, cancellationToken)
                        .ConfigureAwait(false);

                    if (!handled)
                    {
                        await responder.SendErrorAsync(
                            ProtocolMessageNames.ProtocolError,
                            ProtocolErrorCodes.UnknownMessage,
                            $"Unknown message '{envelope.Name}'.",
                            envelope.RequestId,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Command failed {Name} {SessionId}", envelope.Name, session.SessionId);
                    await responder.SendErrorAsync(
                        ProtocolMessageNames.ProtocolError,
                        ProtocolErrorCodes.InternalError,
                        "Internal error.",
                        envelope.RequestId,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session cancelled.
        }
    }

    private static async Task WriteLoopAsync(WebSocket socket, GameSession session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in session.Outbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }

                var bytes = Encoding.UTF8.GetBytes(frame);
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Session cancelled.
        }
        catch (WebSocketException)
        {
            // Client went away.
        }
    }
}
