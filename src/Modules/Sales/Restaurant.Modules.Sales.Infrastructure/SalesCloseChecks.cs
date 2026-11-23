using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Modules.Sales.Infrastructure;

public static class SalesCloseChecks
{
    public static Task<long[]> ConfirmedPortionsAsync(SalesDbContext db, Guid day, long? orderId, CancellationToken ct) =>
        db.Orders.Where(o => o.NgayBanId == day && (orderId == null || o.Id == orderId))
            .SelectMany(o => o.Dots).SelectMany(d => d.Portions)
            .Where(p => p.TrangThai == PortionStatus.Confirmed).Select(p => p.Id).ToArrayAsync(ct);

    public static async Task<(string Code, int Count)[]> ReadAsync(SalesDbContext db, Guid day, CancellationToken ct) =>
        [
            ("salesday.open_orders", await db.Orders.CountAsync(o => o.NgayBanId == day && o.TrangThai == OrderStatus.Open, ct)),
            ("salesday.pending_batches", await db.Orders.Where(o => o.NgayBanId == day)
                .SelectMany(o => o.Dots).CountAsync(d => d.TrangThai == BatchStatus.Pending, ct)),
            ("salesday.pending_events", await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value] FROM sales.Outbox WHERE ProcessedAt IS NULL AND
                  TRY_CONVERT(uniqueidentifier, JSON_VALUE(Payload, '$.businessDayId')) = {day}
                """).SingleAsync(ct))
        ];
}
