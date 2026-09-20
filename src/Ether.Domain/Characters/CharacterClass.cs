namespace Ether.Domain.Characters;

/// <summary>
/// Character class (Blueprint v5.0 §10).
/// The domain supports all three classes structurally; only <see cref="Warrior"/>
/// is implemented in the MVP.
/// </summary>
public enum CharacterClass
{
    Warrior = 0,
    Ranger = 1,
    Arcanist = 2,
}
