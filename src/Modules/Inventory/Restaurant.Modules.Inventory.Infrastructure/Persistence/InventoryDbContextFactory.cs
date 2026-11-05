using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<InventoryDbContext>();
        InventoryPersistence.Configure(options, connectionString);
        return new InventoryDbContext(options.Options);
    }
}
