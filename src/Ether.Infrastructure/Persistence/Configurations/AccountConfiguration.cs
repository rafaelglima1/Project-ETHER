using Ether.Domain.Accounts;
using Ether.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ether.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .HasConversion(DomainValueConverters.AccountId);

        builder.Property(account => account.Status)
            .HasColumnName("status")
            .HasConversion<int>();

        builder.Property(account => account.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(account => account.UpdatedAt)
            .HasColumnName("updated_at");
    }
}
