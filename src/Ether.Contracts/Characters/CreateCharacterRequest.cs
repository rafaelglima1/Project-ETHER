namespace Ether.Contracts.Characters;

/// <summary>Request body for creating a character.</summary>
/// <param name="Name">Character name (trimmed, non-empty, max 24 characters).</param>
/// <param name="CharacterClass">Class name: Warrior, Ranger or Arcanist.</param>
public sealed record CreateCharacterRequest(string Name, string CharacterClass);
