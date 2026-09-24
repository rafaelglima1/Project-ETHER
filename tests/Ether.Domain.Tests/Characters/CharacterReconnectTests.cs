using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

/// <summary>
/// Reconnecting a character whose previous session ended without a clean leave.
/// The character can still be flagged in a transient in-world sub-state (Combat or
/// DisconnectGrace); entering the world again must reassert InWorld instead of
/// failing, so the client can recover its snapshot.
/// </summary>
public sealed class CharacterReconnectTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Character CreateInWorld()
    {
        var character = Character.Create(
            AccountId.New(),
            "Reconnector",
            CharacterClass.Warrior,
            new WorldPosition(new MapId(1), 0, 0),
            Now);
        character.EnterWorld(Now);
        return character;
    }

    [Fact]
    public void Entering_world_again_from_combat_reasserts_in_world()
    {
        var character = CreateInWorld();
        character.EnterCombat(Now.AddSeconds(1));
        Assert.Equal(CharacterState.Combat, character.State);

        character.EnterWorld(Now.AddSeconds(2));

        Assert.Equal(CharacterState.InWorld, character.State);
    }

    [Fact]
    public void Entering_world_again_from_disconnect_grace_reasserts_in_world()
    {
        var character = CreateInWorld();
        character.ChangeState(CharacterState.DisconnectGrace, Now.AddSeconds(1));

        character.EnterWorld(Now.AddSeconds(2));

        Assert.Equal(CharacterState.InWorld, character.State);
    }

    [Fact]
    public void Entering_world_again_while_in_world_is_idempotent()
    {
        var character = CreateInWorld();

        character.EnterWorld(Now.AddSeconds(1));

        Assert.Equal(CharacterState.InWorld, character.State);
    }

    [Fact]
    public void Dead_character_still_cannot_enter_world()
    {
        var character = CreateInWorld();
        character.ApplyDamage(character.MaxHealth, Now.AddSeconds(1));
        Assert.Equal(CharacterState.Dead, character.State);

        Assert.Throws<DomainException>(() => character.EnterWorld(Now.AddSeconds(2)));
    }
}
