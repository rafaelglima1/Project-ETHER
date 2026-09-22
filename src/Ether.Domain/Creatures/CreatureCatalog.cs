using Ether.Domain.Combat;

namespace Ether.Domain.Creatures;

/// <summary>
/// M6 creature catalog for the first playable world. Content-defined data replaces
/// this catalog when the content pipeline lands (same convention as abilities).
/// </summary>
public static class CreatureCatalog
{
    private static readonly IReadOnlyDictionary<DamageType, double> None = new Dictionary<DamageType, double>();

    private static readonly Dictionary<CreatureDefinitionId, CreatureDefinition> Definitions = new()
    {
        [new CreatureDefinitionId("creature.slime")] = new CreatureDefinition(
            new CreatureDefinitionId("creature.slime"),
            "Slime",
            level: 1,
            maxHealth: 30,
            attackPower: 5,
            armor: 1,
            moveSpeed: 1,
            aggroRange: 6,
            attackRange: 1,
            attackCooldown: TimeSpan.FromSeconds(2),
            leashRange: 10,
            respawnDelay: TimeSpan.FromSeconds(30),
            resistances: None),

        [new CreatureDefinitionId("creature.wolf")] = new CreatureDefinition(
            new CreatureDefinitionId("creature.wolf"),
            "Wolf",
            level: 2,
            maxHealth: 45,
            attackPower: 8,
            armor: 2,
            moveSpeed: 2,
            aggroRange: 8,
            attackRange: 1,
            attackCooldown: TimeSpan.FromSeconds(1.5),
            leashRange: 14,
            respawnDelay: TimeSpan.FromSeconds(45),
            resistances: None),

        [new CreatureDefinitionId("creature.spider")] = new CreatureDefinition(
            new CreatureDefinitionId("creature.spider"),
            "Spider",
            level: 2,
            maxHealth: 35,
            attackPower: 7,
            armor: 1,
            moveSpeed: 2,
            aggroRange: 7,
            attackRange: 2,
            attackCooldown: TimeSpan.FromSeconds(2),
            leashRange: 12,
            respawnDelay: TimeSpan.FromSeconds(40),
            resistances: None),
    };

    public static bool TryGet(CreatureDefinitionId id, out CreatureDefinition definition) =>
        Definitions.TryGetValue(id, out definition!);

    public static CreatureDefinition Get(CreatureDefinitionId id) =>
        Definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new Common.DomainException($"Creature definition '{id.Value}' does not exist.");

    public static IReadOnlyCollection<CreatureDefinition> All => Definitions.Values;
}
