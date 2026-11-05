using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

public sealed class FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options)
    : DbContext(options)
{
    public const string Schema = "fulfillment";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FulfillmentDbContext).Assembly);
    }
}
