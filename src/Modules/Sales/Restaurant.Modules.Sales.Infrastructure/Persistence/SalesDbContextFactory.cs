using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.Sales.Infrastructure.Persistence;

public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<SalesDbContext>();
        SalesPersistence.Configure(options, connectionString);
        return new SalesDbContext(options.Options);
    }
}
