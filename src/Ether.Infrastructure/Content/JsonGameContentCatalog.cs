using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Combat;
using Ether.Domain.Creatures;
using Ether.Domain.Items;
using Ether.Domain.Loot;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Content;

/// <summary>
/// Loads the versioned JSON content bundle once, validates its definitions and
/// cross-references, and exposes immutable read catalogs to application services.
/// Domain rules remain in the domain constructors; this adapter owns file I/O.
/// </summary>
public sealed class JsonGameContentCatalog :
    IAbilityCatalog,
    ICreatureCatalog,
    ICreatureSpawnCatalog,
    IItemCatalog,
    ILootTableCatalog,
    IWorldMapProvider
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly IReadOnlyDictionary<AbilityId, AbilityDefinition> _abilities;
    private readonly IReadOnlyDictionary<CreatureDefinitionId, CreatureDefinition> _creatures;
    private readonly IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> _items;
    private readonly IReadOnlyDictionary<CreatureDefinitionId, LootTableDefinition> _lootTables;
    private readonly IReadOnlyDictionary<MapId, WorldMap> _maps;
    private readonly IReadOnlyDictionary<MapId, IReadOnlyList<CreatureSpawnDefinition>> _spawns;

    public JsonGameContentCatalog(IOptions<ContentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ContentValidationException("Content:RootPath must not be empty.");
        }

        var rootPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

        var manifest = ReadRequired<ContentManifest>(rootPath, "manifest.json");
        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            throw new ContentValidationException(
                $"Unsupported content schemaVersion {manifest.SchemaVersion}; supported version is {SupportedSchemaVersion}.");
        }

        if (manifest.ContentVersion < 1)
        {
            throw new ContentValidationException("Content manifest contentVersion must be at least 1.");
        }

        var maps = BuildMaps(ReadRequired<MapDto[]>(rootPath, "maps/maps.json"));
        var items = BuildItems(ReadRequired<ItemDto[]>(rootPath, "items/items.json"));
        var abilities = BuildAbilities(ReadRequired<AbilityDto[]>(rootPath, "abilities/abilities.json"));
        var creatures = BuildCreatures(ReadRequired<CreatureDto[]>(rootPath, "creatures/creatures.json"));
        var lootTables = BuildLootTables(
            ReadRequired<LootTableDto[]>(rootPath, "loot-tables/loot-tables.json"),
            creatures,
            items);
        var spawns = BuildSpawns(
            ReadRequired<CreatureSpawnDto[]>(rootPath, "spawns/creature-spawns.json"),
            maps,
            creatures);

        _maps = maps;
        _items = items;
        _abilities = abilities;
        _creatures = creatures;
        _lootTables = lootTables;
        _spawns = spawns;
        ContentVersion = manifest.ContentVersion;
    }

    public int ContentVersion { get; }

    public IReadOnlyCollection<AbilityDefinition> AllAbilities => _abilities.Values.ToArray();

    public IReadOnlyCollection<CreatureDefinition> AllCreatures => _creatures.Values.ToArray();

    public IReadOnlyCollection<ItemDefinition> AllItems => _items.Values.ToArray();

    public IReadOnlyCollection<LootTableDefinition> AllLootTables => _lootTables.Values.ToArray();

    public IReadOnlyCollection<WorldMap> AllMaps => _maps.Values.ToArray();

    public bool TryGet(AbilityId id, out AbilityDefinition definition) => _abilities.TryGetValue(id, out definition!);

    public AbilityDefinition GetAbility(AbilityId id) =>
        _abilities.TryGetValue(id, out var definition)
            ? definition
            : throw new ContentValidationException($"Ability '{id.Value}' is not defined.");

    public CreatureDefinition Get(CreatureDefinitionId id) =>
        _creatures.TryGetValue(id, out var definition)
            ? definition
            : throw new ContentValidationException($"Creature '{id.Value}' is not defined.");

    public ItemDefinition Get(ItemDefinitionId id) =>
        _items.TryGetValue(id, out var definition)
            ? definition
            : throw new ContentValidationException($"Item '{id.Value}' is not defined.");

    public LootTableDefinition? For(CreatureDefinitionId creatureDefinitionId) =>
        _lootTables.TryGetValue(creatureDefinitionId, out var table) ? table : null;

    public WorldMap GetMap(MapId mapId) =>
        _maps.TryGetValue(mapId, out var map)
            ? map
            : throw new ContentValidationException($"Map '{mapId.Value}' is not defined.");

    public IReadOnlyList<CreatureSpawnDefinition> ForMap(MapId mapId) =>
        _spawns.TryGetValue(mapId, out var spawns) ? spawns : Array.Empty<CreatureSpawnDefinition>();

    private static IReadOnlyDictionary<MapId, WorldMap> BuildMaps(IEnumerable<MapDto> source)
    {
        var maps = new Dictionary<MapId, WorldMap>();
        foreach (var dto in source)
        {
            var mapId = CreateDefinition("map", dto.Id.ToString(), () => new MapId(dto.Id));
            if (!maps.TryAdd(mapId, CreateDefinition("map", dto.Id.ToString(), () => new WorldMap(mapId, dto.Width, dto.Height))))
            {
                throw DuplicateId("map", dto.Id.ToString());
            }
        }

        RequireAny(maps.Count, "maps/maps.json");
        return maps;
    }

    private static IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> BuildItems(IEnumerable<ItemDto> source)
    {
        var items = new Dictionary<ItemDefinitionId, ItemDefinition>();
        foreach (var dto in source)
        {
            var id = CreateDefinition("item", dto.Id, () => new ItemDefinitionId(dto.Id));
            var definition = CreateDefinition("item", dto.Id, () => new ItemDefinition(
                id,
                dto.Name,
                ParseEnum<ItemCategory>(dto.Category, "item", dto.Id, "category"),
                dto.Stackable,
                dto.MaxStack,
                dto.BaseValue,
                ParseEnum<ItemRarity>(dto.Rarity, "item", dto.Id, "rarity")));

            if (!items.TryAdd(id, definition))
            {
                throw DuplicateId("item", dto.Id);
            }
        }

        RequireAny(items.Count, "items/items.json");
        return items;
    }

    private static IReadOnlyDictionary<AbilityId, AbilityDefinition> BuildAbilities(IEnumerable<AbilityDto> source)
    {
        var abilities = new Dictionary<AbilityId, AbilityDefinition>();
        foreach (var dto in source)
        {
            var id = CreateDefinition("ability", dto.Id, () => new AbilityId(dto.Id));
            ValidateFinite(dto.BaseDamage, "ability", dto.Id, "baseDamage");
            ValidateFinite(dto.PowerScaling, "ability", dto.Id, "powerScaling");
            var definition = CreateDefinition("ability", dto.Id, () => new AbilityDefinition(
                id,
                dto.Name,
                ParseEnum<DamageType>(dto.DamageType, "ability", dto.Id, "damageType"),
                dto.BaseDamage,
                dto.PowerScaling,
                TimeSpan.FromMilliseconds(dto.CooldownMilliseconds),
                dto.Range));

            if (!abilities.TryAdd(id, definition))
            {
                throw DuplicateId("ability", dto.Id);
            }
        }

        RequireAny(abilities.Count, "abilities/abilities.json");
        return abilities;
    }

    private static IReadOnlyDictionary<CreatureDefinitionId, CreatureDefinition> BuildCreatures(IEnumerable<CreatureDto> source)
    {
        var creatures = new Dictionary<CreatureDefinitionId, CreatureDefinition>();
        foreach (var dto in source)
        {
            var id = CreateDefinition("creature", dto.Id, () => new CreatureDefinitionId(dto.Id));
            ValidateFinite(dto.AttackPower, "creature", dto.Id, "attackPower");
            ValidateFinite(dto.Armor, "creature", dto.Id, "armor");
            ValidateFinite(dto.MoveSpeed, "creature", dto.Id, "moveSpeed");

            var resistances = new Dictionary<DamageType, double>();
            if (dto.Resistances is null)
            {
                throw InvalidValue("creature", dto.Id, "resistances", "must be an array (use [] when there are no resistances)");
            }

            if (dto.Resistances.Any(resistance => resistance is null))
            {
                throw InvalidValue("creature", dto.Id, "resistances", "must not contain null entries");
            }

            foreach (var resistance in dto.Resistances)
            {
                var damageType = ParseEnum<DamageType>(resistance.DamageType, "creature", dto.Id, "resistances.damageType");
                ValidateFinite(resistance.Value, "creature", dto.Id, "resistances.value");
                if (resistance.Value < 0)
                {
                    throw InvalidValue("creature", dto.Id, "resistances.value", "must not be negative");
                }

                if (!resistances.TryAdd(damageType, resistance.Value))
                {
                    throw InvalidValue("creature", dto.Id, "resistances", $"contains duplicate damage type '{damageType}'");
                }
            }

            var definition = CreateDefinition("creature", dto.Id, () => new CreatureDefinition(
                id,
                dto.Name,
                dto.Level,
                dto.MaxHealth,
                dto.AttackPower,
                dto.Armor,
                dto.MoveSpeed,
                dto.AggroRange,
                dto.AttackRange,
                TimeSpan.FromMilliseconds(dto.AttackCooldownMilliseconds),
                dto.LeashRange,
                TimeSpan.FromMilliseconds(dto.RespawnDelayMilliseconds),
                new ReadOnlyDictionary<DamageType, double>(resistances),
                dto.ExperienceReward));

            if (!creatures.TryAdd(id, definition))
            {
                throw DuplicateId("creature", dto.Id);
            }
        }

        RequireAny(creatures.Count, "creatures/creatures.json");
        return creatures;
    }

    private static IReadOnlyDictionary<CreatureDefinitionId, LootTableDefinition> BuildLootTables(
        IEnumerable<LootTableDto> source,
        IReadOnlyDictionary<CreatureDefinitionId, CreatureDefinition> creatures,
        IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> items)
    {
        var tables = new Dictionary<CreatureDefinitionId, LootTableDefinition>();
        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in source)
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
            {
                throw InvalidValue("loot table", "<empty>", "id", "must not be empty");
            }

            if (!tableIds.Add(dto.Id))
            {
                throw DuplicateId("loot table", dto.Id);
            }

            var creatureId = CreateDefinition("loot table", dto.Id, () => new CreatureDefinitionId(dto.CreatureId));
            if (!creatures.ContainsKey(creatureId))
            {
                throw InvalidValue("loot table", dto.Id, "creatureId", $"references missing creature '{dto.CreatureId}'");
            }

            if (dto.Entries is null || dto.Entries.Length == 0)
            {
                throw InvalidValue("loot table", dto.Id, "entries", "must contain at least one entry");
            }

            if (dto.Entries.Any(entry => entry is null))
            {
                throw InvalidValue("loot table", dto.Id, "entries", "must not contain null entries");
            }

            var entries = dto.Entries.Select(entry =>
            {
                ValidateFinite(entry.Chance, "loot table", dto.Id, "entries.chance");
                var itemId = CreateDefinition("loot table", dto.Id, () => new ItemDefinitionId(entry.ItemId));
                if (!items.ContainsKey(itemId))
                {
                    throw InvalidValue("loot table", dto.Id, "entries.itemId", $"references missing item '{entry.ItemId}'");
                }

                return new LootEntry(itemId, entry.Chance, entry.MinQuantity, entry.MaxQuantity);
            }).ToArray();

            var table = CreateDefinition(
                "loot table",
                dto.Id,
                () => new LootTableDefinition(dto.Id, Array.AsReadOnly(entries)).Validate());
            if (!tables.TryAdd(creatureId, table))
            {
                throw InvalidValue("loot table", dto.Id, "creatureId", $"has more than one table for '{dto.CreatureId}'");
            }
        }

        return tables;
    }

    private static IReadOnlyDictionary<MapId, IReadOnlyList<CreatureSpawnDefinition>> BuildSpawns(
        IEnumerable<CreatureSpawnDto> source,
        IReadOnlyDictionary<MapId, WorldMap> maps,
        IReadOnlyDictionary<CreatureDefinitionId, CreatureDefinition> creatures)
    {
        var spawns = new Dictionary<MapId, List<CreatureSpawnDefinition>>();
        var occupiedTiles = new HashSet<(MapId MapId, int X, int Y)>();
        foreach (var dto in source)
        {
            var mapId = CreateDefinition("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", () => new MapId(dto.MapId));
            var creatureId = CreateDefinition("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", () => new CreatureDefinitionId(dto.CreatureId));
            if (!maps.TryGetValue(mapId, out var map))
            {
                throw InvalidValue("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", "mapId", $"references missing map '{dto.MapId}'");
            }

            if (!creatures.ContainsKey(creatureId))
            {
                throw InvalidValue("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", "creatureId", $"references missing creature '{dto.CreatureId}'");
            }

            var position = CreateDefinition("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}",
                () => new WorldPosition(mapId, dto.X, dto.Y));
            if (!map.Contains(position))
            {
                throw InvalidValue("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", "position", "is outside map bounds");
            }

            if (!occupiedTiles.Add((mapId, dto.X, dto.Y)))
            {
                throw InvalidValue("creature spawn", $"{dto.MapId}:{dto.X}:{dto.Y}", "position", "duplicates another spawn tile");
            }

            if (!spawns.TryGetValue(mapId, out var mapSpawns))
            {
                mapSpawns = [];
                spawns.Add(mapId, mapSpawns);
            }

            mapSpawns.Add(new CreatureSpawnDefinition(mapId, dto.X, dto.Y, creatureId));
        }

        var result = spawns.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CreatureSpawnDefinition>)pair.Value.AsReadOnly());
        RequireAny(result.Values.Sum(mapSpawns => mapSpawns.Count), "spawns/creature-spawns.json");
        return result;
    }

    private static T ReadRequired<T>(string rootPath, string relativePath)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new ContentValidationException($"Required content file '{relativePath}' was not found under '{rootPath}'.");
        }

        try
        {
            var content = JsonSerializer.Deserialize<T>(File.ReadAllBytes(fullPath), SerializerOptions);
            if (content is Array entries && entries.Cast<object?>().Any(entry => entry is null))
            {
                throw new ContentValidationException($"Content file '{relativePath}' contains a null entry.");
            }

            return content is null
                ? throw new ContentValidationException($"Content file '{relativePath}' contains JSON null.")
                : content;
        }
        catch (JsonException exception)
        {
            throw new ContentValidationException($"Content file '{relativePath}' is invalid JSON: {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new ContentValidationException($"Content file '{relativePath}' could not be read: {exception.Message}", exception);
        }
    }

    private static T CreateDefinition<T>(string type, string id, Func<T> create)
    {
        try
        {
            return create();
        }
        catch (Exception exception) when (exception is Ether.Domain.Common.DomainException or ArgumentException or OverflowException)
        {
            throw new ContentValidationException($"Invalid {type} '{id}': {exception.Message}", exception);
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, string type, string id, string property)
        where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw InvalidValue(type, id, property, $"'{value}' is not a defined {typeof(TEnum).Name}");

    private static void ValidateFinite(double value, string type, string id, string property)
    {
        if (!double.IsFinite(value))
        {
            throw InvalidValue(type, id, property, "must be a finite number");
        }
    }

    private static void RequireAny(int count, string file)
    {
        if (count == 0)
        {
            throw new ContentValidationException($"Content file '{file}' must contain at least one definition.");
        }
    }

    private static ContentValidationException DuplicateId(string type, string id) =>
        new($"Duplicate {type} id '{id}'.");

    private static ContentValidationException InvalidValue(string type, string id, string property, string reason) =>
        new($"Invalid {type} '{id}' property '{property}': {reason}.");

    private sealed class ContentManifest
    {
        public required int SchemaVersion { get; init; }

        public required int ContentVersion { get; init; }
    }

    private sealed class MapDto
    {
        public required int Id { get; init; }

        public required int Width { get; init; }

        public required int Height { get; init; }
    }

    private sealed class ItemDto
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string Category { get; init; }

        public required bool Stackable { get; init; }

        public required int MaxStack { get; init; }

        public required long BaseValue { get; init; }

        public required string Rarity { get; init; }
    }

    private sealed class AbilityDto
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string DamageType { get; init; }

        public required double BaseDamage { get; init; }

        public required double PowerScaling { get; init; }

        public required int CooldownMilliseconds { get; init; }

        public required int Range { get; init; }
    }

    private sealed class CreatureDto
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required int Level { get; init; }

        public required int MaxHealth { get; init; }

        public required double AttackPower { get; init; }

        public required double Armor { get; init; }

        public required double MoveSpeed { get; init; }

        public required int AggroRange { get; init; }

        public required int AttackRange { get; init; }

        public required int AttackCooldownMilliseconds { get; init; }

        public required int LeashRange { get; init; }

        public required int RespawnDelayMilliseconds { get; init; }

        public required long ExperienceReward { get; init; }

        public required ResistanceDto[]? Resistances { get; init; }
    }

    private sealed class ResistanceDto
    {
        public required string DamageType { get; init; }

        public required double Value { get; init; }
    }

    private sealed class LootTableDto
    {
        public required string Id { get; init; }

        public required string CreatureId { get; init; }

        public required LootEntryDto[]? Entries { get; init; }
    }

    private sealed class LootEntryDto
    {
        public required string ItemId { get; init; }

        public required double Chance { get; init; }

        public required int MinQuantity { get; init; }

        public required int MaxQuantity { get; init; }
    }

    private sealed class CreatureSpawnDto
    {
        public required int MapId { get; init; }

        public required int X { get; init; }

        public required int Y { get; init; }

        public required string CreatureId { get; init; }
    }
}
