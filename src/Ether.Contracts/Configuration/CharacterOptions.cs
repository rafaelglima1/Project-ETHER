namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Character</c> configuration section.
/// Defines the server-authoritative starting position used when a character is
/// created, until the world/content milestone introduces real spawn definitions.
/// </summary>
public sealed class CharacterOptions
{
    public const string SectionName = "Character";

    /// <summary>Map where new characters start.</summary>
    public int StartingMapId { get; set; } = 1;

    /// <summary>Initial X tile.</summary>
    public int StartingX { get; set; }

    /// <summary>Initial Y tile.</summary>
    public int StartingY { get; set; }
}
