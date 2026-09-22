using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ether.Infrastructure.Persistence.Configurations;

internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public const string UniqueNameIndex = "ux_characters_name";
    public const string AccountIdIndex = "ix_characters_account_id";

    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("characters", table =>
        {
            table.HasCheckConstraint("ck_characters_level", "level >= 1");
            table.HasCheckConstraint("ck_characters_experience", "experience >= 0");
            table.HasCheckConstraint("ck_characters_map_id", "map_id >= 1");
            table.HasCheckConstraint("ck_characters_name_not_blank", "char_length(btrim(name)) > 0");
            table.HasCheckConstraint("ck_characters_max_health_positive", "max_health >= 1");
            table.HasCheckConstraint("ck_characters_health_non_negative", "health >= 0");
            table.HasCheckConstraint("ck_characters_health_within_max", "health <= max_health");
        });

        builder.HasKey(character => character.Id);

        builder.Property(character => character.Id)
            .HasColumnName("id")
            .HasConversion(DomainValueConverters.CharacterId);

        builder.Property(character => character.AccountId)
            .HasColumnName("account_id")
            .HasConversion(DomainValueConverters.AccountId);

        builder.Property(character => character.Name)
            .HasColumnName("name")
            .HasMaxLength(Character.MaxNameLength)
            .IsRequired();

        builder.Property(character => character.Class)
            .HasColumnName("class_id")
            .HasConversion<int>();

        builder.Property(character => character.State)
            .HasColumnName("state")
            .HasConversion<int>();

        builder.Property(character => character.Level)
            .HasColumnName("level");

        builder.Property(character => character.Experience)
            .HasColumnName("experience");

        builder.Property(character => character.MaxHealth)
            .HasColumnName("max_health")
            .HasDefaultValue(Character.DefaultMaxHealth);

        builder.Property(character => character.Health)
            .HasColumnName("health")
            .HasDefaultValue(Character.DefaultMaxHealth);

        builder.Property(character => character.MapId)
            .HasColumnName("map_id")
            .HasConversion(DomainValueConverters.MapId);

        builder.Property(character => character.PositionX)
            .HasColumnName("position_x");

        builder.Property(character => character.PositionY)
            .HasColumnName("position_y");

        builder.Property(character => character.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(character => character.UpdatedAt)
            .HasColumnName("updated_at");

        // Position is reconstructed from its persisted columns.
        builder.Ignore(character => character.Position);

        // A character must belong to an existing account. Deleting an account with
        // characters is intentionally prevented until an account policy exists.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(character => character.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Character names are globally unique; the guarantee is enforced by the database.
        builder.HasIndex(character => character.Name)
            .IsUnique()
            .HasDatabaseName(UniqueNameIndex);

        builder.HasIndex(character => character.AccountId)
            .HasDatabaseName(AccountIdIndex);
    }
}
