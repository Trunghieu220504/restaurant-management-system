using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence;

internal static class SalesDayPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(SalesDayDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", SalesDayDbContext.Schema);
        });
    }
}
