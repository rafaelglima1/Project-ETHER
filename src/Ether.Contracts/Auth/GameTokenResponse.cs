namespace Ether.Contracts.Auth;

/// <summary>Short-lived credential for the GameServer realtime connection.</summary>
public sealed record GameTokenResponse(string GameToken, DateTimeOffset ExpiresAt);
