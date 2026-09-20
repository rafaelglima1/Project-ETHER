using Ether.Domain.Accounts;
using Ether.Domain.Common;

namespace Ether.Domain.Tests.Accounts;

public sealed class EmailTests
{
    [Fact]
    public void Is_normalized_to_lower_case()
    {
        var email = new Email("  Player@Ether.Local  ");

        Assert.Equal("player@ether.local", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@no-local")]
    [InlineData("no-domain@")]
    [InlineData("two@at@signs")]
    [InlineData("has space@ether.local")]
    public void Invalid_values_are_rejected(string value)
    {
        Assert.Throws<DomainException>(() => { _ = new Email(value); });
    }

    [Fact]
    public void Too_long_is_rejected()
    {
        var value = new string('a', Email.MaxLength) + "@e.io";

        Assert.Throws<DomainException>(() => { _ = new Email(value); });
    }
}

public sealed class PasswordHashTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_values_are_rejected(string value)
    {
        Assert.Throws<DomainException>(() => { _ = new PasswordHash(value); });
    }

    [Fact]
    public void Valid_value_is_accepted()
    {
        var hash = new PasswordHash("pbkdf2-sha256$1$c2FsdA==$aGFzaA==");

        Assert.False(hash.IsEmpty);
    }
}
