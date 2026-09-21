using Ether.Application.Abstractions;
using Ether.Domain.Accounts;

namespace Ether.GameServer.Tests.Fakes;

/// <summary>Controllable token service for GameServer unit tests.</summary>
internal sealed class StubTokenService : ITokenService
{
    public TokenValidationStatus GameStatus { get; set; } = TokenValidationStatus.Valid;

    public GameTokenClaims? GameClaims { get; set; }

    public IssuedToken CreateAccessToken(AccountId accountId) => new("access", DateTimeOffset.UtcNow.AddMinutes(5));

    public IssuedToken CreateRefreshToken(AccountId accountId) => new("refresh", DateTimeOffset.UtcNow.AddDays(1));

    public AccountId? ValidateRefreshToken(string token) => null;

    public IssuedToken CreateGameToken(AccountId accountId, Guid characterId) => new("game", DateTimeOffset.UtcNow.AddMinutes(5));

    public GameTokenValidation ValidateGameToken(string token) =>
        GameStatus == TokenValidationStatus.Valid && GameClaims is not null
            ? GameTokenValidation.Valid(GameClaims.Value)
            : GameTokenValidation.Failure(GameStatus);
}
