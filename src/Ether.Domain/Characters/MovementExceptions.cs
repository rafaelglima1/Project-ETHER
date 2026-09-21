namespace Ether.Domain.Characters;

using Ether.Domain.Common;

/// <summary>Raised when a movement destination lies outside the current map.</summary>
public sealed class MovementOutOfBoundsException : DomainException
{
    public MovementOutOfBoundsException(string message) : base(message)
    {
    }
}

/// <summary>Raised when a movement destination exceeds the allowed distance.</summary>
public sealed class MovementTooFarException : DomainException
{
    public MovementTooFarException(string message) : base(message)
    {
    }
}

/// <summary>Raised when a character that is not in the world tries to move.</summary>
public sealed class CharacterNotInWorldException : DomainException
{
    public CharacterNotInWorldException(string message) : base(message)
    {
    }
}
