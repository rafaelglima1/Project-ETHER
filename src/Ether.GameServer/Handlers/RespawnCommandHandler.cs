using Ether.Application.Abstractions;
using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.GameServer.Protocol;
using Ether.GameServer.Realtime;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Handlers;

/// <summary>
/// Respawns the session's dead character and returns a fresh world snapshot.
/// The server decides death, spawn point and restored health.
/// </summary>
public sealed class RespawnCommandHandler : IProtocolCommandHandler
{
    private readonly RespawnCharacterHandler _respawn;
    private readonly WorldSnapshotFactory _snapshots;
    private readonly TimeProvider _timeProvider;

    public RespawnCommandHandler(
        RespawnCharacterHandler respawn,
        WorldSnapshotFactory snapshots,
        TimeProvider timeProvider)
    {
        _respawn = respawn;
        _snapshots = snapshots;
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.CharacterRespawn;

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

        CharacterResponse character;
        try
        {
            character = await _respawn
                .HandleAsync(session.AccountId.Value, session.CharacterId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CharacterNotDeadException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.CharacterNotDead, "Character is not dead.", cancellationToken).ConfigureAwait(false);
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
        catch (DomainException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Respawn rejected.", cancellationToken).ConfigureAwait(false);
            return;
        }

        session.MarkInWorld(_timeProvider.GetUtcNow());

        await responder.SendEventAsync(
            ProtocolMessageNames.WorldSnapshot,
            _snapshots.Build(character),
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
            ProtocolMessageNames.CharacterRespawnRejected,
            code,
            message,
            envelope.RequestId,
            cancellationToken);
}
