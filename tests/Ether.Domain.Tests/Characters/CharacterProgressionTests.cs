using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.Progression;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterProgressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Character Create() =>
        Character.Create(
            AccountId.New(), "Hero", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now);

    [Fact]
    public void Partial_experience_does_not_level_up()
    {
        var character = Create();

        var gained = character.GrantExperience(ExperienceCurve.ExperienceToAdvanceFrom(1) - 1, Now);

        Assert.Equal(0, gained);
        Assert.Equal(1, character.Level);
        Assert.Equal(99, character.Experience);
    }

    [Fact]
    public void Reaching_the_threshold_levels_up_and_rolls_over()
    {
        var character = Create();
        var required = ExperienceCurve.ExperienceToAdvanceFrom(1);

        var gained = character.GrantExperience(required + 5, Now);

        Assert.Equal(1, gained);
        Assert.Equal(2, character.Level);
        Assert.Equal(5, character.Experience);
    }

    [Fact]
    public void Large_grants_can_level_multiple_times()
    {
        var character = Create();
        var total = ExperienceCurve.ExperienceToAdvanceFrom(1)
                    + ExperienceCurve.ExperienceToAdvanceFrom(2)
                    + ExperienceCurve.ExperienceToAdvanceFrom(3);

        var gained = character.GrantExperience(total, Now);

        Assert.Equal(3, gained);
        Assert.Equal(4, character.Level);
        Assert.Equal(0, character.Experience);
    }

    [Fact]
    public void Zero_experience_is_a_no_op()
    {
        var character = Create();

        Assert.Equal(0, character.GrantExperience(0, Now));
        Assert.Equal(1, character.Level);
        Assert.Equal(0, character.Experience);
    }

    [Fact]
    public void Negative_experience_is_rejected()
    {
        var character = Create();

        Assert.Throws<DomainException>(() => character.GrantExperience(-1, Now));
    }
}
