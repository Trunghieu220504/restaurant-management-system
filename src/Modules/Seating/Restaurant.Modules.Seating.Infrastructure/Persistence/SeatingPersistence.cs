using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Seating.Infrastructure.Persistence;

internal static class SeatingPersistence
{
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(SeatingDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", SeatingDbContext.Schema);
        });
    }
}
