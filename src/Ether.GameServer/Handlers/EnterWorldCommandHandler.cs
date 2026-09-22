using Ether.Application.Abstractions;
using Ether.Application.Characters;
using Ether.Application.Creatures;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.Creatures;
using Ether.Domain.World;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Handlers;

/// <summary>Puts the session's character into the world and returns a snapshot.</summary>
public sealed class EnterWorldCommandHandler : IProtocolCommandHandler
{
    private readonly EnterWorldHandler _enterWorld;
    private readonly IWorldMapProvider _maps;
    private readonly ICreatureWorld _creatures;
    private readonly CreatureSpawnService _spawner;
    private readonly ProtocolSerializer _serializer;
    private readonly TimeProvider _timeProvider;

    public EnterWorldCommandHandler(
        EnterWorldHandler enterWorld,
        IWorldMapProvider maps,
        ICreatureWorld creatures,
        CreatureSpawnService spawner,
        ProtocolSerializer serializer,
        TimeProvider timeProvider)
    {
        _enterWorld = enterWorld;
        _maps = maps;
        _creatures = creatures;
        _spawner = spawner;
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

        var mapId = new MapId(character.MapId);
        var map = _maps.GetMap(mapId);

        // Make sure the map's creatures exist, then snapshot the living ones.
        _spawner.EnsureSpawned(mapId);
        var creatures = _creatures.GetByMap(mapId)
            .Where(creature => creature.IsAlive)
            .Select(ToSnapshot)
            .ToList();

        session.MarkInWorld(_timeProvider.GetUtcNow());

        var snapshot = new WorldSnapshotPayload(
            character.MapId,
            map.Width,
            map.Height,
            new PlayerSnapshotPayload(
                character.CharacterId,
                character.PositionX,
                character.PositionY,
                character.State,
                character.Level,
                character.Experience,
                character.ExperienceToNextLevel,
                character.Health,
                character.MaxHealth),
            _timeProvider.GetUtcNow(),
            creatures);

        await responder.SendEventAsync(
            ProtocolMessageNames.WorldSnapshot,
            snapshot,
            envelope.RequestId,
            cancellationToken).ConfigureAwait(false);
    }

    private static CreatureSnapshotPayload ToSnapshot(CreatureInstance creature)
    {
        var definition = CreatureCatalog.Get(creature.DefinitionId);

        return new CreatureSnapshotPayload(
            creature.Id.Value,
            creature.DefinitionId.Value,
            definition.Name,
            creature.PositionX,
            creature.PositionY,
            creature.Health,
            creature.MaxHealth,
            creature.State.ToString());
    }
}
