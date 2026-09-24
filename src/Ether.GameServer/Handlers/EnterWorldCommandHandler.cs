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

/// <summary>Puts the session's character into the world and returns a snapshot.</summary>
public sealed class EnterWorldCommandHandler : IProtocolCommandHandler
{
    private readonly EnterWorldHandler _enterWorld;
    private readonly WorldSnapshotFactory _snapshots;
    private readonly TimeProvider _timeProvider;

    public EnterWorldCommandHandler(
        EnterWorldHandler enterWorld,
        WorldSnapshotFactory snapshots,
        TimeProvider timeProvider)
    {
        _enterWorld = enterWorld;
        _snapshots = snapshots;
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.WorldEnter;

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

        // Check character state BEFORE session state: a dead character must get
        // CHARACTER_DEAD (so the client knows to respawn), not ALREADY_IN_WORLD.
        CharacterResponse character;
        try
        {
            character = await _enterWorld
                .HandleAsync(session.AccountId.Value, session.CharacterId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CharacterDeadException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.CharacterDead, "Character is dead; respawn first.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        catch (CharacterNotFoundException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Character was not found.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        catch (ForbiddenException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotAuthorized, "Character does not belong to this session.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        catch (DomainException)
        {
            // Expected gameplay states must never surface as INTERNAL_ERROR.
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Character cannot enter the world.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.State == GameSessionState.InWorld)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.AlreadyInWorld, "Character is already in the world.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        session.MarkInWorld(_timeProvider.GetUtcNow());

        await responder.SendEventAsync(
            ProtocolMessageNames.WorldSnapshot,
            await _snapshots.BuildAsync(character, cancellationToken).ConfigureAwait(false),
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
            ProtocolMessageNames.WorldEnterRejected,
            code,
            message,
            envelope.RequestId,
            cancellationToken);
}
