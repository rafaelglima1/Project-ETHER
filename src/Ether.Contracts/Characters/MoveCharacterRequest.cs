namespace Ether.Contracts.Characters;

/// <summary>Request body for a movement command (destination tile).</summary>
public sealed record MoveCharacterRequest(int X, int Y);
