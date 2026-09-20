namespace Ether.Contracts.Auth;

/// <summary>Issued token pair returned by login and refresh.</summary>
public sealed record TokenPairResponse(
    Guid AccountId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
