using Ether.Domain.Characters;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterEnumTests
{
    [Fact]
    public void CharacterState_defines_the_m0_lifecycle_states()
    {
        Assert.Equal(
            new[]
            {
                CharacterState.Offline,
                CharacterState.Loading,
                CharacterState.InWorld,
                CharacterState.Combat,
                CharacterState.Dead,
                CharacterState.Respawning,
                CharacterState.DisconnectGrace,
            },
            Enum.GetValues<CharacterState>());
    }

    [Fact]
    public void CharacterClass_supports_the_three_planned_classes()
    {
        Assert.Equal(
            new[] { CharacterClass.Warrior, CharacterClass.Ranger, CharacterClass.Arcanist },
            Enum.GetValues<CharacterClass>());
    }
}
