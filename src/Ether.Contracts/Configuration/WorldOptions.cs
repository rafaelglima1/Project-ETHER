namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>World</c> configuration section. For the first playable the world
/// is a single bounded map; richer content arrives with the content pipeline.
/// </summary>
public sealed class WorldOptions
{
    public const string SectionName = "World";

    public int Width { get; set; } = 32;

    public int Height { get; set; } = 32;

    /// <summary>Maximum tiles a single movement command may cover (Chebyshev).</summary>
    public int MaxMoveDistance { get; set; } = 12;
}
