using System.Text.Json;

using Ether.Contracts.Configuration;
using Ether.Infrastructure.Content;
using Ether.Infrastructure.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Tests.Content;

public sealed class JsonGameContentCatalogTests
{
    [Fact]
    public void Loads_the_existing_content_without_changing_gameplay_values()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        var content = CreateCatalog(contentDirectory.Path);

        Assert.Equal(1, content.ContentVersion);
        Assert.Equal(2, content.AllAbilities.Count);
        Assert.Equal(3, content.AllCreatures.Count);
        Assert.Equal(5, content.AllItems.Count);
        Assert.Equal(3, content.AllLootTables.Count);
        Assert.Single(content.AllMaps);
        Assert.Equal(4, content.ForMap(new Ether.Domain.World.MapId(1)).Count);
        Assert.Equal(32, content.GetMap(new Ether.Domain.World.MapId(1)).Width);
        Assert.Equal(4, content.GetAbility(new Ether.Domain.Combat.AbilityId("warrior.basic_attack")).BaseDamage);
        Assert.Equal(12, content.Get(new Ether.Domain.Creatures.CreatureDefinitionId("creature.slime")).ExperienceReward);
        Assert.Equal(100, content.Get(new Ether.Domain.Items.ItemDefinitionId("item.slime_gel")).MaxStack);
    }

    [Fact]
    public void Gameplay_balance_is_read_from_content_files()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("abilities/abilities.json", "\"baseDamage\": 4", "\"baseDamage\": 5");

        var content = CreateCatalog(contentDirectory.Path);

        Assert.Equal(5, content.GetAbility(new Ether.Domain.Combat.AbilityId("warrior.basic_attack")).BaseDamage);
    }

    [Fact]
    public void Empty_loot_table_collection_is_valid_for_content_without_drops()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        File.WriteAllText(System.IO.Path.Combine(contentDirectory.Path, "loot-tables", "loot-tables.json"), "[]");

        var content = CreateCatalog(contentDirectory.Path);

        Assert.Empty(content.AllLootTables);
        Assert.Null(content.For(new Ether.Domain.Creatures.CreatureDefinitionId("creature.slime")));
    }

    [Fact]
    public void Published_json_schemas_are_valid_and_match_the_supported_schema_version()
    {
        var schemaDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "content", "schemas");
        var schemas = Directory.GetFiles(schemaDirectory, "*.schema.json");

        Assert.Equal(7, schemas.Length);
        foreach (var schemaFile in schemas)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(schemaFile));
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document.RootElement.GetProperty("$schema").GetString());
            Assert.True(document.RootElement.TryGetProperty("additionalProperties", out _) ||
                        document.RootElement.GetProperty("items").GetProperty("additionalProperties").GetBoolean() == false);
        }
    }

    [Fact]
    public void Rejects_duplicate_definition_ids()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("abilities/abilities.json", "warrior.power_strike", "warrior.basic_attack");

        var exception = Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));

        Assert.Contains("Duplicate ability id 'warrior.basic_attack'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_loot_references_to_missing_items()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("loot-tables/loot-tables.json", "item.slime_gel", "item.missing");

        var exception = Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));

        Assert.Contains("references missing item 'item.missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_spawns_referencing_missing_creatures()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("spawns/creature-spawns.json", "creature.spider", "creature.missing");

        var exception = Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));

        Assert.Contains("references missing creature 'creature.missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_spawns_outside_map_bounds()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("spawns/creature-spawns.json", "\"x\": 22", "\"x\": 32");

        var exception = Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));

        Assert.Contains("is outside map bounds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_unknown_json_properties()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("maps/maps.json", "\"height\": 32", "\"height\": 32, \"walkable\": true");

        Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));
    }

    [Fact]
    public void Rejects_missing_required_content_files()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        File.Delete(System.IO.Path.Combine(contentDirectory.Path, "items", "items.json"));

        var exception = Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));

        Assert.Contains("Required content file 'items/items.json'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_invalid_drop_probabilities()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("loot-tables/loot-tables.json", "\"chance\": 0.8", "\"chance\": 1.5");

        Assert.Throws<ContentValidationException>(() => CreateCatalog(contentDirectory.Path));
    }

    [Fact]
    public async Task Invalid_content_fails_host_startup()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("maps/maps.json", "\"width\": 32", "\"width\": 0");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Content:RootPath"] = contentDirectory.Path,
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddEtherInfrastructure(configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<ContentValidationException>(() => host.StartAsync());

        Assert.Contains("Invalid map '1'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_configured_start_location_fails_host_startup()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Content:RootPath"] = contentDirectory.Path,
                ["Character:StartingX"] = "32",
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddEtherInfrastructure(configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<ContentValidationException>(() => host.StartAsync());

        Assert.Contains("outside map '1'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_required_warrior_ability_fails_host_startup()
    {
        using var contentDirectory = new TemporaryContentDirectory();
        contentDirectory.Replace("abilities/abilities.json", "warrior.basic_attack", "warrior.renamed_attack");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Content:RootPath"] = contentDirectory.Path,
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddEtherInfrastructure(configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<ContentValidationException>(() => host.StartAsync());

        Assert.Contains("Required Warrior ability 'warrior.basic_attack' is missing", exception.Message, StringComparison.Ordinal);
    }

    private static JsonGameContentCatalog CreateCatalog(string contentPath) =>
        new(Options.Create(new ContentOptions { RootPath = contentPath }));

    private sealed class TemporaryContentDirectory : IDisposable
    {
        public TemporaryContentDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ether-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);

            var sourcePath = System.IO.Path.Combine(AppContext.BaseDirectory, "content");
            foreach (var sourceFile in Directory.GetFiles(sourcePath, "*.json", SearchOption.AllDirectories))
            {
                var relativePath = System.IO.Path.GetRelativePath(sourcePath, sourceFile);
                var destination = System.IO.Path.Combine(Path, relativePath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                File.Copy(sourceFile, destination);
            }
        }

        public string Path { get; }

        public void Replace(string relativePath, string oldValue, string newValue)
        {
            var filePath = System.IO.Path.Combine(Path, relativePath);
            var contents = File.ReadAllText(filePath);
            Assert.Contains(oldValue, contents, StringComparison.Ordinal);
            File.WriteAllText(filePath, contents.Replace(oldValue, newValue, StringComparison.Ordinal));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
