using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public static class BillingCloseChecks
{
    public static Task<int> AppliedIncidentsAsync(BillingDbContext db, Guid day, long? orderId, long[] incidents, CancellationToken ct) =>
        (from change in db.Set<DieuChinhNghiaVu>()
         join invoice in db.Set<HoaDon>() on change.HoaDonId equals invoice.Id
         where invoice.NgayBanLapId == day && (orderId == null || invoice.DonHangId == orderId)
             && change.SuCoMonId != null && incidents.Contains(change.SuCoMonId.Value)
         select change.SuCoMonId).Distinct().CountAsync(ct);

    public static async Task<(string Code, int Count)[]> ReadAsync(BillingDbContext db, Guid day, CancellationToken ct) =>
        [
            ("salesday.pending_transfers", await db.Set<ThanhToan>().CountAsync(p => p.NgayBanId == day && p.TrangThai == "CHO_XAC_NHAN", ct)),
            ("salesday.pending_refunds", await db.Set<HoanTien>().CountAsync(r => r.NgayBanId == day &&
                r.TrangThai != "THANH_CONG" && r.TrangThai != "HUY" && r.TrangThai != "TU_CHOI", ct))
        ];
}
