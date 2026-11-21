using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Shared.SqlServer;

namespace Restaurant.Api.Modules;

/// <summary>Uses the caller's connection and transaction for both the lock and the fresh status read.</summary>
internal sealed class SqlSalesDayGate(Func<DbTransaction> currentTransaction) : ISalesDayGate
{
    public async Task<SalesDayGateResult> RequireOpenAsync(Guid businessDayId, CancellationToken ct)
    {
        if (businessDayId == Guid.Empty) return new(null, SalesDayErrorCodes.DayNotFound);
        var tx = currentTransaction();
        var connection = tx.Connection ?? throw new InvalidOperationException("The write transaction has ended.");
        await BusinessDayLock.AcquireAsync(connection, tx, businessDayId, BusinessDayLockMode.Shared, cancellationToken: ct);
        var options = new DbContextOptionsBuilder<SalesDayDbContext>().UseSqlServer(connection).Options;
        await using var db = new SalesDayDbContext(options);
        await db.Database.UseTransactionAsync(tx, ct);
        var day = await db.Days.FromSqlInterpolated(
            $"SELECT * FROM [salesday].[NgayBan] WITH (READCOMMITTEDLOCK) WHERE [Id] = {businessDayId}")
            .AsNoTracking().Select(x => new { x.Id, x.BusinessDate, x.Status }).SingleOrDefaultAsync(ct);
        if (day is null) return new(null, SalesDayErrorCodes.DayNotFound);
        return day.Status == SalesDayStatuses.Open
            ? new(new(day.Id, day.BusinessDate), null)
            : new(null, SalesDayErrorCodes.DayNotOpen);
    }
}
