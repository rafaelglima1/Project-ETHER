using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Application.Progression;
using Ether.Contracts.Combat;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;

using Microsoft.Extensions.Options;

namespace Ether.Application.Combat;

/// <summary>
/// Use case: resolve a server-authoritative attack against a creature. Reuses the
/// M5 damage pipeline, cooldown store, entity locks and stats provider.
/// </summary>
public sealed class AttackCreatureCommandHandler
{
    private readonly ICharacterRepository _characters;
    private readonly ICreatureWorld _creatures;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICombatStatsProvider _stats;
    private readonly IAbilityCooldownStore _cooldowns;
    private readonly IEntityLockProvider _locks;
    private readonly IRandomSource _random;
    private readonly KillRewardService _rewards;
    private readonly CombatOptions _options;
    private readonly TimeProvider _timeProvider;

    public AttackCreatureCommandHandler(
        ICharacterRepository characters,
        ICreatureWorld creatures,
        IUnitOfWork unitOfWork,
        ICombatStatsProvider stats,
        IAbilityCooldownStore cooldowns,
        IEntityLockProvider locks,
        IRandomSource random,
        KillRewardService rewards,
        IOptions<CombatOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _characters = characters;
        _creatures = creatures;
        _unitOfWork = unitOfWork;
        _stats = stats;
        _cooldowns = cooldowns;
        _locks = locks;
        _random = random;
        _rewards = rewards;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CombatResultResponse> HandleAsync(
        AccountId authenticatedAccountId,
        CharacterId attackerId,
        AbilityId abilityId,
        CreatureInstanceId targetId,
        CancellationToken cancellationToken)
    {
        if (!AbilityCatalog.TryGet(abilityId, out var ability))
        {
            throw new Exceptions.CombatRejectedException(
                Exceptions.CombatRejectionReason.AbilityNotFound,
                $"Ability '{abilityId.Value}' does not exist.");
        }

        await using var handle = await _locks
            .AcquireAsync([attackerId.Value, targetId.Value], cancellationToken)
            .ConfigureAwait(false);

        var attacker = await _characters.GetByIdAsync(attackerId, cancellationToken).ConfigureAwait(false)
                       ?? throw new CharacterNotFoundException(attackerId);

        if (attacker.AccountId != authenticatedAccountId)
        {
            throw new ForbiddenException("Character does not belong to the authenticated account.");
        }

        if (!attacker.IsAlive)
        {
            throw new Exceptions.CombatRejectedException(Exceptions.CombatRejectionReason.AttackerDead, "Attacker is dead.");
        }

        if (attacker.State is not (CharacterState.InWorld or CharacterState.Combat))
        {
            throw new Exceptions.CombatRejectedException(Exceptions.CombatRejectionReason.InvalidState, "Attacker is not in the world.");
        }

        var creature = _creatures.Get(targetId)
                       ?? throw new Exceptions.CombatRejectedException(Exceptions.CombatRejectionReason.TargetNotFound, "Creature does not exist.");

        if (creature.MapId != attacker.MapId)
        {
            throw new Exceptions.CombatRejectedException(Exceptions.CombatRejectionReason.OutOfRange, "Creature is on a different map.");
        }

        if (!creature.IsAlive)
        {
            throw new Exceptions.CombatRejectedException(Exceptions.CombatRejectionReason.TargetDead, "Creature is dead.");
        }

        var distance = Math.Max(Math.Abs(creature.PositionX - attacker.PositionX), Math.Abs(creature.PositionY - attacker.PositionY));
        if (distance > ability.Range)
        {
            throw new Exceptions.CombatRejectedException(
                Exceptions.CombatRejectionReason.OutOfRange,
                $"Creature is out of range ({distance} > {ability.Range}).");
        }

        var now = _timeProvider.GetUtcNow();
        if (!_cooldowns.IsReady(attackerId.Value, ability.Id, now))
        {
            throw new Exceptions.CombatRejectedException(
                Exceptions.CombatRejectionReason.CooldownActive,
                $"Ability '{ability.Id.Value}' is on cooldown.");
        }

        _cooldowns.Record(attackerId.Value, ability.Id, ability.Cooldown, now);
        attacker.EnterCombat(now);

        var definition = CreatureCatalog.Get(creature.DefinitionId);
        var rules = new DamageRules(_options.MinimumDamage, _options.ResistanceCap);
        var damage = DamageCalculator.Calculate(
            _stats.For(attacker),
            definition.DefenseStats(),
            ability,
            _random.NextUnit(),
            rules);

        var defeated = creature.ApplyDamage(damage.Damage, now, definition.RespawnDelay);

        // Rewards are granted exactly once, on the lethal transition.
        KillRewardResult? reward = null;
        if (defeated)
        {
            reward = await _rewards.GrantAsync(attacker, definition, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CombatResultResponse(
            attackerId.Value,
            targetId.Value,
            ability.Id.Value,
            damage.RawDamage,
            damage.Damage,
            damage.Critical,
            creature.Health,
            creature.MaxHealth,
            creature.State.ToString(),
            defeated,
            AttackerType: "character",
            TargetType: "creature",
            ExperienceGained: reward?.ExperienceGained ?? 0,
            Level: reward?.Level ?? attacker.Level,
            Experience: reward?.Experience ?? attacker.Experience,
            LevelsGained: reward?.LevelsGained ?? 0,
            Loot: reward is null || reward.Items.Count == 0
                ? null
                : reward.Items
                    .Select(item => new LootItemPayload(
                        item.DefinitionId.Value,
                        ItemCatalog.Get(item.DefinitionId).Name,
                        item.Quantity,
                        item.Id.Value))
                    .ToList());
    }
}
