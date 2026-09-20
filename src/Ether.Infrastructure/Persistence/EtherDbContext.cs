using Microsoft.EntityFrameworkCore;

namespace Ether.Infrastructure.Persistence;

/// <summary>
/// Primary persistence context (PostgreSQL).
/// Intentionally empty at M0: gameplay aggregate mappings are added per bounded
/// context in their respective milestones. No gameplay tables are created here.
/// </summary>
public sealed class EtherDbContext : DbContext
{
    public EtherDbContext(DbContextOptions<EtherDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
