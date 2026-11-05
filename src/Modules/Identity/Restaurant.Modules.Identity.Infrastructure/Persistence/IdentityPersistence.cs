using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Identity.Infrastructure.Persistence;

internal static class IdentityPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema);
        });
    }
}
