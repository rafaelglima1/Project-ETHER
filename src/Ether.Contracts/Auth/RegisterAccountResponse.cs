namespace Ether.Contracts.Auth;

/// <summary>Response returned when an account is registered.</summary>
public sealed record RegisterAccountResponse(
    Guid AccountId,
    string Email,
    string Status,
    DateTimeOffset CreatedAt);
