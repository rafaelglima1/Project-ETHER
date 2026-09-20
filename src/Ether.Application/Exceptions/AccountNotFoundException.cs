using Ether.Domain.Accounts;

namespace Ether.Application.Exceptions;

/// <summary>Raised when an operation references an account that does not exist.</summary>
public sealed class AccountNotFoundException : Exception
{
    public AccountNotFoundException(AccountId accountId)
        : base($"Account '{accountId.Value}' was not found.")
    {
        AccountId = accountId;
    }

    public AccountId AccountId { get; }
}
