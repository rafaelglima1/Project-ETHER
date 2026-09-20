using Ether.Application.Abstractions;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;
using Ether.Domain.Common;

namespace Ether.Application.Accounts;

/// <summary>Use case: register a new account with credentials.</summary>
public sealed class RegisterAccountHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public RegisterAccountHandler(
        IAccountRepository accounts,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterAccountResponse> HandleAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = new Email(request.Email);
        ValidatePassword(request.Password);

        var passwordHash = new PasswordHash(_passwordHasher.Hash(request.Password));
        var account = Account.Create(email, passwordHash, _timeProvider.GetUtcNow());

        await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RegisterAccountResponse(
            account.Id.Value,
            account.Email.Value,
            account.Status.ToString(),
            account.CreatedAt);
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainException("Password must not be empty.");
        }

        if (password.Length < Account.MinPasswordLength || password.Length > Account.MaxPasswordLength)
        {
            throw new DomainException(
                $"Password must be between {Account.MinPasswordLength} and {Account.MaxPasswordLength} characters.");
        }
    }
}
