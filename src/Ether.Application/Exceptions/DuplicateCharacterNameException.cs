namespace Ether.Application.Exceptions;

/// <summary>
/// Raised when a character name is already taken.
/// Character names are globally unique and the guarantee is enforced by a unique
/// database constraint; this exception is the mapped outcome of that violation.
/// </summary>
public sealed class DuplicateCharacterNameException : Exception
{
    public DuplicateCharacterNameException(string? name = null)
        : base(name is null
            ? "Character name is already in use."
            : $"Character name '{name}' is already in use.")
    {
        Name = name;
    }

    public string? Name { get; }
}
