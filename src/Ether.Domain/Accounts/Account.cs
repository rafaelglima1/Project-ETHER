using Ether.Domain.Common;

namespace Ether.Domain.Accounts;

/// <summary>
/// Account aggregate root (Blueprint v2.0 §9 / v5.0 §9).
/// M1 keeps only the fields required for the persistent foundation:
/// identity, timestamps and status. Authentication/identity-provider fields
/// belong to a later milestone and are intentionally not modelled here.
/// </summary>
public sealed class Account
{
    private Account()
    {
        // Required by the persistence layer.
    }

    private Account(AccountId id, AccountStatus status, DateTimeOffset nowUtc)
    {
        Id = id;
        Status = status;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public AccountId Id { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a new active account.</summary>
    public static Account Create(DateTimeOffset nowUtc) =>
        new(AccountId.New(), AccountStatus.Active, nowUtc);

    public void Suspend(DateTimeOffset nowUtc)
    {
        if (Status == AccountStatus.Suspended)
        {
            throw new DomainException("Account is already suspended.");
        }

        Status = AccountStatus.Suspended;
        UpdatedAt = nowUtc;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        if (Status == AccountStatus.Active)
        {
            throw new DomainException("Account is already active.");
        }

        Status = AccountStatus.Active;
        UpdatedAt = nowUtc;
    }
}
