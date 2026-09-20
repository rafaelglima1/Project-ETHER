using Ether.Application.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Ether.Application.Tests.DependencyInjection;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEtherApplication_returns_the_same_collection()
    {
        var services = new ServiceCollection();

        var result = services.AddEtherApplication();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddEtherApplication_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationServiceCollectionExtensions.AddEtherApplication(null!));
    }
}
