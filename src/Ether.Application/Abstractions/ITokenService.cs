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

    /// <summary>Validates a game token and returns its claims, if valid.</summary>
    GameTokenClaims? ValidateGameToken(string token);
}

/// <summary>Claims carried by a game session token.</summary>
public readonly record struct GameTokenClaims(AccountId AccountId, Guid CharacterId);
