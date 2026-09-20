namespace Ether.Contracts.Auth;

/// <summary>Request body for login.</summary>
public sealed record LoginRequest(string Email, string Password);
