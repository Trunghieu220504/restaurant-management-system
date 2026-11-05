using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Restaurant.Modules.Audit.Infrastructure.Persistence;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Restaurant before running EF tooling.");
        var options = new DbContextOptionsBuilder<AuditDbContext>();
        AuditPersistence.Configure(options, connectionString);
        return new AuditDbContext(options.Options);
    }
}
