using Ether.Domain.Accounts;
using Ether.Domain.Characters;

using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Persistence;

/// <summary>
/// Primary persistence context (PostgreSQL).
/// M1 maps only Account and Character; later bounded contexts add their own mappings.
/// </summary>
public sealed class EtherDbContext : DbContext
{
    public EtherDbContext(DbContextOptions<EtherDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EtherDbContext).Assembly);
    }
}
