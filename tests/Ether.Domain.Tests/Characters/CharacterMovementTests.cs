using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterMovementTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly WorldMap Map = new(new MapId(1), 32, 32);

    private static Character CreateInWorld()
    {
        var character = Character.Create(
            AccountId.New(), "Mover", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now);
        character.EnterWorld(Now);
        return character;
    }

    [Fact]
    public void Enter_world_sets_the_in_world_state()
    {
        var character = Character.Create(
            AccountId.New(), "Mover", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now);

        character.EnterWorld(Now);

        Assert.Equal(CharacterState.InWorld, character.State);
    }

    [Fact]
    public void Leave_world_returns_to_offline()
    {
        var character = CreateInWorld();

        character.LeaveWorld(Now.AddMinutes(1));

        Assert.Equal(CharacterState.Offline, character.State);
    }

    [Fact]
    public void Valid_move_updates_the_position()
    {
        var character = CreateInWorld();

        character.MoveTo(new WorldPosition(new MapId(1), 5, 7), maxDistance: 12, Map, Now.AddSeconds(1));

        Assert.Equal(5, character.Position.X);
        Assert.Equal(7, character.Position.Y);
    }

    [Fact]
    public void Move_beyond_max_distance_is_rejected()
    {
        var character = CreateInWorld();

        Assert.Throws<DomainException>(() =>
            character.MoveTo(new WorldPosition(new MapId(1), 20, 0), maxDistance: 12, Map, Now));
    }

    [Fact]
    public void Move_outside_the_map_is_rejected()
    {
        var character = CreateInWorld();

        Assert.Throws<DomainException>(() =>
            character.MoveTo(new WorldPosition(new MapId(1), 40, 0), maxDistance: 12, Map, Now));
    }

    [Fact]
    public void Offline_character_cannot_move()
    {
        var character = Character.Create(
            AccountId.New(), "Idle", CharacterClass.Warrior, new WorldPosition(new MapId(1), 0, 0), Now);

        Assert.Throws<DomainException>(() =>
            character.MoveTo(new WorldPosition(new MapId(1), 1, 1), maxDistance: 12, Map, Now));
    }
}
