using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

public sealed class FulfillmentDbContextFactory : IDesignTimeDbContextFactory<FulfillmentDbContext>
{
    public FulfillmentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>();
        FulfillmentPersistence.Configure(options, connectionString);
        return new FulfillmentDbContext(options.Options);
    }
}
