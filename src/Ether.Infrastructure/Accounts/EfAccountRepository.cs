using Ether.Application.Abstractions;
using Ether.Domain.Accounts;
using Ether.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Accounts;

internal sealed class EfAccountRepository : IAccountRepository
{
    private readonly EtherDbContext _context;

    public EfAccountRepository(EtherDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await _context.Accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);

    public Task<Account?> GetByIdAsync(AccountId accountId, CancellationToken cancellationToken) =>
        _context.Accounts.FirstOrDefaultAsync(account => account.Id == accountId, cancellationToken);
}
