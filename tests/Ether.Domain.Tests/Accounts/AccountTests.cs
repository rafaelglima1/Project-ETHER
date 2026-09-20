using Ether.Domain.Accounts;
using Ether.Domain.Common;

namespace Ether.Domain.Tests.Accounts;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Email ValidEmail = new("player@ether.local");
    private static readonly PasswordHash ValidHash = new("pbkdf2-sha256$1$c2FsdA==$aGFzaA==");

    [Fact]
    public void Create_produces_an_active_account()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);

        Assert.False(account.Id.IsEmpty);
        Assert.Equal(ValidEmail, account.Email);
        Assert.Equal(ValidHash, account.PasswordHash);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(Now, account.UpdatedAt);
    }

    [Fact]
    public void Suspending_updates_status_and_timestamp()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);
        var later = Now.AddMinutes(5);

        account.Suspend(later);

        Assert.Equal(AccountStatus.Suspended, account.Status);
        Assert.Equal(later, account.UpdatedAt);
        Assert.Equal(Now, account.CreatedAt);
    }

    [Fact]
    public void Suspending_twice_is_rejected()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);
        account.Suspend(Now);

        Assert.Throws<DomainException>(() => account.Suspend(Now));
    }

    [Fact]
    public void Activating_a_suspended_account_succeeds()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);
        account.Suspend(Now);

        account.Activate(Now.AddMinutes(1));

        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Activating_an_active_account_is_rejected()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);

        Assert.Throws<DomainException>(() => account.Activate(Now));
    }

    [Fact]
    public void Ensure_can_authenticate_rejects_suspended_accounts()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);
        account.Suspend(Now);

        Assert.Throws<DomainException>(account.EnsureCanAuthenticate);
    }

    [Fact]
    public void Change_password_updates_the_hash()
    {
        var account = Account.Create(ValidEmail, ValidHash, Now);
        var newHash = new PasswordHash("pbkdf2-sha256$1$c2FsdA==$bmV3");

        account.ChangePassword(newHash, Now.AddMinutes(1));

        Assert.Equal(newHash, account.PasswordHash);
    }
}
