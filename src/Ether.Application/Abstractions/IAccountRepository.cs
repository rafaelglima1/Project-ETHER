using Ether.Domain.Accounts;

namespace Ether.Application.Abstractions;

/// <summary>Persistence abstraction for the <see cref="Account"/> aggregate.</summary>
public interface IAccountRepository
{
    Task AddAsync(Account account, CancellationToken cancellationToken);

    Task<Account?> GetByIdAsync(AccountId accountId, CancellationToken cancellationToken);
}
