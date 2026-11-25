using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Modules.Sales.Infrastructure;

internal sealed class OrderQueries(SalesDbContext db) : IOrderQueries
{
    public async Task<OpenOrdersPage> ReadOpenAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page is < 0 or > 10000 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        var query = db.Orders.AsNoTracking().Where(o => o.TrangThai == OrderStatus.Open);
        var total = await query.CountAsync(ct);
        var orders = await query.OrderByDescending(o => o.Id).Skip(page * pageSize).Take(pageSize)
            .Include(o => o.Dots).ThenInclude(b => b.Portions).ThenInclude(p => p.Options).AsSplitQuery().ToListAsync(ct);
        return new(orders.Select(o => new OpenOrder(o.Id, o.MaDon, o.Loai, o.NgayBanId, o.PhienSuDungId,
            o.NhanBanLucTao, o.TenKhach, o.Dots.OrderBy(b => b.SoDot).Select(b => new OpenOrderBatch(b.Id, b.SoDot, b.TrangThai,
                b.Portions.Where(p => p.TrangThai != PortionStatus.Cancelled).Sum(p => p.GiaMonChot + p.Options.Sum(x => x.GiaCongChot)),
                b.Portions.Select(p => new PortionView(p.Id, p.TenMonChot, p.TenBienTheChot, p.TramChot, p.GhiChu,
                    p.GiaMonChot + p.Options.Sum(x => x.GiaCongChot), p.Options.Select(x => x.TenTuyChonChot).ToArray(), p.TrangThai)).ToArray())).ToArray())).ToArray(), total);
    }
}
