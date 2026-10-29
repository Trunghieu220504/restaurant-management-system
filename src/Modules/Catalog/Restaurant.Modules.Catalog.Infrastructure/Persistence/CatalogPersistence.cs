using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Catalog.Infrastructure.Persistence;

internal static class CatalogPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema);
        });
    }
}
