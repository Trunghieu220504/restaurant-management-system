using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Contracts;

namespace Restaurant.Api.Modules;

internal sealed class OrderClosing(SalesDbContext sales, ISalesDayGate days, IOrderSettlement settlement) : ICloseOrder
{
    public async Task<MoneyResult> CloseAsync(long orderId, Guid day, CancellationToken ct)
    {
        await using var tx = await sales.Database.BeginTransactionAsync(ct);
        try
        {
            var gate = await days.RequireOpenAsync(day, ct);
            if (!gate.IsOpen) return new(null, gate.ErrorCode);
            var order = await sales.Orders.FromSqlInterpolated(
                $"SELECT * FROM sales.DonHang WITH (UPDLOCK,HOLDLOCK) WHERE Id={orderId}").SingleOrDefaultAsync(ct);
            if (order is null || order.NgayBanId != day) return new(null, "sales.order.not_found");
            if (order.TrangThai == OrderStatuses.Closed) return new(orderId, Replayed: true);
            if (order.TrangThai != OrderStatuses.Open) return new(null, "sales.order.not_open");
            var state = await settlement.GetStateAsync(orderId, ct);
            if (state is null || !state.IsSettled) return new(null, "sales.order.processing_incomplete");

            var confirmed = await SalesCloseChecks.ConfirmedPortionsAsync(sales, day, orderId, ct);
            var options = new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(sales.Database.GetDbConnection()).Options;
            await using var billing = new BillingDbContext(options);
            await billing.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            var now = DateTime.UtcNow;
            var error = await InvoiceSettlement.CloseAsync(billing, orderId, day, confirmed, now, ct);
            if (error != null) return new(null, error);
            var closed = order.Close(now);
            if (!closed.Succeeded) return new(null, closed.Error);
            await sales.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(orderId);
        }
        finally { sales.ChangeTracker.Clear(); }
    }
}
