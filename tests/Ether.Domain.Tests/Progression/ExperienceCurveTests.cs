using Ether.Domain.Common;
using Ether.Domain.Progression;

namespace Ether.Domain.Tests.Progression;

public sealed class ExperienceCurveTests
{
    [Fact]
    public void Level_1_requires_one_hundred_experience()
    {
        Assert.Equal(100, ExperienceCurve.ExperienceToAdvanceFrom(1));
    }

    [Fact]
    public void Curve_follows_floor_100_times_level_pow_1_65()
    {
        Assert.Equal((long)Math.Floor(100 * Math.Pow(2, 1.65)), ExperienceCurve.ExperienceToAdvanceFrom(2));
        Assert.Equal((long)Math.Floor(100 * Math.Pow(3, 1.65)), ExperienceCurve.ExperienceToAdvanceFrom(3));
        Assert.Equal((long)Math.Floor(100 * Math.Pow(10, 1.65)), ExperienceCurve.ExperienceToAdvanceFrom(10));
    }

    [Fact]
    public void Curve_is_monotonically_increasing()
    {
        for (var level = 1; level < 50; level++)
        {
            Assert.True(ExperienceCurve.ExperienceToAdvanceFrom(level + 1) > ExperienceCurve.ExperienceToAdvanceFrom(level));
        }
    }

    [Fact]
    public void Total_experience_to_reach_a_level_accumulates()
    {
        Assert.Equal(0, ExperienceCurve.TotalExperienceToReach(1));
        Assert.Equal(100, ExperienceCurve.TotalExperienceToReach(2));
        Assert.Equal(
            ExperienceCurve.ExperienceToAdvanceFrom(1) + ExperienceCurve.ExperienceToAdvanceFrom(2),
            ExperienceCurve.TotalExperienceToReach(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_levels_are_rejected(int level)
    {
        Assert.Throws<DomainException>(() => ExperienceCurve.ExperienceToAdvanceFrom(level));
    }
}
