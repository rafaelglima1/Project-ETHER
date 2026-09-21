using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Handlers;

/// <summary>
/// Server-authoritative realtime movement. The client sends an intent; the
/// server validates and persists the authoritative position.
/// </summary>
public sealed class MovementCommandHandler : IProtocolCommandHandler
{
    private readonly MoveCharacterHandler _move;
    private readonly ProtocolSerializer _serializer;
    private readonly GameServerOptions _options;
    private readonly TimeProvider _timeProvider;

    public MovementCommandHandler(
        MoveCharacterHandler move,
        ProtocolSerializer serializer,
        IOptions<GameServerOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _move = move;
        _serializer = serializer;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.MovementMove;

    public async Task HandleAsync(
        GameSession session,
        ProtocolEnvelope envelope,
        IProtocolResponder responder,
        CancellationToken cancellationToken)
    {
        if (session.AccountId is null || session.CharacterId is null)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotAuthenticated, "Session is not authenticated.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.State != GameSessionState.InWorld)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotInWorld, "Character is not in the world.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (!session.TryAcquireRateSlot(now.ToUnixTimeSeconds(), _options.MaxMovementCommandsPerSecond))
        {
            await Reject(responder, envelope, ProtocolErrorCodes.RateLimited, "Too many movement commands.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var payload = _serializer.DeserializePayload<MovementMoveCommandPayload>(envelope.Payload);
        if (payload is null)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidPayload, "Movement payload is invalid.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CharacterResponse character;
        try
        {
            character = await _move
                .HandleAsync(session.AccountId.Value, session.CharacterId.Value, new MoveCharacterRequest(payload.X, payload.Y), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MovementOutOfBoundsException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.OutOfBounds, "Destination is outside the map.", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (MovementTooFarException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.TooFar, "Destination is too far.", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (CharacterNotInWorldException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotInWorld, "Character is not in the world.", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (DomainException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Movement rejected.", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (CharacterNotFoundException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Character was not found.", cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (ForbiddenException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotAuthorized, "Character does not belong to this session.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await responder.SendEventAsync(
            ProtocolMessageNames.MovementAccepted,
            new MovementAcceptedEventPayload(character.CharacterId, character.MapId, character.PositionX, character.PositionY),
            envelope.RequestId,
            cancellationToken).ConfigureAwait(false);
    }

    private static Task Reject(
        IProtocolResponder responder,
        ProtocolEnvelope envelope,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        responder.SendErrorAsync(
            ProtocolMessageNames.MovementRejected,
            code,
            message,
            envelope.RequestId,
            cancellationToken);
}
