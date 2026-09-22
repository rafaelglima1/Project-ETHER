using Ether.Domain.Combat;

namespace Ether.Application.Abstractions;

/// <summary>
/// Tracks ability cooldowns. The server owns cooldown state; the client only
/// presents it. M5 uses a process-local store (a distributed store would live in
/// Redis when the world scales horizontally).
/// </summary>
public interface IAbilityCooldownStore
{
    bool IsReady(Guid characterId, AbilityId abilityId, DateTimeOffset now);

    TimeSpan Remaining(Guid characterId, AbilityId abilityId, DateTimeOffset now);

    void Record(Guid characterId, AbilityId abilityId, TimeSpan cooldown, DateTimeOffset now);
}
