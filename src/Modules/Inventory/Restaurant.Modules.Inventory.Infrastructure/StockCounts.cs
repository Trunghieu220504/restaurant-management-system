using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public sealed class StockCounts(InventoryDbContext db, IInventoryDayGate days) : IStockCounts
{
    public static Task InitializeAsync(InventoryDbContext db, Guid day, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT inventory.KiemKeTon (NgayBanId, NguyenLieuId, CheDoChot, TonDau, GiaTriTonDau)
            SELECT {day}, i.Id, 'THEO_DOI_TON', COALESCE(last.SoDemDuyet, 0), COALESCE(last.GiaTriTonCuoi, 0)
            FROM inventory.NguyenLieu i
            OUTER APPLY (SELECT TOP (1) k.SoDemDuyet, k.GiaTriTonCuoi FROM inventory.KiemKeTon k
                         WHERE k.NguyenLieuId=i.Id AND k.ChotLuc IS NOT NULL ORDER BY k.ChotLuc DESC, k.Id DESC) last
            WHERE ((i.LuuTru=0 AND i.CheDo='THEO_DOI_TON') OR last.SoDemDuyet>0 OR last.GiaTriTonCuoi>0)
              AND NOT EXISTS (SELECT 1 FROM inventory.KiemKeTon k WITH (UPDLOCK,HOLDLOCK) WHERE k.NgayBanId={day} AND k.NguyenLieuId=i.Id)
            """, ct);

    internal static IQueryable<KiemKeTon> RelevantForDay(InventoryDbContext db, Guid day, IQueryable<KiemKeTon>? source = null) =>
        (source ?? db.Set<KiemKeTon>()).Where(count => count.NgayBanId == day &&
            (count.ChotLuc != null || count.TonDau > 0 || count.GiaTriTonDau > 0 || count.SoDemNhap != null || count.SoDemDuyet != null ||
             db.Set<NguyenLieu>().Any(item => item.Id == count.NguyenLieuId && !item.LuuTru && item.CheDo == "THEO_DOI_TON")));

    public async Task<IReadOnlyList<StockCountView>> ListAsync(Guid day, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var open = await days.RequireOpenAsync(tx.GetDbTransaction(), day, ct);
        var rows = from k in RelevantForDay(db, day).AsNoTracking()
                   join i in db.Set<NguyenLieu>() on k.NguyenLieuId equals i.Id
                   orderby i.Ma
                   select new StockCountView(i.Id, i.Ten, k.DonViChotId ?? i.DonViChuanId,
                       k.TonDau, k.GiaTriTonDau, k.SoDemNhap, k.SoDemDuyet, k.Version)
                   {
                       FrozenCost = k.ChotLuc != null && k.GiaVonChot != null
                           ? new StockCost(k.GiaBinhQuanChot!.Value, k.LuongDungChot!.Value, k.GiaTriTonCuoi!.Value, k.GiaVonChot.Value) : null
                   };
        var list = await rows.ToListAsync(ct);
        // Days opened before the stock-count migration may be missing carried rows.
        if (open)
        {
            var missing = from i in db.Set<NguyenLieu>()
                          where !i.LuuTru && i.CheDo == "THEO_DOI_TON"
                && !db.Set<KiemKeTon>().Any(k => k.NgayBanId == day && k.NguyenLieuId == i.Id)
                          from last in db.Set<KiemKeTon>().Where(k => k.NguyenLieuId == i.Id && k.ChotLuc != null)
                              .OrderByDescending(k => k.ChotLuc).ThenByDescending(k => k.Id).Take(1).DefaultIfEmpty()
                          select new StockCountView(i.Id, i.Ten, i.DonViChuanId,
                              last.SoDemDuyet ?? 0, last.GiaTriTonCuoi ?? 0, null, null, null);
            list.AddRange(await missing.ToListAsync(ct));
        }
        return list;
    }

    public async Task<StockResult> SaveAsync(Guid day, long ingredient, decimal? draft, byte[]? version, bool approve, Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty || (approve ? version is not { Length: 8 } :
            draft is null or < 0 or >= 100000000000000000000m || decimal.Round(draft.Value, 8) != draft))
            return new(null, "inventory.input.invalid");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await days.RequireOpenAsync(tx.GetDbTransaction(), day, ct)) return new(null, "salesday.day.not_open");
        db.ChangeTracker.Clear();
        var locked = db.Set<KiemKeTon>().FromSqlInterpolated(
            $"SELECT * FROM inventory.KiemKeTon WITH (UPDLOCK,HOLDLOCK) WHERE NgayBanId={day} AND NguyenLieuId={ingredient}");
        var row = await RelevantForDay(db, day, locked).SingleOrDefaultAsync(ct);
        if (row == null)
        {
            if (approve || version != null) return new(null, "inventory.count.stale");
            var item = await db.Set<NguyenLieu>().SingleOrDefaultAsync(i => i.Id == ingredient && !i.LuuTru && i.CheDo == "THEO_DOI_TON", ct);
            if (item == null) return new(null, "inventory.reference.not_found");
            var last = await db.Set<KiemKeTon>().Where(k => k.NguyenLieuId == ingredient && k.ChotLuc != null)
                .OrderByDescending(k => k.ChotLuc).ThenByDescending(k => k.Id).FirstOrDefaultAsync(ct);
            row = new()
            {
                NgayBanId = day,
                NguyenLieuId = ingredient,
                CheDoChot = item.CheDo,
                TonDau = last?.SoDemDuyet ?? 0,
                GiaTriTonDau = last?.GiaTriTonCuoi ?? 0
            };
            db.Add(row);
        }
        else if (version == null || !row.Version.SequenceEqual(version)) return new(null, "inventory.count.stale");

        if (approve)
        {
            if (row.SoDemNhap == null) return new(null, "inventory.count.no_draft");
            row.SoDemDuyet = row.SoDemNhap;
            row.NguoiDuyetId = actor;
            row.DuyetLuc = DateTimeOffset.UtcNow;
        }
        else
        {
            row.SoDemNhap = draft;
            row.NguoiNhapId = actor;
            row.NhapLuc = DateTimeOffset.UtcNow;
            row.SoDemDuyet = null;
            row.NguoiDuyetId = null;
            row.DuyetLuc = null;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return new(ingredient);
    }
}
