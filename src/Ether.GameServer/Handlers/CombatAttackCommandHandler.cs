using Ether.Application.Combat;
using Ether.Application.Exceptions;
using Ether.Contracts.Combat;
using Ether.Contracts.Configuration;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Handlers;

/// <summary>
/// Realtime combat: the client requests an attack; the server resolves everything.
/// No damage/critical/armor value is accepted from the client.
/// </summary>
public sealed class CombatAttackCommandHandler : IProtocolCommandHandler
{
    private readonly AttackCommandHandler _attack;
    private readonly AttackCreatureCommandHandler _attackCreature;
    private readonly ProtocolSerializer _serializer;
    private readonly CombatOptions _options;
    private readonly TimeProvider _timeProvider;

    public CombatAttackCommandHandler(
        AttackCommandHandler attack,
        AttackCreatureCommandHandler attackCreature,
        ProtocolSerializer serializer,
        IOptions<CombatOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _attack = attack;
        _attackCreature = attackCreature;
        _serializer = serializer;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.CombatAttack;

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
        if (!session.TryAcquireRateSlot("combat", now.ToUnixTimeSeconds(), _options.MaxAttacksPerSecond))
        {
            await Reject(responder, envelope, ProtocolErrorCodes.RateLimited, "Too many attack commands.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var payload = _serializer.DeserializePayload<AttackCommandPayload>(envelope.Payload);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AbilityId))
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidPayload, "Attack payload is invalid.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        AbilityId abilityId;
        try
        {
            abilityId = new AbilityId(payload.AbilityId);
        }
        catch (Ether.Domain.Common.DomainException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.AbilityNotFound, "Ability id is invalid.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (payload.TargetId == Guid.Empty)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidPayload, "targetId is required.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var isCreatureTarget = string.Equals(payload.TargetType, "creature", StringComparison.OrdinalIgnoreCase);

        try
        {
            CombatResultResponse result;

            if (isCreatureTarget)
            {
                result = await _attackCreature
                    .HandleAsync(session.AccountId.Value, session.CharacterId.Value, abilityId, new CreatureInstanceId(payload.TargetId), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await _attack
                    .HandleAsync(session.AccountId.Value, session.CharacterId.Value, abilityId, new CharacterId(payload.TargetId), cancellationToken)
                    .ConfigureAwait(false);
            }

            await responder.SendEventAsync(ProtocolMessageNames.CombatResult, result, envelope.RequestId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CombatRejectedException rejection)
        {
            await Reject(responder, envelope, MapReason(rejection.Reason), rejection.Message, cancellationToken).ConfigureAwait(false);
        }
        catch (ForbiddenException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.NotAuthorized, "Character does not belong to this session.", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CharacterNotFoundException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Character was not found.", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Ether.Domain.Common.DomainException)
        {
            await Reject(responder, envelope, ProtocolErrorCodes.InvalidState, "Attack rejected.", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string MapReason(CombatRejectionReason reason) => reason switch
    {
        CombatRejectionReason.AbilityNotFound => ProtocolErrorCodes.AbilityNotFound,
        CombatRejectionReason.TargetNotFound => ProtocolErrorCodes.TargetNotFound,
        CombatRejectionReason.TargetDead => ProtocolErrorCodes.TargetDead,
        CombatRejectionReason.AttackerDead => ProtocolErrorCodes.AttackerDead,
        CombatRejectionReason.OutOfRange => ProtocolErrorCodes.OutOfRange,
        CombatRejectionReason.CooldownActive => ProtocolErrorCodes.CooldownActive,
        CombatRejectionReason.SelfTarget => ProtocolErrorCodes.SelfTarget,
        _ => ProtocolErrorCodes.InvalidState,
    };

    private static Task Reject(
        IProtocolResponder responder,
        ProtocolEnvelope envelope,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        responder.SendErrorAsync(
            ProtocolMessageNames.CombatRejected,
            code,
            message,
            envelope.RequestId,
            cancellationToken);
}
