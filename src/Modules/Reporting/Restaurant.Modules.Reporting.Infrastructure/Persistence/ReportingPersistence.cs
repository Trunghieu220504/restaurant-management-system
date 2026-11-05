using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Reporting.Infrastructure.Persistence;

internal static class ReportingPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", ReportingDbContext.Schema);
        });
    }
}
