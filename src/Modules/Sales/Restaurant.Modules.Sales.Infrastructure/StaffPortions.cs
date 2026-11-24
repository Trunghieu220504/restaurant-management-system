using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Sales.Infrastructure;

internal sealed class StaffPortions(SalesDbContext db, IStaffPortionDay days, IOutboxWriter outbox) : IStaffPortions
{
    public async Task<IReadOnlyList<StaffPortion>> ListAsync(CancellationToken ct)
    {
        var day = await days.CurrentOpenDayAsync(ct);
        if (day is null) return [];
        return await (from order in db.Orders
                      from batch in order.Dots
                      from p in batch.Portions
                      where order.NgayBanId == day && order.TrangThai == OrderStatus.Open
                          && (batch.TrangThai == BatchStatus.Pending || batch.TrangThai == BatchStatus.Confirmed)
                          && p.TrangThai != PortionStatus.Cancelled
                      orderby order.Id descending, batch.SoDot, p.Id
                      select new StaffPortion(p.Id.ToString(), order.MaDon, p.TenMonChot, p.TenBienTheChot,
                          p.GhiChu, p.TrangThai, Convert.ToBase64String(p.Version))).ToListAsync(ct);
    }

    public async Task<string?> ChangeAsync(long id, PortionChange change, bool cancel, Guid actor, CancellationToken ct)
    {
        var reason = change.Reason?.Trim();
        if (actor == Guid.Empty || string.IsNullOrEmpty(change.Version) ||
            (cancel && (string.IsNullOrEmpty(reason) || reason.Length > 512))) return SalesErrors.InputInvalid;
        var owner = await (from order in db.Orders
                           from batch in order.Dots
                           from p in batch.Portions
                           where p.Id == id
                           select new { order.Id, order.NgayBanId, batch.SoDot }).SingleOrDefaultAsync(ct);
        if (owner is null) return SalesErrors.PortionNotFound;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await days.RequireOpenAsync(tx.GetDbTransaction(), owner.NgayBanId, ct)) return "salesday.day.not_open";
        var orderToChange = await db.Orders.FromSqlInterpolated(
                $"SELECT * FROM sales.DonHang WITH (UPDLOCK) WHERE Id = {owner.Id}")
            .Include(o => o.Dots).ThenInclude(b => b.Portions)
            .SingleAsync(o => o.Id == owner.Id, ct);
        var batchToChange = orderToChange.Dots.Single(b => b.SoDot == owner.SoDot);
        var portion = batchToChange.Portions.Single(p => p.Id == id);
        if (change.Version != Convert.ToBase64String(portion.Version)) return "sales.portion.changed";
        if (!cancel && batchToChange.TrangThai != BatchStatus.Pending) return SalesErrors.BatchNotPending;
        if (!cancel && portion.TrangThai == PortionStatus.Pending && orderToChange.TrangThai == OrderStatus.Open &&
            portion.GhiChu == (string.IsNullOrWhiteSpace(change.Note) ? null : change.Note.Trim())) return null;
        var result = cancel ? orderToChange.CancelPortion(owner.SoDot, id) : orderToChange.EditPendingNote(owner.SoDot, id, change.Note);
        if (!result.Succeeded) return result.Error;
        var message = new IntegrationEvent<object>(Guid.NewGuid(), DateTimeOffset.UtcNow,
            cancel ? "sales.portion.cancelled" : "sales.portion.edited", 1, new { PortionId = id, OrderId = owner.Id, ActorId = actor, owner.NgayBanId, Reason = reason });
        await outbox.EnqueueAsync(message, owner.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), orderToChange.SoLanCapNhat, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException) { return "sales.portion.changed"; }
    }
}
