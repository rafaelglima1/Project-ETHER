using Ether.Contracts.Configuration;
using Ether.Infrastructure.Authentication;

namespace Ether.Infrastructure.Tests.Authentication;

public sealed class AuthenticationOptionsValidatorTests
{
    private const string StrongKey = "0123456789abcdef0123456789abcdef";

    private static AuthenticationOptions OptionsWith(string signingKey) =>
        new() { SigningKey = signingKey };

    [Fact]
    public void Production_requires_signing_key()
    {
        var result = Validate(isProduction: true, OptionsWith(string.Empty));

        Assert.True(result.Failed);
        Assert.Contains("SigningKey", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_whitespace_signing_key()
    {
        var result = Validate(isProduction: true, OptionsWith("   "));

        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_rejects_development_placeholder_signing_key()
    {
        var result = Validate(
            isProduction: true,
            OptionsWith(AuthenticationOptions.DevelopmentPlaceholderSigningKey));

        Assert.True(result.Failed);
        Assert.Contains("placeholder", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_signing_key_shorter_than_minimum()
    {
        var tooShort = new string('a', AuthenticationOptions.MinimumSigningKeyLength - 1);

        var result = Validate(isProduction: true, OptionsWith(tooShort));

        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_accepts_signing_key_at_minimum_length()
    {
        var result = Validate(isProduction: true, OptionsWith(StrongKey));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Development_allows_empty_signing_key()
    {
        var result = Validate(isProduction: false, OptionsWith(string.Empty));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Development_allows_placeholder_signing_key()
    {
        var result = Validate(
            isProduction: false,
            OptionsWith(AuthenticationOptions.DevelopmentPlaceholderSigningKey));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Null_options_throws()
    {
        var validator = new AuthenticationOptionsValidator(isProduction: true);

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(
        bool isProduction,
        AuthenticationOptions options) =>
        new AuthenticationOptionsValidator(isProduction).Validate(null, options);
}
