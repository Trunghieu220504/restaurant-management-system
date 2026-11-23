using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;

namespace Restaurant.Modules.Catalog.Infrastructure;

public sealed class MenuAvailability(CatalogDbContext db, ICatalogBusinessDays days) : IMenuAvailability
{
    public async Task<AvailabilitySnapshot> SnapshotAsync(CancellationToken ct)
    {
        var day = await days.CurrentOpenDayAsync(ct);
        if (day is null) return new(null, []);
        var items = db.Set<HetMon>().Where(x => x.NgayBanId == day && x.Het)
            .Select(x => new { Kind = "ITEM", TargetId = x.MonId });
        var variants = db.Set<HetBienThe>().Where(x => x.NgayBanId == day && x.Het)
            .Select(x => new { Kind = "VARIANT", TargetId = x.BienTheId });
        var options = from flag in db.Set<HetTuyChon>()
                      join option in db.Set<TuyChon>() on flag.TuyChonId equals option.Id
                      join groupRow in db.Set<NhomTuyChon>() on option.NhomTuyChonId equals groupRow.Id
                      where flag.NgayBanId == day && flag.Het
                      select new { Kind = groupRow.Loai == "TOPPING" ? "TOPPING" : "OPTION", TargetId = option.Id };
        var flags = await items.Concat(variants).Concat(options)
            .Select(x => new SoldOutTarget(x.Kind, x.TargetId)).ToListAsync(ct);
        return new(day, flags);
    }

    public async Task<string?> SetAsync(AvailabilityChange change, Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty || change.BusinessDayId == Guid.Empty || change.TargetId == Guid.Empty) return "catalog.input.invalid";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (!await days.RequireOpenAsync(tx.GetDbTransaction(), change.BusinessDayId, ct)) return "salesday.day.not_open";
            var now = DateTimeOffset.UtcNow;
            switch (change.Kind)
            {
                case "ITEM":
                    if (!await db.Set<Mon>().AnyAsync(x => x.Id == change.TargetId, ct)) return "catalog.target.not_found";
                    var flagMon = await db.Set<HetMon>().FromSqlInterpolated(
                        $"SELECT * FROM catalog.HetMon WITH (UPDLOCK,HOLDLOCK) WHERE NgayBanId={change.BusinessDayId} AND MonId={change.TargetId}").SingleOrDefaultAsync(ct);
                    if (flagMon is null) db.Add(flagMon = new HetMon { NgayBanId = change.BusinessDayId, MonId = change.TargetId });
                    flagMon.Het = change.SoldOut;
                    flagMon.NguoiCapNhatId = actor;
                    flagMon.CapNhatLuc = now;
                    break;
                case "VARIANT":
                    if (!await db.Set<BienThe>().AnyAsync(x => x.Id == change.TargetId, ct)) return "catalog.target.not_found";
                    var flagBienThe = await db.Set<HetBienThe>().FromSqlInterpolated(
                        $"SELECT * FROM catalog.HetBienThe WITH (UPDLOCK,HOLDLOCK) WHERE NgayBanId={change.BusinessDayId} AND BienTheId={change.TargetId}").SingleOrDefaultAsync(ct);
                    if (flagBienThe is null) db.Add(flagBienThe = new HetBienThe { NgayBanId = change.BusinessDayId, BienTheId = change.TargetId });
                    flagBienThe.Het = change.SoldOut;
                    flagBienThe.NguoiCapNhatId = actor;
                    flagBienThe.CapNhatLuc = now;
                    break;
                case "OPTION":
                case "TOPPING":
                    var choiceKind = await (from option in db.Set<TuyChon>()
                                            join groupRow in db.Set<NhomTuyChon>() on option.NhomTuyChonId equals groupRow.Id
                                            where option.Id == change.TargetId
                                            select groupRow.Loai).SingleOrDefaultAsync(ct);
                    if (choiceKind != (change.Kind == "TOPPING" ? "TOPPING" : "TUY_CHON")) return "catalog.target.not_found";
                    var flagTuyChon = await db.Set<HetTuyChon>().FromSqlInterpolated(
                        $"SELECT * FROM catalog.HetTuyChon WITH (UPDLOCK,HOLDLOCK) WHERE NgayBanId={change.BusinessDayId} AND TuyChonId={change.TargetId}").SingleOrDefaultAsync(ct);
                    if (flagTuyChon is null) db.Add(flagTuyChon = new HetTuyChon { NgayBanId = change.BusinessDayId, TuyChonId = change.TargetId });
                    flagTuyChon.Het = change.SoldOut;
                    flagTuyChon.NguoiCapNhatId = actor;
                    flagTuyChon.CapNhatLuc = now;
                    break;
                default: return "catalog.input.invalid";
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return null;
        }
        catch (TimeoutException) { return CatalogErrors.ConcurrencyConflict; }
        catch (DbUpdateConcurrencyException) { return CatalogErrors.ConcurrencyConflict; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 }) { return CatalogErrors.ConcurrencyConflict; }
        finally { db.ChangeTracker.Clear(); }
    }
    public async Task<PublicMenu> PublicAsync(CancellationToken ct)
    {
        var dayId = await days.CurrentOpenDayAsync(ct);
        if (dayId is null) return new(null, []);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await days.RequireOpenAsync(tx.GetDbTransaction(), dayId.Value, ct)) return new(null, []);
        var day = await DayMenu.LoadAsync(db, dayId.Value, ct);
        var groups = day.Menu.Groups.Where(x => x.Active).Select(g => new PublicGroup(g.Id, g.Name,
            day.Menu.Items.Where(i => i.GroupId == g.Id).Select(day.PublicItem).OfType<PublicItem>().ToArray()))
            .Where(x => x.Items.Count > 0).ToArray();
        await tx.CommitAsync(ct);
        return new(dayId, groups);
    }
}
