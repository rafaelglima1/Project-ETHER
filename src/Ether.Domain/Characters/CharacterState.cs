namespace Ether.Domain.Characters;

/// <summary>
/// Lifecycle state of a character (Blueprint v5.0 §8).
/// Transitions between states are validated by the Character aggregate (M1+).
/// </summary>
public enum CharacterState
{
    Offline = 0,
    Loading = 1,
    InWorld = 2,
    Combat = 3,
    Dead = 4,
    Respawning = 5,
    DisconnectGrace = 6,
}
