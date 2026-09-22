namespace Ether.Domain.Creatures;

/// <summary>
/// Creature AI/lifecycle states (Blueprint v2.0 §38). Distinct from
/// <see cref="Characters.CharacterState"/> because creatures have their own lifecycle.
/// </summary>
public enum CreatureState
{
    Idle = 0,
    Patrol = 1,
    Investigate = 2,
    Chase = 3,
    Attack = 4,
    Flee = 5,
    Return = 6,
    Dead = 7,
    Respawning = 8,
}
