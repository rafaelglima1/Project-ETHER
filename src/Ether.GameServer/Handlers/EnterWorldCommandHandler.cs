using Ether.Application.Abstractions;
using Ether.Application.Characters;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Handlers;

/// <summary>Puts the session's character into the world and returns a snapshot.</summary>
public sealed class EnterWorldCommandHandler : IProtocolCommandHandler
{
    private readonly EnterWorldHandler _enterWorld;
    private readonly IWorldMapProvider _maps;
    private readonly ProtocolSerializer _serializer;
    private readonly TimeProvider _timeProvider;

    public EnterWorldCommandHandler(
        EnterWorldHandler enterWorld,
        IWorldMapProvider maps,
        ProtocolSerializer serializer,
        TimeProvider timeProvider)
    {
        _enterWorld = enterWorld;
        _maps = maps;
        _serializer = serializer;
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
            await responder.SendErrorAsync(
                ProtocolMessageNames.WorldEnterRejected,
                ProtocolErrorCodes.NotAuthenticated,
                "Session is not authenticated.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (session.State == GameSessionState.InWorld)
        {
            await responder.SendErrorAsync(
                ProtocolMessageNames.WorldEnterRejected,
                ProtocolErrorCodes.AlreadyInWorld,
                "Character is already in the world.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        CharacterResponse character;
        try
        {
            character = await _enterWorld
                .HandleAsync(session.AccountId.Value, session.CharacterId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CharacterNotFoundException)
        {
            await responder.SendErrorAsync(
                ProtocolMessageNames.WorldEnterRejected,
                ProtocolErrorCodes.InvalidState,
                "Character was not found.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (ForbiddenException)
        {
            await responder.SendErrorAsync(
                ProtocolMessageNames.WorldEnterRejected,
                ProtocolErrorCodes.NotAuthorized,
                "Character does not belong to this session.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var map = _maps.GetMap(new Ether.Domain.World.MapId(character.MapId));
        session.MarkInWorld(_timeProvider.GetUtcNow());

        var snapshot = new WorldSnapshotPayload(
            character.MapId,
            map.Width,
            map.Height,
            new PlayerSnapshotPayload(character.CharacterId, character.PositionX, character.PositionY, character.State),
            _timeProvider.GetUtcNow());

        await responder.SendEventAsync(
            ProtocolMessageNames.WorldSnapshot,
            snapshot,
            envelope.RequestId,
            cancellationToken).ConfigureAwait(false);
    }
}
