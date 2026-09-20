using Ether.Contracts.Configuration;

using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Authentication;

/// <summary>
/// Validates <see cref="AuthenticationOptions"/> at startup and on first resolution.
/// In Production a usable signing key is mandatory: the application must fail fast
/// rather than silently accept an empty, too-short or development placeholder key.
/// In Development/Test the convenient defaults remain valid.
/// </summary>
public sealed class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    private readonly bool _isProduction;

    public AuthenticationOptionsValidator(bool isProduction)
    {
        _isProduction = isProduction;
    }

    public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_isProduction)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:SigningKey is required in Production.");
        }

        if (string.Equals(
                options.SigningKey,
                AuthenticationOptions.DevelopmentPlaceholderSigningKey,
                StringComparison.Ordinal) ||
            string.Equals(
                options.SigningKey,
                AuthenticationOptions.DevelopmentFallbackSigningKey,
                StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:SigningKey must not use a development key value in Production.");
        }

        if (options.SigningKey.Length < AuthenticationOptions.MinimumSigningKeyLength)
        {
            return ValidateOptionsResult.Fail(
                $"Authentication:SigningKey must be at least {AuthenticationOptions.MinimumSigningKeyLength} characters in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
