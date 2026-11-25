using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public static class IncidentObligation
{
    // Host owns the enclosing day/order/ticket transaction; Billing owns its invoice and signed adjustment rules.
    public static async Task<string?> ReduceAsync(BillingDbContext db, long orderId, Guid dayId, long portionId,
        long incidentId, Guid actor, Guid requestKey, string reason, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("An incident transaction is required.");
        var invoiceId = await db.Set<HoaDon>().Where(x => x.DonHangId == orderId).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);
        if (invoiceId is null) return "billing.invoice.not_found";
        var invoice = await BillingStore.LockInvoiceAsync(db, invoiceId.Value, ct);
        if (invoice is null || invoice.DongLuc is not null) return "billing.invoice.closed";
        var line = await db.Set<DongHoaDon>().SingleOrDefaultAsync(x => x.HoaDonId == invoice.Id && x.ChiTietDonId == portionId, ct);
        if (line is null) return "billing.line.not_found";
        if (line.ThanhTienGoc <= 0) return "billing.incident.no_obligation";
        var prior = await db.Set<DieuChinhNghiaVu>().SingleOrDefaultAsync(x => x.SuCoMonId == incidentId, ct);
        if (prior is not null) return prior.HoaDonId == invoice.Id && prior.SoTien == -line.ThanhTienGoc
            ? null : "billing.incident.conflict";
        var bill = await BillingStore.ReadAsync(db, invoice, ct);
        if (line.ThanhTienGoc > bill.Obligation) return "billing.amount.exceeds_available";
        // Billing keys are global; do not reuse a caller key scoped only to an actor and operation in Fulfillment.
        var billingKey = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"fulfillment.incident:{incidentId}"))[..16]);
        db.Add(new DieuChinhNghiaVu
        {
            HoaDonId = invoice.Id,
            SoTien = -line.ThanhTienGoc,
            SuCoMonId = incidentId,
            NgayBanId = dayId,
            NguoiDuyetId = actor,
            KhoaYeuCau = billingKey,
            LyDo = reason,
            TaoLuc = DateTime.UtcNow
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return "billing.idempotency.conflict"; }
        return null;
    }
}
