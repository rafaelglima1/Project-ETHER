using Ether.Application.Auth;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Application.Tests.Auth;

public sealed class RefreshTokenHandlerTests
{
    private static async Task<(InMemoryPersistence Persistence, RefreshTokenHandler Handler)> SetupAsync()
    {
        var persistence = new InMemoryPersistence();
        var account = Account.Create(
            new Email("player@ether.local"), new PasswordHash("hashed:strong-password"), DateTimeOffset.UtcNow);
        await persistence.SeedAccountAsync(account);

        return (persistence, new RefreshTokenHandler(persistence, new FakeTokenService()));
    }

    [Fact]
    public async Task Valid_refresh_token_issues_a_new_pair()
    {
        var (persistence, handler) = await SetupAsync();
        var account = await persistence.GetByEmailAsync(new Email("player@ether.local"), CancellationToken.None);

        var response = await handler.HandleAsync(
            new RefreshRequest($"refresh:{account!.Id.Value}"), CancellationToken.None);

        Assert.Equal(account.Id.Value, response.AccountId);
        Assert.StartsWith("access:", response.AccessToken, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("access:00000000-0000-0000-0000-000000000000")]
    public async Task Invalid_refresh_token_is_rejected(string token)
    {
        var (_, handler) = await SetupAsync();

        await Assert.ThrowsAsync<InvalidTokenException>(() =>
            handler.HandleAsync(new RefreshRequest(token), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_account_is_rejected()
    {
        var (_, handler) = await SetupAsync();

        await Assert.ThrowsAsync<InvalidTokenException>(() =>
            handler.HandleAsync(new RefreshRequest($"refresh:{Guid.NewGuid()}"), CancellationToken.None));
    }
}
