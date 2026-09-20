using Ether.Domain.Common;

namespace Ether.Domain.Tests.Common;

public sealed class DomainExceptionTests
{
    [Fact]
    public void Preserves_message()
    {
        var exception = new DomainException("invariant violated");

        Assert.Equal("invariant violated", exception.Message);
    }

    [Fact]
    public void Preserves_inner_exception()
    {
        var inner = new InvalidOperationException("root cause");
        var exception = new DomainException("invariant violated", inner);

        Assert.Same(inner, exception.InnerException);
    }
}
