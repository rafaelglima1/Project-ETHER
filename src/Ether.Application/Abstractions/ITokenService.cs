using Ether.Domain.Accounts;

namespace Ether.Application.Abstractions;

/// <summary>A token and its absolute expiry.</summary>
public readonly record struct IssuedToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues and validates the tokens used to establish and resume sessions
/// (Blueprint v5.0 §47). Access tokens are validated by the API host; refresh and
/// game tokens are validated through this abstraction.
/// </summary>
public interface ITokenService
{
    IssuedToken CreateAccessToken(AccountId accountId);

    IssuedToken CreateRefreshToken(AccountId accountId);

    /// <summary>Validates a refresh token and returns the owning account, if valid.</summary>
    AccountId? ValidateRefreshToken(string token);

    /// <summary>Issues a short-lived token for the game server connection.</summary>
    IssuedToken CreateGameToken(AccountId accountId, Guid characterId);

    /// <summary>Validates a game token, distinguishing the failure reason.</summary>
    GameTokenValidation ValidateGameToken(string token);
}

/// <summary>Claims carried by a game session token.</summary>
public readonly record struct GameTokenClaims(AccountId AccountId, Guid CharacterId);

/// <summary>Outcome of a game token validation.</summary>
public enum TokenValidationStatus
{
    Valid = 0,
    Invalid = 1,
    Expired = 2,
    WrongPurpose = 3,
}

/// <summary>Result of validating a game token.</summary>
public readonly record struct GameTokenValidation(TokenValidationStatus Status, GameTokenClaims? Claims)
{
    public static GameTokenValidation Valid(GameTokenClaims claims) => new(TokenValidationStatus.Valid, claims);

    public static GameTokenValidation Failure(TokenValidationStatus status) => new(status, null);
}
