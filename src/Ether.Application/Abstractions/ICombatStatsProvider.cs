using Ether.Domain.Combat;
using Ether.Domain.Characters;

namespace Ether.Application.Abstractions;

/// <summary>Resolves the combat statistics of a character.</summary>
public interface ICombatStatsProvider
{
    CombatStats For(Character character);
}
