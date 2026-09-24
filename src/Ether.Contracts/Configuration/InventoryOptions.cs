namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>Inventory</c> configuration section.
/// Capacity is the maximum number of distinct item stacks a character can hold.
/// </summary>
public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Maximum distinct item stacks per character (Blueprint v5.0 §27: 30).</summary>
    public int MaxSlots { get; set; } = 30;
}
