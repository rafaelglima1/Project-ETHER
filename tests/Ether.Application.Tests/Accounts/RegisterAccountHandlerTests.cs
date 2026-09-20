using Ether.Application.Accounts;
using Ether.Application.Exceptions;
using Ether.Application.Tests.Fakes;
using Ether.Contracts.Auth;
using Ether.Domain.Common;

namespace Ether.Application.Tests.Accounts;

public sealed class RegisterAccountHandlerTests
{
    private static RegisterAccountHandler CreateHandler(InMemoryPersistence persistence) =>
        new(persistence, persistence, new FakePasswordHasher(), TimeProvider.System);

    [Fact]
    public async Task Registers_an_active_account()
    {
        var persistence = new InMemoryPersistence();
        var handler = CreateHandler(persistence);

        var response = await handler.HandleAsync(
            new RegisterAccountRequest("Player@Ether.Local", "strong-password"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.AccountId);
        Assert.Equal("player@ether.local", response.Email);
        Assert.Equal("Active", response.Status);
    }

    [Fact]
    public async Task Stores_the_password_hashed()
    {
        var persistence = new InMemoryPersistence();
        var handler = CreateHandler(persistence);

        var response = await handler.HandleAsync(
            new RegisterAccountRequest("player@ether.local", "strong-password"),
            CancellationToken.None);

        var stored = await persistence.GetByIdAsync(
            new Ether.Domain.Accounts.AccountId(response.AccountId), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("hashed:strong-password", stored!.PasswordHash.Value);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var persistence = new InMemoryPersistence();
        var handler = CreateHandler(persistence);
        var request = new RegisterAccountRequest("player@ether.local", "strong-password");

        await handler.HandleAsync(request, CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            handler.HandleAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Invalid_email_is_rejected(string email)
    {
        var handler = CreateHandler(new InMemoryPersistence());

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new RegisterAccountRequest(email, "strong-password"), CancellationToken.None));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task Weak_password_is_rejected(string password)
    {
        var handler = CreateHandler(new InMemoryPersistence());

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new RegisterAccountRequest("player@ether.local", password), CancellationToken.None));
    }
}
