using Ether.Application.Abstractions;
using Ether.Domain.Accounts;

namespace Ether.Application.Tests.Fakes;

/// <summary>Deterministic token service for application-layer tests.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public static readonly DateTimeOffset FixedExpiry = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public IssuedToken CreateAccessToken(AccountId accountId) =>
        new($"access:{accountId.Value}", FixedExpiry);

    public IssuedToken CreateRefreshToken(AccountId accountId) =>
        new($"refresh:{accountId.Value}", FixedExpiry);

    public AccountId? ValidateRefreshToken(string token)
    {
        if (token is null || !token.StartsWith("refresh:", StringComparison.Ordinal))
        {
            return null;
        }

        return Guid.TryParse(token["refresh:".Length..], out var value) ? new AccountId(value) : null;
    }

    public IssuedToken CreateGameToken(AccountId accountId, Guid characterId) =>
        new($"game:{accountId.Value}:{characterId}", FixedExpiry);

    public GameTokenClaims? ValidateGameToken(string token)
    {
        if (token is null || !token.StartsWith("game:", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = token.Split(':');
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var accountId) || !Guid.TryParse(parts[2], out var characterId))
        {
            return null;
        }

        return new GameTokenClaims(new AccountId(accountId), characterId);
    }
}
