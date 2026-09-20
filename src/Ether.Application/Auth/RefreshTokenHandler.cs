using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Auth;

namespace Ether.Application.Auth;

/// <summary>Use case: exchange a refresh token for a new token pair.</summary>
public sealed class RefreshTokenHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITokenService _tokenService;

    public RefreshTokenHandler(IAccountRepository accounts, ITokenService tokenService)
    {
        _accounts = accounts;
        _tokenService = tokenService;
    }

    public async Task<TokenPairResponse> HandleAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidTokenException();
        }

        var accountId = _tokenService.ValidateRefreshToken(request.RefreshToken)
                        ?? throw new InvalidTokenException();

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidTokenException();

        try
        {
            account.EnsureCanAuthenticate();
        }
        catch (Domain.Common.DomainException)
        {
            throw new InvalidTokenException();
        }

        return TokenPairIssuer.Issue(_tokenService, account.Id);
    }
}
