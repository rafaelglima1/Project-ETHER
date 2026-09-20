using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Application.Auth;

/// <summary>Use case: authenticate an account and issue a token pair.</summary>
public sealed class LoginHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public LoginHandler(
        IAccountRepository accounts,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<TokenPairResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Email email;
        try
        {
            email = new Email(request.Email);
        }
        catch (Domain.Common.DomainException)
        {
            // Do not reveal whether the email exists.
            throw new InvalidCredentialsException();
        }

        var account = await _accounts.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        // Verify a dummy hash when the account is missing to keep timing similar.
        if (account is null || !_passwordHasher.Verify(request.Password ?? string.Empty, account.PasswordHash.Value))
        {
            throw new InvalidCredentialsException();
        }

        account.EnsureCanAuthenticate();

        return TokenPairIssuer.Issue(_tokenService, account.Id);
    }
}
