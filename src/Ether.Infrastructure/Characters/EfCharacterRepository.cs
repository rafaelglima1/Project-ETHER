using Ether.Application.Abstractions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Characters;

internal sealed class EfCharacterRepository : ICharacterRepository
{
    private readonly EtherDbContext _context;

    public EfCharacterRepository(EtherDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Character character, CancellationToken cancellationToken) =>
        await _context.Characters.AddAsync(character, cancellationToken).ConfigureAwait(false);

    public Task<Character?> GetByIdAsync(CharacterId characterId, CancellationToken cancellationToken) =>
        _context.Characters.FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken);

    public async Task<IReadOnlyList<Character>> GetByAccountIdAsync(
        AccountId accountId,
        CancellationToken cancellationToken) =>
        await _context.Characters
            .Where(character => character.AccountId == accountId)
            .OrderBy(character => character.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
