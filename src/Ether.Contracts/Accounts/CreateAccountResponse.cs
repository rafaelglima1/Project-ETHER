namespace Ether.Contracts.Accounts;

/// <summary>Response returned when an account is created.</summary>
public sealed record CreateAccountResponse(
    Guid AccountId,
    string Status,
    DateTimeOffset CreatedAt);
