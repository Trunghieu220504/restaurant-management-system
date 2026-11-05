using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Sales.Infrastructure.Persistence;

internal static class SalesPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(SalesDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema);
        });
    }
}
