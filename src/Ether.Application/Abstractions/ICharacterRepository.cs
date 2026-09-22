using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.World;

namespace Ether.Application.Abstractions;

/// <summary>Persistence abstraction for the <see cref="Character"/> aggregate.</summary>
public interface ICharacterRepository
{
    Task AddAsync(Character character, CancellationToken cancellationToken);

    Task<Character?> GetByIdAsync(CharacterId characterId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Character>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>Characters currently located on a map (used by creature AI).</summary>
    Task<IReadOnlyList<Character>> GetByMapAsync(MapId mapId, CancellationToken cancellationToken);
}
