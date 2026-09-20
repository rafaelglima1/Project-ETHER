namespace Ether.Contracts.Auth;

/// <summary>Request body for refreshing an access token.</summary>
public sealed record RefreshRequest(string RefreshToken);
