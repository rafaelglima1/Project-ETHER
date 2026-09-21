using System.Security.Claims;

using Ether.Application.Exceptions;
using Ether.Domain.Accounts;

namespace Ether.Api.Auth;

/// <summary>Reads the authenticated account from the validated access token.</summary>
internal static class ClaimsPrincipalExtensions
{
    public static AccountId AccountId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subject, out var value) || value == Guid.Empty)
        {
            throw new InvalidTokenException("Authenticated account is missing from the token.");
        }

        return new AccountId(value);
    }
}
