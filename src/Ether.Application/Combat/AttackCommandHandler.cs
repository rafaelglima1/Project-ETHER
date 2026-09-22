using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Combat;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Combat;

using Microsoft.Extensions.Options;

namespace Ether.Application.Combat;

/// <summary>
/// Use case: resolve a server-authoritative attack.
/// The client only expresses intent (target + ability); ownership, state, range,
/// cooldown, damage, criticals and HP are all decided here and in the domain.
/// </summary>
public sealed class AttackCommandHandler
{
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICombatStatsProvider _stats;
    private readonly IAbilityCooldownStore _cooldowns;
    private readonly IEntityLockProvider _locks;
    private readonly IRandomSource _random;
    private readonly CombatOptions _options;
    private readonly TimeProvider _timeProvider;

    public AttackCommandHandler(
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        ICombatStatsProvider stats,
        IAbilityCooldownStore cooldowns,
        IEntityLockProvider locks,
        IRandomSource random,
        IOptions<CombatOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _characters = characters;
        _unitOfWork = unitOfWork;
        _stats = stats;
        _cooldowns = cooldowns;
        _locks = locks;
        _random = random;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CombatResultResponse> HandleAsync(
        AccountId authenticatedAccountId,
        CharacterId attackerId,
        AbilityId abilityId,
        CharacterId targetId,
        CancellationToken cancellationToken)
    {
        if (!AbilityCatalog.TryGet(abilityId, out var ability))
        {
            throw new CombatRejectedException(CombatRejectionReason.AbilityNotFound, $"Ability '{abilityId.Value}' does not exist.");
        }

        if (attackerId == targetId)
        {
            throw new CombatRejectedException(CombatRejectionReason.SelfTarget, "A character cannot target itself.");
        }

        var lockIds = new List<Guid> { attackerId.Value, targetId.Value };
        await using var handle = await _locks.AcquireAsync(lockIds, cancellationToken).ConfigureAwait(false);

        var attacker = await _characters.GetByIdAsync(attackerId, cancellationToken).ConfigureAwait(false)
                       ?? throw new CharacterNotFoundException(attackerId);

        if (attacker.AccountId != authenticatedAccountId)
        {
            throw new ForbiddenException("Character does not belong to the authenticated account.");
        }

        if (!attacker.IsAlive)
        {
            throw new CombatRejectedException(CombatRejectionReason.AttackerDead, "Attacker is dead.");
        }

        if (attacker.State is not (CharacterState.InWorld or CharacterState.Combat))
        {
            throw new CombatRejectedException(CombatRejectionReason.InvalidState, "Attacker is not in the world.");
        }

        var target = await _characters.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false)
                     ?? throw new CombatRejectedException(CombatRejectionReason.TargetNotFound, "Target does not exist.");

        if (target.MapId != attacker.MapId)
        {
            throw new CombatRejectedException(CombatRejectionReason.OutOfRange, "Target is on a different map.");
        }

        if (!target.IsAlive)
        {
            throw new CombatRejectedException(CombatRejectionReason.TargetDead, "Target is dead.");
        }

        if (target.State is not (CharacterState.InWorld or CharacterState.Combat))
        {
            throw new CombatRejectedException(CombatRejectionReason.InvalidState, "Target is not attackable.");
        }

        var distance = Math.Max(Math.Abs(target.PositionX - attacker.PositionX), Math.Abs(target.PositionY - attacker.PositionY));
        if (distance > ability.Range)
        {
            throw new CombatRejectedException(CombatRejectionReason.OutOfRange, $"Target is out of range ({distance} > {ability.Range}).");
        }

        var now = _timeProvider.GetUtcNow();
        if (!_cooldowns.IsReady(attackerId.Value, ability.Id, now))
        {
            throw new CombatRejectedException(
                CombatRejectionReason.CooldownActive,
                $"Ability '{ability.Id.Value}' is on cooldown for {_cooldowns.Remaining(attackerId.Value, ability.Id, now).TotalSeconds:0.#}s.");
        }

        _cooldowns.Record(attackerId.Value, ability.Id, ability.Cooldown, now);

        attacker.EnterCombat(now);
        target.EnterCombat(now);

        var rules = new DamageRules(_options.MinimumDamage, _options.ResistanceCap);
        var damage = DamageCalculator.Calculate(_stats.For(attacker), _stats.For(target), ability, _random.NextUnit(), rules);

        var defeated = target.ApplyDamage(damage.Damage, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CombatResultResponse(
            attackerId.Value,
            targetId.Value,
            ability.Id.Value,
            damage.RawDamage,
            damage.Damage,
            damage.Critical,
            target.Health,
            target.MaxHealth,
            target.State.ToString(),
            defeated);
    }
}
