namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Authentication</c> configuration section.
/// Signing material must never be committed; provide it through environment variables.
/// </summary>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Issuer { get; set; } = "ether";

    public string Audience { get; set; } = "ether-client";

    /// <summary>HMAC signing key. Must be supplied via configuration; never versioned.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 30;

    /// <summary>Lifetime of the short-lived game session token, in minutes.</summary>
    public int GameTokenLifetimeMinutes { get; set; } = 5;

    /// <summary>Indicates whether signing material was provided.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SigningKey);
}
