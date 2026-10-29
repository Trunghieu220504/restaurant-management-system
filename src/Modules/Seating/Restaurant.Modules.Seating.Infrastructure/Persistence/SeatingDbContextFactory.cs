using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.Seating.Infrastructure.Persistence;

public sealed class SeatingDbContextFactory : IDesignTimeDbContextFactory<SeatingDbContext>
{
    public SeatingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<SeatingDbContext>();
        SeatingPersistence.Configure(options, connectionString);
        return new SeatingDbContext(options.Options);
    }
}
