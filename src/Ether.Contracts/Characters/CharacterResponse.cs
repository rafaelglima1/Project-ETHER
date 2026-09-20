namespace Ether.Contracts.Characters;

/// <summary>Character representation exposed by the HTTP API.</summary>
/// <remarks>
/// Class and state are transported as their stable domain names
/// (e.g. "Warrior", "Offline") so this contract stays independent of the domain layer.
/// </remarks>
public sealed record CharacterResponse(
    Guid CharacterId,
    Guid AccountId,
    string Name,
    string CharacterClass,
    string State,
    int Level,
    long Experience,
    int MapId,
    int PositionX,
    int PositionY,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
