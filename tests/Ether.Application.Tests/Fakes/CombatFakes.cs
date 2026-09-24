using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Combat;
using Ether.Domain.Items;

namespace Ether.Application.Tests.Fakes;

/// <summary>Fixed combat stats for deterministic handler tests.</summary>
internal sealed class FakeCombatStatsProvider : ICombatStatsProvider
{
    public CombatStats Stats { get; set; } = CombatStats.Neutral;

    public CombatStats For(Character character) => Stats;
}

/// <summary>Deterministic randomness for critical-hit tests.</summary>
internal sealed class FixedRandomSource : IRandomSource
{
    public double Value { get; set; }

    public double NextUnit() => Value;
}

/// <summary>In-memory cooldown tracking mirroring the production semantics.</summary>
internal sealed class FakeCooldownStore : IAbilityCooldownStore
{
    private readonly Dictionary<(Guid, string), DateTimeOffset> _readyAt = [];

    public bool IsReady(Guid characterId, AbilityId abilityId, DateTimeOffset now) =>
        Remaining(characterId, abilityId, now) <= TimeSpan.Zero;

    public TimeSpan Remaining(Guid characterId, AbilityId abilityId, DateTimeOffset now)
    {
        if (!_readyAt.TryGetValue((characterId, abilityId.Value), out var readyAt))
        {
            return TimeSpan.Zero;
        }

        var remaining = readyAt - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public void Record(Guid characterId, AbilityId abilityId, TimeSpan cooldown, DateTimeOffset now)
    {
        if (cooldown <= TimeSpan.Zero)
        {
            _readyAt.Remove((characterId, abilityId.Value));
            return;
        }

        _readyAt[(characterId, abilityId.Value)] = now + cooldown;
    }
}

/// <summary>No-op lock provider for single-threaded handler tests.</summary>
internal sealed class NoopEntityLockProvider : IEntityLockProvider
{
    public Task<IEntityLockLease> AcquireAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken) =>
        Task.FromResult<IEntityLockLease>(new Noop(entityIds));

    private sealed class Noop : IEntityLockLease
    {
        private readonly HashSet<Guid> _entityIds;

        public Noop(IEnumerable<Guid> entityIds) => _entityIds = entityIds.ToHashSet();

        public bool Covers(Guid entityId) => _entityIds.Contains(entityId);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>In-memory owned items for application tests.</summary>
internal sealed class FakeItemInstanceRepository : IItemInstanceRepository
{
    private readonly List<ItemInstance> _items = [];

    public IReadOnlyList<ItemInstance> All => _items;

    public Task AddAsync(ItemInstance item, CancellationToken cancellationToken)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ItemInstance>> GetByOwnerAsync(
        Ether.Domain.Characters.CharacterId characterId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemInstance>>(
            _items.Where(item => item.OwnerCharacterId == characterId).ToList());
}
