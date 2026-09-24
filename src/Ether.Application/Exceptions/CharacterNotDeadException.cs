namespace Ether.Application.Exceptions;

/// <summary>Raised when a respawn is requested for a character that is not dead.</summary>
public sealed class CharacterNotDeadException : Exception
{
    public CharacterNotDeadException()
        : base("Character is not dead and cannot respawn.")
    {
    }
}
