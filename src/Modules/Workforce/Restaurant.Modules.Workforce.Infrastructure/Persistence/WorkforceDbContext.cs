using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Workforce.Infrastructure.Persistence;

public sealed class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options)
    : DbContext(options)
{
    public const string Schema = "workforce";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkforceDbContext).Assembly);
    }
}
