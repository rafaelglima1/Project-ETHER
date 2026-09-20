using Ether.Domain.Accounts;
using Ether.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ether.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public const string UniqueEmailIndex = "ux_accounts_email";

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint("ck_accounts_email_not_blank", "char_length(btrim(email)) > 0");
            table.HasCheckConstraint("ck_accounts_password_hash_not_blank", "char_length(btrim(password_hash)) > 0");
        });

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .HasConversion(DomainValueConverters.AccountId);

        builder.Property(account => account.Email)
            .HasColumnName("email")
            .HasMaxLength(Email.MaxLength)
            .HasConversion(
                email => email.Value,
                value => new Email(value))
            .IsRequired();

        builder.Property(account => account.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(PasswordHash.MaxLength)
            .HasConversion(
                hash => hash.Value,
                value => new PasswordHash(value))
            .IsRequired();

        builder.Property(account => account.Status)
            .HasColumnName("status")
            .HasConversion<int>();

        builder.Property(account => account.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(account => account.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(account => account.Email)
            .IsUnique()
            .HasDatabaseName(UniqueEmailIndex);
    }
}
