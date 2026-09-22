namespace Ether.Domain.Combat;

/// <summary>
/// Source of randomness for combat resolution. Injected so critical hits are
/// deterministic in tests (Blueprint: all critical RNG is server-side).
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns a value in [0, 1).</summary>
    double NextUnit();
}
