using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Items;

using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Persistence;

/// <summary>
/// Primary persistence context (PostgreSQL).
/// Mappings are added per bounded context; every table here reflects implemented features.
/// </summary>
public sealed class EtherDbContext : DbContext
{
    public EtherDbContext(DbContextOptions<EtherDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<ItemInstance> ItemInstances => Set<ItemInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EtherDbContext).Assembly);
    }
}
