using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence;

public sealed class SalesDayDbContextFactory : IDesignTimeDbContextFactory<SalesDayDbContext>
{
    public SalesDayDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<SalesDayDbContext>();
        SalesDayPersistence.Configure(options, connectionString);
        return new SalesDayDbContext(options.Options);
    }
}
