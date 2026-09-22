using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Combat;

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
    public Task<IAsyncDisposable> AcquireAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable>(new Noop());

    private sealed class Noop : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
