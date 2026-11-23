using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public sealed record MethodMovement(string Method, decimal Receipts, decimal Refunds);
public sealed record BillingDayTotals(decimal Charges, decimal Adjustments, decimal Waivers, IReadOnlyList<MethodMovement> Methods)
{
    public static async Task<BillingDayTotals> ReadAsync(BillingDbContext db, Guid day, CancellationToken ct)
    {
        var charges = await (from line in db.Set<DongHoaDon>()
                             join invoice in db.Set<HoaDon>() on line.HoaDonId equals invoice.Id
                             where invoice.NgayBanLapId == day
                             select line.ThanhTienGoc).SumAsync(ct);
        var adjustments = await db.Set<DieuChinhNghiaVu>().Where(x => x.NgayBanId == day).SumAsync(x => x.SoTien, ct);
        var waivers = await db.Set<MienThu>().Where(x => x.NgayBanId == day).SumAsync(x => x.SoTien, ct);
        var receipts = await db.Set<ThanhToan>().Where(x => x.NgayBanId == day && x.TrangThai == "THANH_CONG").GroupBy(x => x.PhuongThuc)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.SoTien) }).ToDictionaryAsync(x => x.Method, x => x.Total, ct);
        var refunds = await db.Set<HoanTien>().Where(x => x.NgayBanId == day && x.TrangThai == "THANH_CONG").GroupBy(x => x.PhuongThuc)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.SoTien) }).ToDictionaryAsync(x => x.Method, x => x.Total, ct);
        return new(charges, adjustments, waivers, new[] { "TIEN_MAT", "CHUYEN_KHOAN" }.Union(receipts.Keys).Union(refunds.Keys)
            .Select(method => new MethodMovement(method, receipts.GetValueOrDefault(method), refunds.GetValueOrDefault(method))).ToArray());
    }
}
