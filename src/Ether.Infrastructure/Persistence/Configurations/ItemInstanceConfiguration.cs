using Ether.Domain.Characters;
using Ether.Domain.Items;
using Ether.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ether.Infrastructure.Persistence.Configurations;

internal sealed class ItemInstanceConfiguration : IEntityTypeConfiguration<ItemInstance>
{
    public const string OwnerIndex = "ix_item_instances_owner_character_id";

    public void Configure(EntityTypeBuilder<ItemInstance> builder)
    {
        builder.ToTable("item_instances", table =>
        {
            table.HasCheckConstraint("ck_item_instances_quantity_positive", "quantity > 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasConversion(DomainValueConverters.ItemInstanceId);

        builder.Property(item => item.DefinitionId)
            .HasColumnName("definition_id")
            .HasMaxLength(ItemDefinitionId.MaxLength)
            .HasConversion(DomainValueConverters.ItemDefinitionId)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity");

        builder.Property(item => item.OwnerCharacterId)
            .HasColumnName("owner_character_id")
            .HasConversion(DomainValueConverters.CharacterId);

        builder.Property(item => item.Location)
            .HasColumnName("location_type")
            .HasConversion<int>();

        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at");

        // An item instance belongs to a character; deleting a character with items
        // is prevented until an item policy exists.
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(item => item.OwnerCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.OwnerCharacterId)
            .HasDatabaseName(OwnerIndex);
    }
}
