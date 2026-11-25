using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

public sealed class FulfillmentIncidentContext : IFulfillmentIncidentContext
{
    public async Task<string?> RequireOpenAsync(DbTransaction transaction, long portionId, CancellationToken ct)
    {
        await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(transaction.Connection!).Options);
        await sales.Database.UseTransactionAsync(transaction, ct);
        var reference = await (from portion in sales.Portions
                               join batch in sales.Batches on portion.DotGoiMonId equals batch.Id
                               join order in sales.Orders on batch.DonHangId equals order.Id
                               where portion.Id == portionId
                               select new { order.Id, order.NgayBanId }).SingleOrDefaultAsync(ct);
        if (reference is null) return "sales.portion.not_found";
        var day = await new SqlSalesDayGate(() => transaction).RequireOpenAsync(reference.NgayBanId, ct);
        var locked = await sales.Orders.FromSqlInterpolated(
            $"SELECT * FROM sales.DonHang WITH (UPDLOCK,HOLDLOCK) WHERE Id={reference.Id}").SingleAsync(ct);
        var portionStatus = await sales.Portions.Where(x => x.Id == portionId).Select(x => x.TrangThai).SingleAsync(ct);
        // A delayed confirmation can outlive a cancellation and order closure. It must be acknowledged without new work.
        if (portionStatus == "DA_HUY") return "sales.portion.cancelled";
        if (!day.IsOpen) return day.ErrorCode;
        return locked.TrangThai == "MO" ? null : "sales.order.not_open";
    }

    public async Task<string?> ReduceObligationAsync(DbTransaction transaction, long portionId, long incidentId,
        Guid actor, Guid requestKey, string reason, CancellationToken ct)
    {
        await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(transaction.Connection!).Options);
        await sales.Database.UseTransactionAsync(transaction, ct);
        var owner = await (from portion in sales.Portions
                           join batch in sales.Batches on portion.DotGoiMonId equals batch.Id
                           join order in sales.Orders on batch.DonHangId equals order.Id
                           where portion.Id == portionId
                           select new { order.Id, order.NgayBanId }).SingleAsync(ct);
        await using var billing = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(transaction.Connection!).Options);
        await billing.Database.UseTransactionAsync(transaction, ct);
        return await IncidentObligation.ReduceAsync(billing, owner.Id, owner.NgayBanId, portionId, incidentId, actor, requestKey, reason, ct);
    }
}
