using Ether.Application.Abstractions;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Application.Auth;

/// <summary>Issues a fresh access/refresh pair for an account.</summary>
internal static class TokenPairIssuer
{
    public static TokenPairResponse Issue(ITokenService tokenService, AccountId accountId)
    {
        var access = tokenService.CreateAccessToken(accountId);
        var refresh = tokenService.CreateRefreshToken(accountId);

        return new TokenPairResponse(
            accountId.Value,
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt);
    }
}
