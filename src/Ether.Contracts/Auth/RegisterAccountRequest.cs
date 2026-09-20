namespace Ether.Contracts.Auth;

/// <summary>Request body for account registration.</summary>
public sealed record RegisterAccountRequest(string Email, string Password);
