namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds runtime movement limits. Map identity and dimensions are authored in the
/// versioned content bundle.
/// </summary>
public sealed class WorldOptions
{
    public const string SectionName = "World";

    /// <summary>Maximum tiles a single movement command may cover (Chebyshev).</summary>
    public int MaxMoveDistance { get; set; } = 12;
}
