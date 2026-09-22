using System.Collections.Concurrent;

using Ether.Application.Abstractions;
using Ether.Domain.Combat;

namespace Ether.Infrastructure.Memory;

/// <summary>
/// Process-local cooldown store (M5). Character cooldowns do not need to survive a
/// restart; a distributed store (Redis) would replace this if the world shards.
/// </summary>
public sealed class InMemoryAbilityCooldownStore : IAbilityCooldownStore
{
    private readonly ConcurrentDictionary<(Guid CharacterId, string AbilityId), DateTimeOffset> _readyAt = new();

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
            _readyAt.TryRemove((characterId, abilityId.Value), out _);
            return;
        }

        _readyAt[(characterId, abilityId.Value)] = now + cooldown;
    }
}
