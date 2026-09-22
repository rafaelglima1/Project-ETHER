namespace Ether.Domain.Progression;

/// <summary>
/// Canonical experience curve (Blueprint v5.0 §29): the experience required to
/// advance from a level to the next is <c>floor(100 × level^1.65)</c>.
/// This is the single place the formula exists; nothing else may redefine it.
/// </summary>
public static class ExperienceCurve
{
    /// <summary>Bumped whenever the curve changes so persisted progression stays interpretable.</summary>
    public const int FormulaVersion = 1;

    private const double Coefficient = 100d;
    private const double Exponent = 1.65d;

    /// <summary>Experience required to advance from <paramref name="level"/> to the next level.</summary>
    public static long ExperienceToAdvanceFrom(int level)
    {
        if (level < 1)
        {
            throw new Common.DomainException("Level must be at least 1.");
        }

        return (long)Math.Floor(Coefficient * Math.Pow(level, Exponent));
    }

    /// <summary>Total experience required to reach <paramref name="level"/> from level 1.</summary>
    public static long TotalExperienceToReach(int level)
    {
        if (level < 1)
        {
            throw new Common.DomainException("Level must be at least 1.");
        }

        long total = 0;
        for (var current = 1; current < level; current++)
        {
            total += ExperienceToAdvanceFrom(current);
        }

        return total;
    }
}
