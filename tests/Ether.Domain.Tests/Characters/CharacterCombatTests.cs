using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterCombatTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Character CreateInWorld(int maxHealth = 100)
    {
        var character = Character.Create(
            AccountId.New(),
            "Fighter",
            CharacterClass.Warrior,
            new WorldPosition(new MapId(1), 0, 0),
            Now,
            maxHealth);
        character.EnterWorld(Now);
        return character;
    }

    [Fact]
    public void Create_initializes_health()
    {
        var character = Character.Create(
            AccountId.New(), "Fighter", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now, maxHealth: 250);

        Assert.Equal(250, character.MaxHealth);
        Assert.Equal(250, character.Health);
        Assert.True(character.IsAlive);
    }

    [Fact]
    public void Non_positive_max_health_is_rejected()
    {
        Assert.Throws<DomainException>(() => Character.Create(
            AccountId.New(), "Fighter", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now, maxHealth: 0));
    }

    [Fact]
    public void Damage_reduces_health()
    {
        var character = CreateInWorld(maxHealth: 100);

        var defeated = character.ApplyDamage(30, Now.AddSeconds(1));

        Assert.Equal(70, character.Health);
        Assert.False(defeated);
        Assert.True(character.IsAlive);
    }

    [Fact]
    public void Health_never_goes_negative()
    {
        var character = CreateInWorld(maxHealth: 50);

        var defeated = character.ApplyDamage(999, Now.AddSeconds(1));

        Assert.Equal(0, character.Health);
        Assert.True(defeated);
        Assert.False(character.IsAlive);
        Assert.Equal(CharacterState.Dead, character.State);
    }

    [Fact]
    public void Dead_character_cannot_be_damaged_again()
    {
        var character = CreateInWorld(maxHealth: 10);
        character.ApplyDamage(10, Now.AddSeconds(1));

        Assert.Throws<InvalidStateException>(() => character.ApplyDamage(1, Now.AddSeconds(2)));
    }

    [Fact]
    public void Negative_damage_is_rejected()
    {
        var character = CreateInWorld();

        Assert.Throws<DomainException>(() => character.ApplyDamage(-1, Now));
    }

    [Fact]
    public void Entering_combat_moves_in_world_to_combat()
    {
        var character = CreateInWorld();

        character.EnterCombat(Now.AddSeconds(1));

        Assert.Equal(CharacterState.Combat, character.State);
    }

    [Fact]
    public void Offline_character_cannot_enter_combat()
    {
        var character = Character.Create(
            AccountId.New(), "Fighter", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now);

        Assert.Throws<InvalidStateException>(() => character.EnterCombat(Now));
    }
}
