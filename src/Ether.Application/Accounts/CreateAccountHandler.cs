using Ether.Application.Abstractions;
using Ether.Contracts.Accounts;
using Ether.Domain.Accounts;

namespace Ether.Application.Accounts;

/// <summary>Use case: create a new account.</summary>
public sealed class CreateAccountHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateAccountHandler(
        IAccountRepository accounts,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CreateAccountResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var account = Account.Create(_timeProvider.GetUtcNow());

        await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateAccountResponse(account.Id.Value, account.Status.ToString(), account.CreatedAt);
    }
}
