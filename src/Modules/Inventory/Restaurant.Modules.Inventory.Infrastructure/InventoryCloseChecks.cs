using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public static class InventoryCloseChecks
{
    public static async Task<(string Code, int Count)[]> ReadAsync(InventoryDbContext db, Guid day, CancellationToken ct) =>
        [
            ("salesday.draft_purchases", await db.Set<PhieuNhap>().CountAsync(p => p.NgayBanId == day && p.TrangThai == "NHAP", ct)),
            ("salesday.unapproved_counts", await db.Set<NguyenLieu>().CountAsync(i => ((!i.LuuTru && i.CheDo == "THEO_DOI_TON") ||
                db.Set<KiemKeTon>().Any(c => c.NgayBanId == day && c.NguyenLieuId == i.Id)) &&
                !db.Set<KiemKeTon>().Any(c => c.NgayBanId == day && c.NguyenLieuId == i.Id &&
                    c.SoDemDuyet != null && c.DuyetLuc != null && c.NguoiDuyetId != null), ct))
        ];
}
