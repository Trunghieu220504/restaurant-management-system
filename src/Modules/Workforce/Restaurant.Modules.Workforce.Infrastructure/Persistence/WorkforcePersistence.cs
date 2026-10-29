using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Workforce.Infrastructure.Persistence;

internal static class WorkforcePersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(WorkforceDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", WorkforceDbContext.Schema);
        });
    }
}
