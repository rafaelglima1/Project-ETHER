using Ether.Domain.Characters;
using Ether.Domain.Items;

namespace Ether.Application.Abstractions;

/// <summary>Persistence abstraction for owned item instances.</summary>
public interface IItemInstanceRepository
{
    Task AddAsync(ItemInstance item, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemInstance>> GetByOwnerAsync(CharacterId characterId, CancellationToken cancellationToken);
}
