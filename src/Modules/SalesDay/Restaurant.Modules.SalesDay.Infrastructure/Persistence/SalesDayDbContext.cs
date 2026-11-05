using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence;

public sealed class SalesDayDbContext(DbContextOptions<SalesDayDbContext> options)
    : DbContext(options)
{
    public const string Schema = "salesday";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDayDbContext).Assembly);
    }
}
