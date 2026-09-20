using Ether.Domain.Characters;

namespace Ether.Application.Exceptions;

/// <summary>Raised when an operation references a character that does not exist.</summary>
public sealed class CharacterNotFoundException : Exception
{
    public CharacterNotFoundException(CharacterId characterId)
        : base($"Character '{characterId.Value}' was not found.")
    {
        CharacterId = characterId;
    }

    public CharacterId CharacterId { get; }
}
