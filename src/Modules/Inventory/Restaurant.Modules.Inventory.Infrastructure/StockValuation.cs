using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public sealed record StockValuationLine(long Ingredient, long Unit, decimal Purchased, decimal PurchaseValue,
    decimal QuantityAdjustment, decimal ValueAdjustment, StockCost? Cost);
public sealed record DayStockValue(IReadOnlyList<StockValuationLine> Lines, decimal ExpensedPurchases, decimal DrawerPurchases)
{
    public decimal TotalCost => ExpensedPurchases + Lines.Sum(x => x.Cost!.Cost);
}

public static class StockValuation
{
    public static async Task<DayStockValue> ReadAsync(InventoryDbContext db, Guid day, CancellationToken ct)
    {
        var purchases = await (from line in db.Set<ChiTietPhieuNhap>()
                               join receipt in db.Set<PhieuNhap>() on line.PhieuNhapId equals receipt.Id
                               where receipt.NgayBanId == day && receipt.TrangThai == "HIEU_LUC"
                               group line by new { line.NguyenLieuId, line.CheDoChot, line.DonViChuanChotId, receipt.TraBangTienKet } into g
                               select new { g.Key, Quantity = g.Sum(l => l.SoLuongChuan), Value = g.Sum(l => l.ThanhTien) }).ToListAsync(ct);
        var tracked = purchases.Where(p => p.Key.CheDoChot == "THEO_DOI_TON").ToLookup(p => p.Key.NguyenLieuId);
        var adjustments = await db.Set<DieuChinhTon>().Where(a => a.NgayBanId == day).GroupBy(a => a.NguyenLieuId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(a => a.SoLuongChuan), Value = g.Sum(a => a.GiaTriDieuChinh) })
            .ToDictionaryAsync(x => x.Id, ct);
        var counts = await (from k in db.Set<KiemKeTon>()
                            join i in db.Set<NguyenLieu>() on k.NguyenLieuId equals i.Id
                            where k.NgayBanId == day
                            select new { Count = k, Unit = i.DonViChuanId }).AsNoTracking().ToListAsync(ct);
        var lines = new List<StockValuationLine>();
        foreach (var row in counts)
        {
            var k = row.Count;
            var receipts = tracked[k.NguyenLieuId];
            var adjustment = adjustments.GetValueOrDefault(k.NguyenLieuId);
            var acquired = receipts.Where(p => p.Key.DonViChuanChotId == row.Unit).Sum(p => p.Quantity);
            var price = receipts.Sum(p => p.Value);
            var quantityDelta = adjustment?.Quantity ?? 0;
            var valueDelta = adjustment?.Value ?? 0;
            var cost = k.SoDemDuyet is decimal approved && receipts.All(p => p.Key.DonViChuanChotId == row.Unit)
                ? StockCost.Calculate(k.TonDau + acquired + quantityDelta, k.GiaTriTonDau + price + valueDelta, approved) : null;
            lines.Add(new(k.NguyenLieuId, row.Unit, acquired, price, quantityDelta, valueDelta, cost));
        }
        // An orphaned movement cannot disappear from the financial snapshot.
        var known = counts.Select(x => x.Count.NguyenLieuId).ToHashSet();
        foreach (var id in tracked.Select(g => g.Key).Union(adjustments.Keys).Where(id => !known.Contains(id)))
            lines.Add(new(id, 0, 0, 0, 0, 0, null));
        return new(lines, purchases.Where(p => p.Key.CheDoChot == "TINH_KHI_MUA").Sum(p => p.Value),
            purchases.Where(p => p.Key.TraBangTienKet).Sum(p => p.Value));
    }

    public static async Task FreezeAsync(InventoryDbContext db, Guid day, DayStockValue stock, DateTimeOffset now, CancellationToken ct)
    {
        var counts = await db.Set<KiemKeTon>().Where(k => k.NgayBanId == day).ToDictionaryAsync(k => k.NguyenLieuId, ct);
        foreach (var line in stock.Lines)
        {
            var row = counts[line.Ingredient];
            var cost = line.Cost!;
            row.DonViChotId = line.Unit;
            row.TongNhapChot = line.Purchased;
            row.GiaTriNhapChot = line.PurchaseValue;
            row.DieuChinhLuongChot = line.QuantityAdjustment;
            row.DieuChinhGiaTriChot = line.ValueAdjustment;
            row.GiaBinhQuanChot = decimal.Round(cost.Average, 8, MidpointRounding.AwayFromZero);
            row.LuongDungChot = cost.Usage;
            row.GiaTriTonCuoi = decimal.Round(cost.ClosingValue, 8, MidpointRounding.AwayFromZero);
            row.GiaVonChot = decimal.Round(cost.Cost, 8, MidpointRounding.AwayFromZero);
            row.ChotLuc = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
