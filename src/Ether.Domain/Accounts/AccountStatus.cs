namespace Ether.Domain.Accounts;

/// <summary>
/// Lifecycle status of an account.
/// Only the states needed by the M1 foundation are defined.
/// </summary>
public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
}
