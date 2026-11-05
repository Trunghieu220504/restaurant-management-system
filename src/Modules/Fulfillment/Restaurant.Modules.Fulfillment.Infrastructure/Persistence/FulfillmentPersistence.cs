using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

internal static class FulfillmentPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(FulfillmentDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", FulfillmentDbContext.Schema);
        });
    }
}
