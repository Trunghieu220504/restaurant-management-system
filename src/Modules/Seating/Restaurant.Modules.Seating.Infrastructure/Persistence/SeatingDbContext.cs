using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Seating.Infrastructure.Persistence;

public sealed class SeatingDbContext(DbContextOptions<SeatingDbContext> options)
    : DbContext(options)
{
    public const string Schema = "seating";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeatingDbContext).Assembly);
    }
}
