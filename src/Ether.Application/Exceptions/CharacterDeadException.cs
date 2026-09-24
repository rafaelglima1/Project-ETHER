namespace Ether.Application.Exceptions;

/// <summary>
/// Raised when an operation requires a living character but the character is dead
/// (or respawning). Mapped to a deterministic protocol/HTTP error, never a 500.
/// </summary>
public sealed class CharacterDeadException : Exception
{
    public CharacterDeadException()
        : base("Character is dead and must respawn before entering the world.")
    {
    }
}
