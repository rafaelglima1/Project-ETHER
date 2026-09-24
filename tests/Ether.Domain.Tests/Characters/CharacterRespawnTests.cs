using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterRespawnTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly WorldPosition Spawn = new(new MapId(1), 0, 0);

    private static Character CreateDead(int maxHealth = 100)
    {
        var c = Character.Create(AccountId.New(), "Hero", CharacterClass.Warrior, Spawn, Now, maxHealth);
        c.EnterWorld(Now);
        c.ApplyDamage(maxHealth, Now.AddSeconds(1));
        return c;
    }

    [Fact]
    public void Respawn_restores_health_and_returns_to_in_world()
    {
        var c = CreateDead();
        Assert.Equal(CharacterState.Dead, c.State);

        c.Respawn(Spawn, Now.AddSeconds(10));

        Assert.Equal(c.MaxHealth, c.Health);
        Assert.Equal(CharacterState.InWorld, c.State);
        Assert.Equal(0, c.PositionX);
        Assert.Equal(0, c.PositionY);
        Assert.True(c.IsAlive);
    }

    [Fact]
    public void Respawn_places_character_at_the_given_spawn()
    {
        var c = CreateDead();
        var spawn = new WorldPosition(new MapId(1), 5, 7);

        c.Respawn(spawn, Now.AddSeconds(10));

        Assert.Equal(5, c.PositionX);
        Assert.Equal(7, c.PositionY);
    }

    [Fact]
    public void Respawn_from_alive_is_rejected()
    {
        var c = Character.Create(AccountId.New(), "Hero", CharacterClass.Warrior, Spawn, Now);
        c.EnterWorld(Now);

        Assert.Throws<InvalidStateException>(() => c.Respawn(Spawn, Now));
    }

    [Fact]
    public void Respawn_with_empty_map_is_rejected()
    {
        var c = CreateDead();

        Assert.Throws<DomainException>(() => c.Respawn(new WorldPosition(MapId.Empty, 0, 0), Now));
    }

    [Fact]
    public void Dead_character_cannot_receive_damage()
    {
        var c = CreateDead();

        Assert.Throws<InvalidStateException>(() => c.ApplyDamage(1, Now));
    }

    [Fact]
    public void Dead_character_cannot_move()
    {
        var c = CreateDead();
        var map = new WorldMap(new MapId(1), 32, 32);

        Assert.ThrowsAny<DomainException>(() => c.MoveTo(new WorldPosition(new MapId(1), 1, 1), 12, map, Now));
    }

    [Fact]
    public void Respawn_transitions_through_respawning()
    {
        var c = CreateDead();

        Assert.Equal(CharacterState.Dead, c.State);
        // Respawn internally: Dead → Respawning → InWorld
        c.Respawn(Spawn, Now.AddSeconds(5));
        Assert.Equal(CharacterState.InWorld, c.State);
    }

    [Fact]
    public void Respawn_does_not_reset_experience()
    {
        var c = CreateDead();
        c.GrantExperience(50, Now.AddSeconds(1));

        c.Respawn(Spawn, Now.AddSeconds(10));

        Assert.Equal(50, c.Experience);
        Assert.Equal(1, c.Level);
    }
}
