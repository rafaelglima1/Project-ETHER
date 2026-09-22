using Ether.Application.Abstractions;
using Ether.Domain.Characters;
using Ether.Domain.Items;
using Ether.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Items;

internal sealed class EfItemInstanceRepository : IItemInstanceRepository
{
    private readonly EtherDbContext _context;

    public EfItemInstanceRepository(EtherDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ItemInstance item, CancellationToken cancellationToken) =>
        await _context.ItemInstances.AddAsync(item, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ItemInstance>> GetByOwnerAsync(
        CharacterId characterId,
        CancellationToken cancellationToken) =>
        await _context.ItemInstances
            .Where(item => item.OwnerCharacterId == characterId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
