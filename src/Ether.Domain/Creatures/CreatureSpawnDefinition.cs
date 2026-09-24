using Ether.Domain.World;

namespace Ether.Domain.Creatures;

/// <summary>Where a creature spawns. Spawn placement is content, not simulation state.</summary>
public sealed record CreatureSpawnDefinition(MapId MapId, int X, int Y, CreatureDefinitionId DefinitionId);
