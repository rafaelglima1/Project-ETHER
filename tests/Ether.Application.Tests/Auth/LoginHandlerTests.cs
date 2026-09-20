using Ether.Application.Auth;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Application.Tests.Auth;

public sealed class LoginHandlerTests
{
    private static async Task<(InMemoryPersistence Persistence, LoginHandler Handler)> SetupAsync()
    {
        var persistence = new InMemoryPersistence();
        await persistence.SeedAccountAsync(
            Account.Create(new Email("player@ether.local"), new PasswordHash("hashed:strong-password"), DateTimeOffset.UtcNow));

        var handler = new LoginHandler(
            persistence, new FakePasswordHasher(), new FakeTokenService(), TimeProvider.System);

        return (persistence, handler);
    }

    [Fact]
    public async Task Login_with_valid_credentials_issues_tokens()
    {
        var (persistence, handler) = await SetupAsync();
        var account = await persistence.GetByEmailAsync(new Email("player@ether.local"), CancellationToken.None);

        var response = await handler.HandleAsync(
            new LoginRequest("player@ether.local", "strong-password"), CancellationToken.None);

        Assert.Equal(account!.Id.Value, response.AccountId);
        Assert.StartsWith("access:", response.AccessToken, StringComparison.Ordinal);
        Assert.StartsWith("refresh:", response.RefreshToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_email_returns_invalid_credentials()
    {
        var (_, handler) = await SetupAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginRequest("nobody@ether.local", "strong-password"), CancellationToken.None));
    }

    [Fact]
    public async Task Wrong_password_returns_invalid_credentials()
    {
        var (_, handler) = await SetupAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new LoginRequest("player@ether.local", "wrong-password"), CancellationToken.None));
    }

    [Fact]
    public async Task Suspended_account_cannot_log_in()
    {
        var (persistence, handler) = await SetupAsync();
        var account = await persistence.GetByEmailAsync(new Email("player@ether.local"), CancellationToken.None);
        account!.Suspend(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<Ether.Domain.Common.DomainException>(() =>
            handler.HandleAsync(new LoginRequest("player@ether.local", "strong-password"), CancellationToken.None));
    }
}
