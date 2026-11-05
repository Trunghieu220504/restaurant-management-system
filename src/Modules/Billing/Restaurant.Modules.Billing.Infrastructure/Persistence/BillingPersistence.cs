using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Billing.Infrastructure.Persistence;

internal static class BillingPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema);
        });
    }
}
