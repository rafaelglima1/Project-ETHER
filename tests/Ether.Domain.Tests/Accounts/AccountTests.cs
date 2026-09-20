using Ether.Domain.Accounts;
using Ether.Domain.Common;

namespace Ether.Domain.Tests.Accounts;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_produces_an_active_account()
    {
        var account = Account.Create(Now);

        Assert.False(account.Id.IsEmpty);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(Now, account.UpdatedAt);
    }

    [Fact]
    public void Suspending_updates_status_and_timestamp()
    {
        var account = Account.Create(Now);
        var later = Now.AddMinutes(5);

        account.Suspend(later);

        Assert.Equal(AccountStatus.Suspended, account.Status);
        Assert.Equal(later, account.UpdatedAt);
        Assert.Equal(Now, account.CreatedAt);
    }

    [Fact]
    public void Suspending_twice_is_rejected()
    {
        var account = Account.Create(Now);
        account.Suspend(Now);

        Assert.Throws<DomainException>(() => account.Suspend(Now));
    }

    [Fact]
    public void Activating_a_suspended_account_succeeds()
    {
        var account = Account.Create(Now);
        account.Suspend(Now);

        account.Activate(Now.AddMinutes(1));

        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Activating_an_active_account_is_rejected()
    {
        var account = Account.Create(Now);

        Assert.Throws<DomainException>(() => account.Activate(Now));
    }
}
