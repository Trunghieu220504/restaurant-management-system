using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

public sealed class BillingLedger(BillingDbContext db, IBillingDayGate days, IConfiguration configuration) : IBillingLedger
{
    private const long MaxMoney = 999999999999999999L;
    private const string Invalid = "billing.input.invalid";
    private const string Excess = "billing.amount.exceeds_available";

    public async Task<InvoiceView?> FindAsync(long invoiceId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var invoice = await BillingStore.LockInvoiceAsync(db, invoiceId, ct);
        var result = invoice is null ? null : await BillingStore.ReadAsync(db, invoice, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<long?> ForOrderAsync(long orderId, CancellationToken ct) => db.Set<HoaDon>()
        .Where(x => x.DonHangId == orderId).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);

    public Task<string?> DocumentAsync(long documentId, CancellationToken ct) => db.Set<ChungTu>()
        .Where(x => x.Id == documentId).Select(x => x.NoiDungChot).SingleOrDefaultAsync(ct);

    public Task<MoneyResult> CollectAsync(long invoiceId, CollectPayment request, Guid actor, Guid key, CancellationToken ct)
    {
        var cash = request.Method == "TIEN_MAT";
        if (request.Amount is <= 0 or > MaxMoney ||
            (cash ? request.Tendered is null or > MaxMoney || request.Tendered < request.Amount || request.BankReference is not null :
            request.Method != "CHUYEN_KHOAN" || request.Tendered is not null || string.IsNullOrWhiteSpace(request.BankReference) ||
            request.BankReference.Length > 150 || request.BankReference.Any(ch => ch < 32 || ch > 126)))
            return Task.FromResult(new MoneyResult(null, Invalid));
        return WriteAsync(invoiceId, request.BusinessDayId, actor, key, new { op = "collect", request }, async (invoice, bill) =>
        {
            if (request.Amount > bill.Available) return new(null, Excess);
            var now = DateTime.UtcNow;
            var payment = new ThanhToan
            {
                HoaDonId = invoice.Id,
                NgayBanId = request.BusinessDayId,
                SoTien = request.Amount,
                PhuongThuc = request.Method,
                TienKhachDua = request.Tendered,
                TienThua = request.Tendered - request.Amount,
                MaThamChieuNganHang = request.BankReference?.Trim(),
                TrangThai = cash ? "THANH_CONG" : "CHO_XAC_NHAN",
                NguoiThuId = actor,
                TaoLuc = now,
                XacNhanLuc = cash ? now : null,
                KhoaYeuCau = key
            };
            db.Add(payment);
            await db.SaveChangesAsync(ct);
            if (cash) await DocumentAsync(invoice, payment, actor, request.BusinessDayId, ct);
            return new(payment.Id);
        }, ct);
    }

    public Task<MoneyResult> DecideAsync(long invoiceId, long paymentId, TransferDecision request, bool confirm, Guid actor, Guid key, CancellationToken ct) =>
        WriteAsync(invoiceId, request.BusinessDayId, actor, key, new { op = confirm ? "confirm" : "cancel", paymentId }, async (invoice, bill) =>
        {
            var payment = await db.Set<ThanhToan>().SingleOrDefaultAsync(x => x.HoaDonId == invoice.Id && x.Id == paymentId, ct);
            if (payment is null) return new(null, "billing.payment.not_found");
            if (payment.NgayBanId != request.BusinessDayId || payment.PhuongThuc != "CHUYEN_KHOAN" || payment.TrangThai != "CHO_XAC_NHAN")
                return new(null, "billing.payment.not_pending");
            if (confirm && payment.SoTien > bill.Due) return new(null, Excess);
            payment.TrangThai = confirm ? "THANH_CONG" : "HUY";
            payment.XacNhanLuc = confirm ? DateTime.UtcNow : null;
            await db.SaveChangesAsync(ct);
            if (confirm) await DocumentAsync(invoice, payment, actor, request.BusinessDayId, ct);
            return new(payment.Id);
        }, ct);

    public Task<MoneyResult> WaiveAsync(long invoiceId, WaiveBalance request, Guid actor, Guid key, CancellationToken ct)
    {
        if (request.Amount is <= 0 or > MaxMoney || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Task.FromResult(new MoneyResult(null, Invalid));
        return WriteAsync(invoiceId, request.BusinessDayId, actor, key, new { op = "waive", request }, async (invoice, bill) =>
        {
            if (request.Amount > bill.Available) return new(null, Excess);
            var waiver = new MienThu
            {
                HoaDonId = invoiceId,
                NgayBanId = request.BusinessDayId,
                SoTien = request.Amount,
                LyDo = request.Reason.Trim(),
                NguoiDuyetId = actor,
                TaoLuc = DateTime.UtcNow,
                KhoaYeuCau = key
            };
            db.Add(waiver);
            await db.SaveChangesAsync(ct);
            await DocumentAsync(invoice, null, actor, request.BusinessDayId, ct);
            return new(waiver.Id);
        }, ct);
    }

    private Task DocumentAsync(HoaDon invoice, ThanhToan? payment, Guid actor, Guid dayId, CancellationToken ct) =>
        BillingStore.AddDocumentAsync(db, invoice, payment, actor, dayId, configuration["Billing:StoreName"], configuration["Billing:StoreAddress"], ct);

    private async Task<MoneyResult> WriteAsync(long invoiceId, Guid dayId, Guid actor, Guid key, object payload,
        Func<HoaDon, InvoiceView, Task<MoneyResult>> change, CancellationToken ct)
    {
        if (invoiceId <= 0 || dayId == Guid.Empty || actor == Guid.Empty || key == Guid.Empty) return new(null, Invalid);
        var fingerprint = BillingStore.Fingerprint(new { invoiceId, dayId, actor, payload });
        async Task<MoneyResult?> ReplayAsync()
        {
            var row = await db.Set<BillingRequest>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
            if (row is null) return null;
            return row.Fingerprint == fingerprint ? new(row.ResourceId, Replayed: true) : new(null, "billing.idempotency.conflict");
        }
        try
        {
            var replay = await ReplayAsync();
            if (replay is not null) return replay;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (!await days.RequireOpenAsync(tx.GetDbTransaction(), dayId, ct)) return new(null, "salesday.day.not_open");
            await BillingStore.LockAsync(db, $"billing:key:{key:N}", ct);
            replay = await ReplayAsync();
            if (replay is not null) return replay;
            var invoice = await BillingStore.LockInvoiceAsync(db, invoiceId, ct);
            if (invoice is null) return new(null, "billing.invoice.not_found");
            if (invoice.DongLuc is not null) return new(null, "billing.invoice.closed");
            var result = await change(invoice, await BillingStore.ReadAsync(db, invoice, ct));
            if (result.Error is not null) return result;
            db.Add(new BillingRequest { Key = key, Fingerprint = fingerprint, InvoiceId = invoice.Id, ResourceId = result.ResourceId!.Value });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (TimeoutException) { return new(null, "billing.concurrency.conflict"); }
        catch (SqlException ex) when (ex.Number is 1205 or 51001) { return new(null, "billing.concurrency.conflict"); }
        finally { db.ChangeTracker.Clear(); }
    }
}
