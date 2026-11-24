using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Inventory.Infrastructure;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Messaging;
using Restaurant.Shared.SqlServer;

namespace Restaurant.Api.Modules;

internal sealed partial class DayClosing
{
    private IQueryable<DaySnapshot> Snapshots => db.Set<DaySnapshot>().AsNoTracking().Include(s => s.Methods.OrderBy(m => m.Method));

    public Task<DaySnapshot?> SnapshotAsync(Guid day, CancellationToken ct) => Snapshots.SingleOrDefaultAsync(s => s.DayId == day, ct);

    public async Task<IReadOnlyList<DaySnapshot>> HistoryAsync(int offset, int limit, CancellationToken ct) =>
        await Snapshots.OrderByDescending(s => s.BusinessDate).Skip(offset).Take(limit).ToListAsync(ct);

    public async Task<SettlementPreview?> PreviewAsync(Guid day, CancellationToken ct)
    {
        if (day == Guid.Empty) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BusinessDayLock.AcquireAsync(db.Database.GetDbConnection(), tx.GetDbTransaction(), day,
            BusinessDayLockMode.Exclusive, cancellationToken: ct);
        var current = await db.Days.AsNoTracking().SingleOrDefaultAsync(d => d.Id == day && d.ActiveSlot == 1, ct);
        if (current == null) return null;
        var stock = await StockValuation.ReadAsync(inventory, day, ct);
        var money = await BillingDayTotals.ReadAsync(billing, day, ct);
        var blockers = await BlockersAsync(day, ct, stock);
        return Preview(current, stock, money, blockers);
    }

    private static SettlementPreview Preview(BusinessDay day, DayStockValue stock, BillingDayTotals money, List<CloseBlocker> blockers)
    {
        var cash = money.Methods.Single(m => m.Method == "TIEN_MAT");
        var revenue = money.Charges + money.Adjustments - money.Waivers;
        decimal? raw = stock.Lines.All(l => l.Cost != null) && !blockers.Any(b => b.Code == "salesday.unapproved_counts") ? stock.TotalCost : null;
        decimal? rounded = raw.HasValue ? decimal.Round(raw.Value, 0, MidpointRounding.AwayFromZero) : null;
        var costs = stock.Lines.Where(l => l.Cost != null).Select(l => new CostLine(l.Ingredient, l.Unit,
            l.Cost!.Usage, l.Cost.Average, l.Cost.ClosingValue, l.Cost.Cost)).ToList();
        return new(day.OpeningCash, day.OpeningCash + cash.Receipts - cash.Refunds - stock.DrawerPurchases,
            stock.DrawerPurchases, revenue, rounded, raw.HasValue ? decimal.Round(rounded!.Value - raw.Value, 8, MidpointRounding.AwayFromZero) : null,
            revenue - rounded, costs, blockers);
    }

    public async Task<SettlementResult> FinalizeAsync(Guid day, FinalizeDay input, Guid actor, Guid key, CancellationToken ct)
    {
        var hasLateReason = !string.IsNullOrWhiteSpace(input.LateReason);
        if (day == Guid.Empty || actor == Guid.Empty || key == Guid.Empty || input.CountedCash < 0 ||
            input.CountedCash > long.MaxValue || decimal.Truncate(input.CountedCash) != input.CountedCash ||
            input.DifferenceReason?.Length > 1000 || input.LateReason?.Length > 1000 ||
            input.LateClose != hasLateReason) return new(null, "salesday.input.invalid");
        var differenceReason = string.IsNullOrWhiteSpace(input.DifferenceReason) ? null : input.DifferenceReason.Trim();
        var lateReason = input.LateClose ? input.LateReason!.Trim() : null;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var connection = db.Database.GetDbConnection();
            await BusinessDayLock.AcquireAsync(connection, tx.GetDbTransaction(), day, BusinessDayLockMode.Exclusive, cancellationToken: ct);
            var prior = await SnapshotAsync(day, ct);
            if (prior != null)
            {
                if (prior.RequestKey != key) return new(null, "salesday.day.already_closed");
                return prior.ClosedBy == actor && prior.CountedCash == input.CountedCash && prior.DifferenceReason == differenceReason &&
                    prior.LateClose == input.LateClose && prior.LateReason == lateReason ? new(prior) : new(null, "salesday.idempotency.conflict");
            }
            if (await db.Set<ClosingEntry>().AnyAsync(e => e.DayId == day && e.RequestKey == key, ct)) return new(null, "salesday.idempotency.conflict");
            var current = await db.Days.SingleOrDefaultAsync(d => d.Id == day, ct);
            if (current == null) return new(null, "salesday.day.not_found");
            if (current.Status != SalesDayStatuses.PendingClose) return new(null, "salesday.close.invalid_transition");
            // Inventory snapshots and the final day transition must roll back together.
            await using var stockDb = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(connection).Options);
            await stockDb.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
            var stock = await StockValuation.ReadAsync(stockDb, day, ct);
            var blockers = await BlockersAsync(day, ct, stock);
            if (blockers.Count > 0) return new(null, "salesday.close.blocked", blockers);
            var money = await BillingDayTotals.ReadAsync(billing, day, ct);
            var view = Preview(current, stock, money, blockers);
            if (view.ExpectedCash != input.CountedCash && differenceReason == null) return new(null, "salesday.cash.reason_required");
            var now = DateTimeOffset.UtcNow;
            var snapshot = new DaySnapshot
            {
                DayId = day,
                BusinessDate = current.BusinessDate,
                ClosedBy = actor,
                ClosedAt = now,
                RequestKey = key,
                LateClose = input.LateClose,
                LateReason = lateReason,
                DifferenceReason = differenceReason,
                CountedCash = input.CountedCash,
                ExpectedCash = view.ExpectedCash,
                OpeningCash = current.OpeningCash,
                DrawerPurchases = stock.DrawerPurchases,
                NetRevenue = view.NetRevenue,
                Cost = view.Cost!.Value,
                RoundingDifference = view.RoundingDifference!.Value,
                GrossProfit = view.GrossProfit!.Value,
                Charges = money.Charges,
                Adjustments = money.Adjustments,
                Waivers = money.Waivers,
                Receipts = money.Methods.Sum(m => m.Receipts),
                Refunds = money.Methods.Sum(m => m.Refunds),
                Methods = money.Methods.Select(m => new DayMethodSnapshot
                {
                    DayId = day,
                    Method = m.Method,
                    Receipts = m.Receipts,
                    Refunds = m.Refunds,
                    Net = m.Receipts - m.Refunds
                }).ToList()
            };
            await StockValuation.FreezeAsync(stockDb, day, stock, now, ct);
            current.Status = SalesDayStatuses.Closed;
            current.ActiveSlot = null;
            db.Add(snapshot);
            db.Add(new ClosingEntry { DayId = day, RequestKey = key, Actor = actor, Status = current.Status, ChangedAt = now });
            DayClosedEvent.Enqueue(db, snapshot);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(snapshot);
        }
        catch (TimeoutException) { return new(null, "salesday.close.busy"); }
        finally { db.ChangeTracker.Clear(); }
    }
}
