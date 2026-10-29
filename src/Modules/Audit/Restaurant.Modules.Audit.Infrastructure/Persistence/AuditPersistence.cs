using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Audit.Infrastructure.Persistence;

internal static class AuditPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema);
        });
    }
}
