using Ether.Application.Accounts;
using Ether.Application.Tests.Fakes;

namespace Ether.Application.Tests.Accounts;

public sealed class CreateAccountHandlerTests
{
    [Fact]
    public async Task Creates_an_active_account()
    {
        var persistence = new InMemoryPersistence();
        var handler = new CreateAccountHandler(persistence, persistence, TimeProvider.System);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.AccountId);
        Assert.Equal("Active", response.Status);
    }

    [Fact]
    public async Task Persists_the_account()
    {
        var persistence = new InMemoryPersistence();
        var handler = new CreateAccountHandler(persistence, persistence, TimeProvider.System);

        var response = await handler.HandleAsync(CancellationToken.None);
        var stored = await persistence.GetByIdAsync(
            new Ether.Domain.Accounts.AccountId(response.AccountId),
            CancellationToken.None);

        Assert.NotNull(stored);
    }
}
