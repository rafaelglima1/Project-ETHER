using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Characters;

public sealed class CharacterTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly WorldPosition Start = new(new MapId(1), 4, 7);

    private static Character CreateCharacter(
        string name = "Aragorn",
        CharacterClass characterClass = CharacterClass.Warrior) =>
        Character.Create(AccountId.New(), name, characterClass, Start, Now);

    [Fact]
    public void Create_produces_a_valid_initial_character()
    {
        var accountId = AccountId.New();

        var character = Character.Create(accountId, "Aragorn", CharacterClass.Warrior, Start, Now);

        Assert.False(character.Id.IsEmpty);
        Assert.Equal(accountId, character.AccountId);
        Assert.Equal("Aragorn", character.Name);
        Assert.Equal(CharacterClass.Warrior, character.Class);
        Assert.Equal(CharacterState.Offline, character.State);
        Assert.Equal(Character.InitialLevel, character.Level);
        Assert.Equal(Character.InitialExperience, character.Experience);
        Assert.Equal(1, character.MapId.Value);
        Assert.Equal(4, character.Position.X);
        Assert.Equal(7, character.Position.Y);
        Assert.Equal(Now, character.CreatedAt);
        Assert.Equal(Now, character.UpdatedAt);
    }

    [Fact]
    public void Name_is_trimmed()
    {
        var character = CreateCharacter("  Aragorn  ");

        Assert.Equal("Aragorn", character.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_is_rejected(string name)
    {
        Assert.Throws<DomainException>(() => CreateCharacter(name));
    }

    [Fact]
    public void Name_longer_than_the_limit_is_rejected()
    {
        var name = new string('a', Character.MaxNameLength + 1);

        Assert.Throws<DomainException>(() => CreateCharacter(name));
    }

    [Fact]
    public void Name_at_the_limit_is_accepted()
    {
        var name = new string('a', Character.MaxNameLength);

        var character = CreateCharacter(name);

        Assert.Equal(Character.MaxNameLength, character.Name.Length);
    }

    [Fact]
    public void Undefined_class_is_rejected()
    {
        Assert.Throws<DomainException>(() => CreateCharacter(characterClass: (CharacterClass)99));
    }

    [Fact]
    public void Empty_account_is_rejected()
    {
        Assert.Throws<DomainException>(() =>
            Character.Create(AccountId.Empty, "Aragorn", CharacterClass.Warrior, Start, Now));
    }

    [Fact]
    public void Valid_state_transition_is_applied()
    {
        var character = CreateCharacter();
        var later = Now.AddSeconds(30);

        character.ChangeState(CharacterState.Loading, later);

        Assert.Equal(CharacterState.Loading, character.State);
        Assert.Equal(later, character.UpdatedAt);
    }

    [Fact]
    public void Invalid_state_transition_is_rejected()
    {
        var character = CreateCharacter();

        Assert.Throws<DomainException>(() => character.ChangeState(CharacterState.Combat, Now));
    }

    [Fact]
    public void Undefined_state_is_rejected()
    {
        var character = CreateCharacter();

        Assert.Throws<DomainException>(() => character.ChangeState((CharacterState)99, Now));
    }

    [Fact]
    public void Same_state_transition_is_a_no_op()
    {
        var character = CreateCharacter();

        character.ChangeState(CharacterState.Offline, Now.AddMinutes(1));

        Assert.Equal(CharacterState.Offline, character.State);
        Assert.Equal(Now, character.UpdatedAt);
    }
}
