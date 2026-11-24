using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Reporting.Application;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

internal sealed class BusinessReports(SalesDayDbContext salesDays, BillingDbContext billing, InventoryDbContext inventory) : IBusinessReports
{
    public async Task<BusinessReport> ReadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var snapshots = await salesDays.Set<DaySnapshot>().AsNoTracking().Include(d => d.Methods)
            .Where(d => d.BusinessDate >= from && d.BusinessDate <= to).OrderBy(d => d.BusinessDate).ToListAsync(ct);
        var days = snapshots.Select(d => new ReportDay(d.DayId, d.BusinessDate, d.Charges, d.Adjustments, d.Waivers,
            d.NetRevenue, d.Receipts, d.Refunds, d.DrawerPurchases, d.Cost, d.GrossProfit,
            d.Methods.OrderBy(m => m.Method).Select(m => new CashMovement(m.Method, m.Receipts, m.Refunds, m.Net)).ToArray())).ToArray();
        var methods = days.SelectMany(d => d.Methods).GroupBy(m => m.Method).OrderBy(g => g.Key)
            .Select(g => new CashMovement(g.Key, g.Sum(m => m.Receipts), g.Sum(m => m.Refunds), g.Sum(m => m.Net))).ToArray();
        var ids = days.Select(d => d.DayId).ToArray();
        var items = await ItemsAsync(ids, ct);
        var stock = await StockAsync(ids, ct);
        return new(from, to, days, days.Sum(d => d.NetRevenue), days.Sum(d => d.Cost), days.Sum(d => d.GrossProfit),
            days.Sum(d => d.DrawerPurchases), methods, items, stock);
    }

    private Task<Guid[]> DayIdsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        salesDays.Set<DaySnapshot>().Where(d => d.BusinessDate >= from && d.BusinessDate <= to).Select(d => d.DayId).ToArrayAsync(ct);

    public async Task<IReadOnlyList<SoldItem>> ItemsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await ItemsAsync(await DayIdsAsync(from, to, ct), ct);

    public async Task<IReadOnlyList<ClosingStock>> StockAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await StockAsync(await DayIdsAsync(from, to, ct), ct);

    private Task<List<SoldItem>> ItemsAsync(Guid[] ids, CancellationToken ct) =>
        (from line in billing.Set<DongHoaDon>()
         join invoice in billing.Set<HoaDon>() on line.HoaDonId equals invoice.Id
         where ids.Contains(invoice.NgayBanLapId)
         group line by line.TenMonChot into g
         orderby g.Key
         select new SoldItem(g.Key, g.Count(), g.Sum(l => l.ThanhTienGoc))).ToListAsync(ct);

    private Task<List<ClosingStock>> StockAsync(Guid[] ids, CancellationToken ct) =>
        inventory.Set<KiemKeTon>().Where(k => ids.Contains(k.NgayBanId) && k.ChotLuc != null)
            .OrderBy(k => k.ChotLuc).ThenBy(k => k.NguyenLieuId)
            .Select(k => new ClosingStock(k.NgayBanId, k.NguyenLieuId, k.DonViChotId, k.TonDau, k.SoDemDuyet,
                k.GiaTriTonCuoi, k.LuongDungChot, k.GiaVonChot)).ToListAsync(ct);
}
