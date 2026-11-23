using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Restaurant.Modules.Billing.Contracts;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public sealed class InvoiceCharging(BillingDbContext db, IConfiguration configuration) : IInvoiceCharging
{
    public async Task<ChargeBatchResult> ChargeConfirmedBatchAsync(ChargeBatchRequest request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sales must enlist Billing in its confirmation transaction.");
        if (request is null || request.OrderId <= 0 || request.BatchId <= 0 || request.BusinessDayId == Guid.Empty ||
            request.IdempotencyKey == Guid.Empty || request.Actor == Guid.Empty || request.Lines is not { Count: > 0 } ||
            request.Lines.Any(x => x is null || x.PortionId <= 0 || string.IsNullOrWhiteSpace(x.Description) ||
                x.Description.Length > 2000 || x.Amount is < 0 or > 999999999999999999L) ||
            request.Lines.Select(x => x.PortionId).Distinct().Count() != request.Lines.Count)
            return new(null, BillingErrorCodes.LineInvalid);
        var lines = request.Lines.OrderBy(x => x.PortionId).ToArray();
        var fingerprint = BillingStore.Fingerprint(new { operation = "charge", request.OrderId, request.BatchId, request.BusinessDayId, request.Actor, lines });
        await BillingStore.LockAsync(db, $"billing:key:{request.IdempotencyKey:N}", ct);
        var previous = await db.Set<BillingRequest>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == request.IdempotencyKey, ct);
        if (previous is not null) return previous.Fingerprint == fingerprint
            ? new(previous.InvoiceId, null, true) : new(null, BillingErrorCodes.IdempotencyConflict);

        await BillingStore.LockAsync(db, $"billing:order:{request.OrderId}", ct);
        var invoice = await db.Set<HoaDon>().FromSqlInterpolated(
            $"SELECT * FROM billing.HoaDon WITH (UPDLOCK) WHERE DonHangId={request.OrderId}").SingleOrDefaultAsync(ct);
        if (invoice is not null && invoice.NgayBanLapId != request.BusinessDayId) return new(null, BillingErrorCodes.LineInvalid);
        var ids = lines.Select(x => x.PortionId).ToArray();
        var existing = await db.Set<DongHoaDon>().Where(x => ids.Contains(x.ChiTietDonId)).ToDictionaryAsync(x => x.ChiTietDonId, ct);
        foreach (var line in lines)
            if (existing.TryGetValue(line.PortionId, out var row) &&
                (row.HoaDonId != invoice?.Id || row.ThanhTienGoc != line.Amount || BillingStore.Description(row) != line.Description))
                return new(null, BillingErrorCodes.IdempotencyConflict);
        var missing = lines.Where(x => !existing.ContainsKey(x.PortionId)).ToArray();
        if (invoice?.DongLuc is not null && missing.Length > 0) return new(null, BillingErrorCodes.InvoiceClosed);
        if (invoice is null)
        {
            invoice = new HoaDon { DonHangId = request.OrderId, MaHoaDon = $"HD-{request.OrderId}", NgayBanLapId = request.BusinessDayId, TaoLuc = DateTime.UtcNow };
            db.Add(invoice);
            await db.SaveChangesAsync(ct);
        }
        foreach (var line in missing)
            db.Add(new DongHoaDon
            {
                HoaDonId = invoice.Id,
                ChiTietDonId = line.PortionId,
                TenMonChot = line.Description.Length <= 150 ? line.Description : line.Description[..(char.IsHighSurrogate(line.Description[149]) ? 149 : 150)],
                NoiDungTuyChonChot = line.Description.Length > 150 ? line.Description : null,
                ThanhTienGoc = line.Amount
            });
        if (missing.Length > 0)
        {
            await db.SaveChangesAsync(ct);
            await BillingStore.AddDocumentAsync(db, invoice, null, request.Actor, request.BusinessDayId, configuration["Billing:StoreName"], configuration["Billing:StoreAddress"], ct);
        }
        db.Add(new BillingRequest { Key = request.IdempotencyKey, Fingerprint = fingerprint, InvoiceId = invoice.Id, ResourceId = invoice.Id });
        await db.SaveChangesAsync(ct);
        return new(invoice.Id, null, missing.Length == 0);
    }
}
