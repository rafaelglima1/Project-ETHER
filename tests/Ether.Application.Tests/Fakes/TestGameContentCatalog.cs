using Ether.Application.Abstractions;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;
using Ether.Domain.Loot;
using Ether.Domain.World;

namespace Ether.Application.Tests.Fakes;

/// <summary>Small deterministic content fixture for application use-case tests.</summary>
internal sealed class TestGameContentCatalog :
    IAbilityCatalog,
    ICreatureCatalog,
    ICreatureSpawnCatalog,
    IItemCatalog,
    ILootTableCatalog
{
    public static readonly ItemDefinition SlimeGel = new(
        new ItemDefinitionId("item.slime_gel"), "Slime Gel", ItemCategory.Material, true, 100, 2);

    public static readonly ItemDefinition WolfPelt = new(
        new ItemDefinitionId("item.wolf_pelt"), "Wolf Pelt", ItemCategory.Material, true, 100, 6);

    public static readonly ItemDefinition SpiderSilk = new(
        new ItemDefinitionId("item.spider_silk"), "Spider Silk", ItemCategory.Material, true, 100, 5);

    public static readonly ItemDefinition HealthPotion = new(
        new ItemDefinitionId("item.health_potion"), "Health Potion", ItemCategory.Consumable, true, 20, 15);

    public static readonly ItemDefinition CopperOre = new(
        new ItemDefinitionId("item.copper_ore"), "Copper Ore", ItemCategory.Material, true, 100, 3);

    private static readonly AbilityId BasicAttackId = new("warrior.basic_attack");
    private static readonly AbilityId PowerStrikeId = new("warrior.power_strike");

    private static readonly CreatureDefinition Slime = Creature(
        "creature.slime", "Slime", level: 1, health: 30, attack: 5, armor: 1, speed: 1,
        aggro: 6, range: 1, cooldownMs: 2000, leash: 10, respawnMs: 30000, xp: 12);

    private static readonly CreatureDefinition Wolf = Creature(
        "creature.wolf", "Wolf", level: 2, health: 45, attack: 8, armor: 2, speed: 2,
        aggro: 8, range: 1, cooldownMs: 1500, leash: 14, respawnMs: 45000, xp: 25);

    private static readonly CreatureDefinition Spider = Creature(
        "creature.spider", "Spider", level: 2, health: 35, attack: 7, armor: 1, speed: 2,
        aggro: 7, range: 2, cooldownMs: 2000, leash: 12, respawnMs: 40000, xp: 22);

    private readonly Dictionary<AbilityId, AbilityDefinition> _abilities = new()
    {
        [BasicAttackId] = new(BasicAttackId, "Basic Attack", DamageType.Physical, 4, 1, TimeSpan.Zero, 1),
        [PowerStrikeId] = new(PowerStrikeId, "Power Strike", DamageType.Physical, 8, 1.5, TimeSpan.FromSeconds(6), 1),
    };

    private readonly Dictionary<CreatureDefinitionId, CreatureDefinition> _creatures = new()
    {
        [Slime.Id] = Slime,
        [Wolf.Id] = Wolf,
        [Spider.Id] = Spider,
    };

    private readonly Dictionary<ItemDefinitionId, ItemDefinition> _items = new()
    {
        [SlimeGel.Id] = SlimeGel,
        [WolfPelt.Id] = WolfPelt,
        [SpiderSilk.Id] = SpiderSilk,
        [HealthPotion.Id] = HealthPotion,
        [CopperOre.Id] = CopperOre,
    };

    private readonly Dictionary<CreatureDefinitionId, LootTableDefinition> _loot = new()
    {
        [Slime.Id] = new LootTableDefinition("loot.slime", [new(SlimeGel.Id, 0.8, 1, 2)]).Validate(),
        [Wolf.Id] = new LootTableDefinition("loot.wolf", [new(WolfPelt.Id, 0.7, 1, 1), new(HealthPotion.Id, 0.2, 1, 1)]).Validate(),
        [Spider.Id] = new LootTableDefinition("loot.spider", [new(SpiderSilk.Id, 0.75, 1, 2), new(CopperOre.Id, 0.3, 1, 1)]).Validate(),
    };

    private static readonly IReadOnlyList<CreatureSpawnDefinition> Spawns =
    [
        new(new MapId(1), 6, 6, Slime.Id),
        new(new MapId(1), 9, 4, Slime.Id),
        new(new MapId(1), 14, 11, Wolf.Id),
        new(new MapId(1), 22, 18, Spider.Id),
    ];

    public static TestGameContentCatalog Instance { get; } = new();

    public IReadOnlyCollection<AbilityDefinition> AllAbilities => _abilities.Values;

    public IReadOnlyCollection<CreatureDefinition> AllCreatures => _creatures.Values;

    public IReadOnlyCollection<ItemDefinition> AllItems => _items.Values;

    public IReadOnlyCollection<LootTableDefinition> AllLootTables => _loot.Values;

    public bool TryGet(AbilityId id, out AbilityDefinition definition) => _abilities.TryGetValue(id, out definition!);

    public CreatureDefinition Get(CreatureDefinitionId id) =>
        _creatures.TryGetValue(id, out var definition)
            ? definition
            : throw new Ether.Domain.Common.DomainException($"Unknown test creature '{id.Value}'.");

    public ItemDefinition Get(ItemDefinitionId id) =>
        _items.TryGetValue(id, out var definition)
            ? definition
            : throw new Ether.Domain.Common.DomainException($"Unknown test item '{id.Value}'.");

    public LootTableDefinition? For(CreatureDefinitionId creatureDefinitionId) =>
        _loot.TryGetValue(creatureDefinitionId, out var table) ? table : null;

    public IReadOnlyList<CreatureSpawnDefinition> ForMap(MapId mapId) =>
        Spawns.Where(spawn => spawn.MapId == mapId).ToList();

    private static CreatureDefinition Creature(
        string id,
        string name,
        int level,
        int health,
        double attack,
        double armor,
        double speed,
        int aggro,
        int range,
        int cooldownMs,
        int leash,
        int respawnMs,
        long xp) =>
        new(
            new CreatureDefinitionId(id), name, level, health, attack, armor, speed, aggro, range,
            TimeSpan.FromMilliseconds(cooldownMs), leash, TimeSpan.FromMilliseconds(respawnMs),
            new Dictionary<DamageType, double>(), xp);
}
