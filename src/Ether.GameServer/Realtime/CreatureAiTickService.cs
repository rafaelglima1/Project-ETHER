using Ether.Application.Abstractions;
using Ether.Application.Creatures;
using Ether.Contracts.Combat;
using Ether.Contracts.Configuration;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.World;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ether.GameServer.Realtime;

/// <summary>
/// Runs the creature AI at the configured rate and broadcasts the results to the
/// clients on the map. AI rules live in Application/Domain; this service only
/// schedules ticks and translates outcomes into protocol messages.
/// </summary>
internal sealed class CreatureAiTickService : BackgroundService
{
    private readonly GameSessionManager _sessions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProtocolSerializer _serializer;
    private readonly GameServerOptions _options;
    private readonly CharacterOptions _characterOptions;
    private readonly ILogger<CreatureAiTickService> _logger;

    public CreatureAiTickService(
        GameSessionManager sessions,
        IServiceScopeFactory scopeFactory,
        ProtocolSerializer serializer,
        IOptions<GameServerOptions> options,
        IOptions<CharacterOptions> characterOptions,
        ILogger<CreatureAiTickService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(characterOptions);

        _sessions = sessions;
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _options = options.Value;
        _characterOptions = characterOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(50, 1000 / Math.Max(1, _options.AiTickRateHz)));
        var mapId = new MapId(_characterOptions.StartingMapId);

        _logger.LogInformation("Creature AI tick started at {Hz} Hz for map {MapId}", _options.AiTickRateHz, mapId.Value);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var tick = scope.ServiceProvider.GetRequiredService<CreatureAiTickHandler>();
                var result = await tick.HandleAsync(mapId, stoppingToken).ConfigureAwait(false);

                if (result.Moves.Count > 0 || result.Attacks.Count > 0)
                {
                    var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
                    var onMap = await characters.GetByMapAsync(mapId, stoppingToken).ConfigureAwait(false);

                    BroadcastMoves(result.Moves, onMap.Select(character => character.Id.Value).ToHashSet());
                    BroadcastAttacks(result.Attacks);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Creature AI tick failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void BroadcastMoves(IReadOnlyList<CreatureMovedEvent> moves, HashSet<Guid> charactersOnMap)
    {
        foreach (var session in _sessions.Snapshot())
        {
            if (session.State != GameSessionState.InWorld ||
                session.CharacterId is not { } characterId ||
                !charactersOnMap.Contains(characterId.Value))
            {
                continue;
            }

            foreach (var move in moves)
            {
                Send(
                    session,
                    ProtocolMessageNames.WorldCreatureMoved,
                    new WorldCreatureMovedEventPayload(
                        move.CreatureId, move.MapId, move.X, move.Y, move.Health, move.MaxHealth, move.State));
            }
        }
    }

    private void BroadcastAttacks(IReadOnlyList<CreatureAttackedCharacterEvent> attacks)
    {
        foreach (var attack in attacks)
        {
            var session = _sessions.FindByCharacter(new CharacterId(attack.TargetCharacterId));
            if (session is null)
            {
                continue;
            }

            Send(
                session,
                ProtocolMessageNames.CombatResult,
                new CombatResultResponse(
                    attack.CreatureId,
                    attack.TargetCharacterId,
                    attack.AbilityId,
                    attack.RawDamage,
                    attack.Damage,
                    attack.Critical,
                    attack.TargetHealth,
                    attack.TargetMaxHealth,
                    attack.TargetState,
                    attack.TargetDefeated,
                    AttackerType: "creature",
                    TargetType: "character"));
        }
    }

    private void Send(GameSession session, string name, object payload)
    {
        var envelope = _serializer.Build(
            ProtocolMessageTypes.Event,
            name,
            payload,
            requestId: null,
            session.NextOutboundSequence());

        session.Outbound.Writer.TryWrite(_serializer.Serialize(envelope));
    }
}
