using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ether.Infrastructure.Tests.Persistence;

public sealed class EtherDbContextModelTests
{
    private const string DesignConnectionString =
        "Host=localhost;Database=ether;Username=ether;Password=ether";

    /// <summary>
    /// Uses the design-time model so configuration such as check constraints is
    /// available (the runtime model is read-optimized and omits it).
    /// </summary>
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<EtherDbContext>()
            .UseNpgsql(DesignConnectionString)
            .Options;

        using var context = new EtherDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Model_maps_only_account_and_character()
    {
        var model = CreateModel();

        var entityTypes = model.GetEntityTypes().Select(entity => entity.ClrType).ToList();

        Assert.Equal(2, entityTypes.Count);
        Assert.Contains(typeof(Account), entityTypes);
        Assert.Contains(typeof(Character), entityTypes);
    }

    [Fact]
    public void Account_is_mapped_to_expected_table_and_columns()
    {
        var account = CreateModel().FindEntityType(typeof(Account))!;

        Assert.Equal("accounts", account.GetTableName());
        Assert.Equal("PK_accounts", account.FindPrimaryKey()!.GetName());

        var columns = account.GetProperties().Select(property => property.GetColumnName()).ToList();
        Assert.Contains("id", columns);
        Assert.Contains("status", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);
    }

    [Fact]
    public void Character_is_mapped_to_expected_table_and_columns()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;

        Assert.Equal("characters", character.GetTableName());

        var columns = character.GetProperties().Select(property => property.GetColumnName()).ToList();
        Assert.Contains("id", columns);
        Assert.Contains("account_id", columns);
        Assert.Contains("name", columns);
        Assert.Contains("class_id", columns);
        Assert.Contains("state", columns);
        Assert.Contains("level", columns);
        Assert.Contains("experience", columns);
        Assert.Contains("map_id", columns);
        Assert.Contains("position_x", columns);
        Assert.Contains("position_y", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);

        // Position is a domain reconstruction, not a persisted column.
        Assert.DoesNotContain(character.GetProperties(), property => property.Name == nameof(Character.Position));
    }

    [Fact]
    public void Character_name_has_max_length()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;
        var name = character.FindProperty(nameof(Character.Name))!;

        Assert.Equal(Character.MaxNameLength, name.GetMaxLength());
        Assert.False(name.IsNullable);
    }

    [Fact]
    public void Character_name_has_a_unique_index()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;

        var uniqueIndex = character.GetIndexes()
            .Single(index => index.GetDatabaseName() == "ux_characters_name");

        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(nameof(Character.Name), Assert.Single(uniqueIndex.Properties).Name);
    }

    [Fact]
    public void Character_has_an_account_index()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;

        Assert.Contains(character.GetIndexes(), index => index.GetDatabaseName() == "ix_characters_account_id");
    }

    [Fact]
    public void Character_has_a_restricted_foreign_key_to_account()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;

        var foreignKey = Assert.Single(character.GetForeignKeys());

        Assert.Equal("accounts", foreignKey.PrincipalEntityType.GetTableName());
        Assert.Equal(nameof(Character.AccountId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Character_has_the_expected_check_constraints()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;
        var storeObject = StoreObjectIdentifier.Table("characters", null);

        var constraints = character.GetCheckConstraints()
            .Select(constraint => constraint.GetName(storeObject))
            .ToList();

        Assert.Contains("ck_characters_level", constraints);
        Assert.Contains("ck_characters_experience", constraints);
        Assert.Contains("ck_characters_map_id", constraints);
        Assert.Contains("ck_characters_name_not_blank", constraints);
        Assert.Contains("ck_characters_max_health_positive", constraints);
        Assert.Contains("ck_characters_health_non_negative", constraints);
        Assert.Contains("ck_characters_health_within_max", constraints);
    }

    [Fact]
    public void Character_health_columns_have_defaults()
    {
        var character = CreateModel().FindEntityType(typeof(Character))!;

        var maxHealth = character.FindProperty(nameof(Character.MaxHealth))!;
        var health = character.FindProperty(nameof(Character.Health))!;

        Assert.Equal("max_health", maxHealth.GetColumnName());
        Assert.Equal("health", health.GetColumnName());
        Assert.Equal(Character.DefaultMaxHealth, maxHealth.GetDefaultValue());
        Assert.Equal(Character.DefaultMaxHealth, health.GetDefaultValue());
    }
}
