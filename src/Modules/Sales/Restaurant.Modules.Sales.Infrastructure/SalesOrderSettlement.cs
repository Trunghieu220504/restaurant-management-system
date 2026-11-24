using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Modules.Sales.Infrastructure;

/// <summary>
/// Read-only answer for Billing (HIE-3.2+): is the order finished from the ordering side, and does a portion belong to it.
/// Sales decides alone about batches and portions; money stays with Billing. Like the session lookup it takes no lock, so a
/// caller that acts on "settled" (closing the order) holds the order lock of ADR-01 while it decides.
/// </summary>
public sealed class SalesOrderSettlement(SalesDbContext db) : IOrderSettlement
{
    public async Task<OrderProcessingState?> GetStateAsync(long orderId, CancellationToken ct)
    {
        if (orderId <= 0) return null;

        // One statement, so the counts and the status come from the same read.
        var row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.NgayBanId,
                o.TrangThai,
                Pending = o.Dots.Count(d => d.TrangThai == BatchStatus.Pending),
                // Final portion states are cancelled (and "served" once Fulfillment reports it back to Sales; that state is not
                // modelled yet). Everything else, including a confirmed portion that nobody served, is still open work.
                NonFinal = o.Dots.SelectMany(d => d.Portions)
                    .Count(p => p.TrangThai == PortionStatus.Pending || p.TrangThai == PortionStatus.Confirmed)
            })
            .SingleOrDefaultAsync(ct);

        return row is null
            ? null
            : new OrderProcessingState(row.Id, row.NgayBanId, row.TrangThai, row.Pending, row.NonFinal);
    }

    public Task<bool> PortionBelongsToOrderAsync(long orderId, long portionId, CancellationToken ct)
    {
        if (orderId <= 0 || portionId <= 0) return Task.FromResult(false);
        return (from p in db.Portions.AsNoTracking()
                join d in db.Batches.AsNoTracking() on p.DotGoiMonId equals d.Id
                where p.Id == portionId && d.DonHangId == orderId
                select p.Id).AnyAsync(ct);
    }
}
