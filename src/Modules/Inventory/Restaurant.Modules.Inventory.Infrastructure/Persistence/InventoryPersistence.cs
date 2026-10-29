using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Inventory.Infrastructure.Persistence;

internal static class InventoryPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema);
        });
    }
}
