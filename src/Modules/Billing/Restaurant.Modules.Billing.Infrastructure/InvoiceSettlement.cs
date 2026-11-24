using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public static class InvoiceSettlement
{
    public static async Task<string?> CloseAsync(BillingDbContext db, long orderId, Guid day,
        IReadOnlyList<long> confirmed, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Order closing requires a transaction.");
        await BillingStore.LockAsync(db, $"billing:order:{orderId}", ct);
        var invoice = await db.Set<HoaDon>().FromSqlInterpolated(
            $"SELECT * FROM billing.HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE DonHangId={orderId}").SingleOrDefaultAsync(ct);
        if (invoice is null) return confirmed.Count == 0 ? null : "billing.invoice.missing_charges";
        if (invoice.NgayBanLapId != day) return "billing.invoice.day_mismatch";
        var bill = await BillingStore.ReadAsync(db, invoice, ct);
        if (confirmed.Except(bill.Lines.Select(l => l.PortionId)).Any()) return "billing.invoice.missing_charges";
        if (bill.Reserved != 0) return "billing.transfer.pending";
        if (await (from refund in db.Set<HoanTien>()
                   join payment in db.Set<ThanhToan>() on refund.ThanhToanId equals payment.Id
                   where payment.HoaDonId == invoice.Id && refund.TrangThai != "THANH_CONG" &&
                         refund.TrangThai != "TU_CHOI" && refund.TrangThai != "HUY"
                   select refund.Id).AnyAsync(ct)) return "billing.refund.pending";
        if (bill.NetCollected != bill.Obligation) return "billing.invoice.unsettled";
        invoice.DongLuc ??= now;
        await db.SaveChangesAsync(ct);
        return null;
    }
}
