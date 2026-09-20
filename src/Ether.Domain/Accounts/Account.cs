using Ether.Domain.Common;

namespace Ether.Domain.Accounts;

/// <summary>
/// Account aggregate root (Blueprint v2.0 §9 / v5.0 §9).
/// M2 adds the identity credentials required to establish a session.
/// </summary>
public sealed class Account
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    private Account()
    {
        // Required by the persistence layer.
    }

    private Account(AccountId id, Email email, PasswordHash passwordHash, AccountStatus status, DateTimeOffset nowUtc)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Status = status;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public AccountId Id { get; private set; }

    public Email Email { get; private set; }

    public PasswordHash PasswordHash { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a new active account with credentials.</summary>
    public static Account Create(Email email, PasswordHash passwordHash, DateTimeOffset nowUtc)
    {
        if (email.IsEmpty)
        {
            throw new DomainException("Account email is required.");
        }

        if (passwordHash.IsEmpty)
        {
            throw new DomainException("Account password hash is required.");
        }

        return new Account(AccountId.New(), email, passwordHash, AccountStatus.Active, nowUtc);
    }

    /// <summary>Replaces the stored password hash.</summary>
    public void ChangePassword(PasswordHash passwordHash, DateTimeOffset nowUtc)
    {
        if (passwordHash.IsEmpty)
        {
            throw new DomainException("Account password hash is required.");
        }

        PasswordHash = passwordHash;
        UpdatedAt = nowUtc;
    }

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

    /// <summary>Ensures the account is allowed to establish new sessions.</summary>
    public void EnsureCanAuthenticate()
    {
        if (Status != AccountStatus.Active)
        {
            throw new DomainException("Account is not allowed to authenticate.");
        }
    }
}
