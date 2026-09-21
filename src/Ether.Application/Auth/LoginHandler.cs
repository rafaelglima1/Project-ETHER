using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Application.Auth;

/// <summary>Use case: authenticate an account and issue a token pair.</summary>
public sealed class LoginHandler
{
    /// <summary>
    /// Well-formed PBKDF2-SHA256 hash used when the account does not exist, so the
    /// same hashing work is always performed and the response time does not reveal
    /// whether an email is registered.
    /// </summary>
    internal const string DummyPasswordHash =
        "pbkdf2-sha256$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

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

        // Always run the hasher (against a dummy hash when the account is missing)
        // so timing does not reveal whether the email exists.
        var storedHash = account?.PasswordHash.Value ?? DummyPasswordHash;
        var passwordValid = _passwordHasher.Verify(request.Password ?? string.Empty, storedHash);

        if (account is null || !passwordValid)
        {
            throw new InvalidCredentialsException();
        }

        account.EnsureCanAuthenticate();

        return TokenPairIssuer.Issue(_tokenService, account.Id);
    }
}
