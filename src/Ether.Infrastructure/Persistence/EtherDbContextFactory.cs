using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ether.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations).
/// It never connects to a database when adding or scripting migrations; the
/// connection string can be overridden with <c>ETHER_DESIGN_CONNECTION</c>.
/// </summary>
public sealed class EtherDbContextFactory : IDesignTimeDbContextFactory<EtherDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Database=ether;Username=ether;Password=ether";

    public EtherDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ETHER_DESIGN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<EtherDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EtherDbContext(options);
    }
}
