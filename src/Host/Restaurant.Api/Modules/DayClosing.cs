using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Infrastructure;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Shared.SqlServer;
using Restaurant.Modules.Fulfillment.Infrastructure;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

internal sealed partial class DayClosing(SalesDayDbContext db, SalesDbContext sales, SeatingDbContext seating,
    BillingDbContext billing, InventoryDbContext inventory, FulfillmentDbContext kitchen) : IDayClosing, IDaySettlement
{
    public Task<ClosingResult> ChecklistAsync(Guid dayId, CancellationToken ct) =>
        RunAsync(dayId, null, Guid.Empty, Guid.Empty, ct);

    public Task<ClosingResult> ChangeAsync(Guid dayId, bool cancel, Guid actor, Guid key, CancellationToken ct) =>
        RunAsync(dayId, cancel ? SalesDayStatuses.Open : SalesDayStatuses.PendingClose, actor, key, ct);

    private async Task<ClosingResult> RunAsync(Guid dayId, string? target, Guid actor, Guid key, CancellationToken ct)
    {
        if (dayId == Guid.Empty) return new(null, SalesDayErrorCodes.DayNotFound);
        if (target != null && (actor == Guid.Empty || key == Guid.Empty)) return new(null, "salesday.input.invalid");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize the checklist with day-scoped writers, including the first insert in another module.
            await BusinessDayLock.AcquireAsync(db.Database.GetDbConnection(), tx.GetDbTransaction(), dayId,
                BusinessDayLockMode.Exclusive, cancellationToken: ct);
            var day = await db.Days.SingleOrDefaultAsync(d => d.Id == dayId, ct);
            if (day is null) return new(null, SalesDayErrorCodes.DayNotFound);
            if (target == null && day.Status == SalesDayStatuses.Closed) return new(new(dayId, day.Status, []));
            if (target != null)
            {
                var prior = await db.Set<ClosingEntry>().AsNoTracking().SingleOrDefaultAsync(e => e.DayId == dayId && e.RequestKey == key, ct);
                if (prior != null) return prior.Status == target && prior.Actor == actor
                    ? new(new(dayId, prior.Status, [])) : new(null, "salesday.idempotency.conflict");
                if (day.Status == target || day.Status == SalesDayStatuses.Closed)
                    return new(null, "salesday.close.invalid_transition");
            }
            if (target != SalesDayStatuses.Open)
            {
                var blockers = await BlockersAsync(dayId, ct);
                var checklist = new CloseChecklist(dayId, day.Status, blockers);
                if (target == null) return new(checklist);
                if (blockers.Count > 0) return new(checklist, "salesday.close.blocked");
            }
            day.Status = target!;
            db.Add(new ClosingEntry { DayId = dayId, RequestKey = key, Actor = actor, Status = target!, ChangedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(new(dayId, target!, []));
        }
        catch (TimeoutException) { return new(null, "salesday.close.busy"); }
        finally { db.ChangeTracker.Clear(); }
    }

    private async Task<List<CloseBlocker>> BlockersAsync(Guid dayId, CancellationToken ct, DayStockValue? valuation = null)
    {
        var blockers = new List<CloseBlocker>();
        Add(blockers, await SalesCloseChecks.ReadAsync(sales, dayId, ct));
        Add(blockers, await SeatingCloseChecks.ReadAsync(seating, dayId, ct));
        Add(blockers, await BillingCloseChecks.ReadAsync(billing, dayId, ct));
        Add(blockers, await InventoryCloseChecks.ReadAsync(inventory, dayId, ct));
        var confirmed = await SalesCloseChecks.ConfirmedPortionsAsync(sales, dayId, null, ct);
        var final = await TicketSettlement.FinalAsync(kitchen, confirmed, ct);
        if (confirmed.Length > final.Count)
            blockers.Add(new("salesday.pending_fulfillment", confirmed.Length - final.Count));
        var incidents = final.Where(t => t.AdjustmentIncidentId.HasValue).Select(t => t.AdjustmentIncidentId!.Value).ToArray();
        if (incidents.Length > 0)
        {
            var applied = await BillingCloseChecks.AppliedIncidentsAsync(billing, dayId, null, incidents, ct);
            if (applied < incidents.Length) blockers.Add(new("salesday.pending_adjustments", incidents.Length - applied));
        }
        if (!blockers.Any(b => b.Code == "salesday.unapproved_counts"))
        {
            valuation ??= await StockValuation.ReadAsync(inventory, dayId, ct);
            var invalid = valuation.Lines.Count(line => line.Cost == null);
            if (invalid > 0) blockers.Add(new("inventory.count.invalid_valuation", invalid));
        }
        return blockers;
    }

    private static void Add(List<CloseBlocker> blockers, (string Code, int Count)[] counts) =>
        blockers.AddRange(counts.Where(x => x.Count > 0).Select(x => new CloseBlocker(x.Code, x.Count)));
}
