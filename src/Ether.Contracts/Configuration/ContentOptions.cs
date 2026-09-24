namespace Ether.Contracts.Configuration;

/// <summary>Filesystem location for versioned gameplay content files.</summary>
public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>Path relative to the application directory unless absolute.</summary>
    public string RootPath { get; set; } = "content";
}
