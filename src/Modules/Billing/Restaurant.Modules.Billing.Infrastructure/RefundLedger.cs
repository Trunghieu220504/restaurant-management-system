using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;

namespace Restaurant.Modules.Billing.Infrastructure;

public sealed partial class BillingLedger
{
    private static readonly Expression<Func<HoanTien, RefundView>> RefundProjection = r =>
        new(r.Id, r.ThanhToanId, r.NgayBanId, r.SoTien, r.PhuongThuc, r.TrangThai, r.LyDo, r.NguoiDeNghiId,
            r.NguoiDuyetId, r.NguoiChiId, r.MaChiTra, r.TaoLuc, r.ChiLuc);

    public async Task<IReadOnlyList<RefundView>> RefundsAsync(long invoiceId, CancellationToken ct) =>
        await db.Set<HoanTien>().Where(r => db.Set<ThanhToan>().Any(p => p.Id == r.ThanhToanId && p.HoaDonId == invoiceId))
            .OrderBy(r => r.Id).Select(RefundProjection).ToListAsync(ct);

    public async Task<MoneyResult> RequestRefundAsync(long paymentId, RefundRequest input, Guid actor, Guid key, CancellationToken ct)
    {
        if (input.Amount is <= 0 or > MaxMoney || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
            return new(null, Invalid);
        var invoiceId = await db.Set<ThanhToan>().Where(p => p.Id == paymentId).Select(p => (long?)p.HoaDonId).SingleOrDefaultAsync(ct);
        if (invoiceId == null) return new(null, "billing.payment.not_found");
        return await WriteAsync(invoiceId.Value, input.BusinessDayId, actor, key, new { op = "refund.request", paymentId, input }, async (_, bill) =>
        {
            var payment = bill.Payments.SingleOrDefault(p => p.Id == paymentId);
            if (payment is null) return new(null, "billing.payment.not_found");
            if (payment.Status != "THANH_CONG") return new(null, "billing.payment.not_collected");
            if (input.Amount > payment.Amount) return new(null, Excess);
            var row = new HoanTien
            {
                ThanhToanId = paymentId,
                NgayBanId = input.BusinessDayId,
                SoTien = input.Amount,
                PhuongThuc = payment.Method,
                TrangThai = "CHO_DUYET",
                LyDo = input.Reason.Trim(),
                NguoiDeNghiId = actor,
                TaoLuc = DateTime.UtcNow,
                KhoaYeuCau = key
            };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return new(row.Id);
        }, ct, allowClosed: true);
    }

    public async Task<MoneyResult> ChangeRefundAsync(long refundId, RefundAction action, RefundDecision input,
        Guid actor, Guid key, CancellationToken ct)
    {
        var invoiceId = await (from refund in db.Set<HoanTien>()
                               join payment in db.Set<ThanhToan>() on refund.ThanhToanId equals payment.Id
                               where refund.Id == refundId
                               select (long?)payment.HoaDonId).SingleOrDefaultAsync(ct);
        if (invoiceId == null) return new(null, "billing.refund.not_found");
        return await WriteAsync(invoiceId.Value, input.BusinessDayId, actor, key, new { op = "refund.transition", refundId, action, input }, async (invoice, billBefore) =>
        {
            var row = await db.Set<HoanTien>().SingleOrDefaultAsync(r => r.Id == refundId &&
                db.Set<ThanhToan>().Any(p => p.Id == r.ThanhToanId && p.HoaDonId == invoiceId), ct);
            if (row is null) return new(null, "billing.refund.not_found");
            if (row.NgayBanId != input.BusinessDayId) return new(null, "billing.refund.day_mismatch");
            var next = (row.TrangThai, action) switch
            {
                ("CHO_DUYET", RefundAction.Approve) => "DA_DUYET",
                ("CHO_DUYET", RefundAction.Reject) => "TU_CHOI",
                ("DA_DUYET", RefundAction.BeginPayout) => "DANG_CHI",
                ("DA_DUYET" or "DANG_CHI", RefundAction.Unknown) => "CHO_DOI_SOAT",
                ("DA_DUYET" or "DANG_CHI", RefundAction.Success) => "THANH_CONG",
                ("CHO_DOI_SOAT", RefundAction.ConfirmPaid) => "THANH_CONG",
                ("CHO_DOI_SOAT", RefundAction.ConfirmUnpaid) => "HUY",
                _ => null
            };
            if (next is null) return new(null, "billing.refund.invalid_transition");
            if (action == RefundAction.Approve)
            {
                // WriteAsync holds the invoice lock from this check through commit. Pending requests do not reserve money.
                var claimed = await db.Set<HoanTien>().Where(r => r.ThanhToanId == row.ThanhToanId &&
                    (r.TrangThai == "DA_DUYET" || r.TrangThai == "DANG_CHI" || r.TrangThai == "CHO_DOI_SOAT" || r.TrangThai == "THANH_CONG"))
                    .SumAsync(r => r.SoTien, ct);
                if (row.SoTien > billBefore.Payments.Single(p => p.Id == row.ThanhToanId).Amount - claimed) return new(null, Excess);
            }
            var reference = input.Reference?.Trim();
            if (reference?.Length > 150 || reference?.Any(c => c < 32 || c > 126) == true ||
                (next == "THANH_CONG" && row.PhuongThuc == "CHUYEN_KHOAN" && string.IsNullOrEmpty(reference)))
                return new(null, Invalid);
            row.TrangThai = next;
            if (action is RefundAction.Approve or RefundAction.Reject) row.NguoiDuyetId = actor;
            if (action is RefundAction.BeginPayout or RefundAction.Unknown or RefundAction.Success) row.NguoiChiId ??= actor;
            if (reference is { Length: > 0 }) row.MaChiTra = reference;
            if (next == "THANH_CONG")
            {
                row.ChiLuc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                var bill = await BillingStore.ReadAsync(db, invoice, ct);
                var number = Guid.NewGuid().ToString("N");
                var document = new PrintableBill(number, "PHIEU_HOAN", row.ChiLuc.Value, actor, configuration["Billing:StoreName"],
                    configuration["Billing:StoreAddress"], bill.Id, bill.OrderId, bill.Number, input.BusinessDayId,
                    bill.Lines, bill.Obligation, bill.NetCollected, bill.Due, bill.Waivers, null)
                { Refund = await db.Set<HoanTien>().Where(r => r.Id == row.Id).Select(RefundProjection).SingleAsync(ct) };
                db.Add(new ChungTu
                {
                    HoaDonId = invoice.Id,
                    HoanTienId = refundId,
                    Loai = document.Kind,
                    SoChungTu = number,
                    NoiDungChot = JsonSerializer.Serialize(document, JsonSerializerOptions.Web),
                    TaoLuc = document.Issued,
                    NguoiLapId = actor
                });
            }
            return new(row.Id);
        }, ct, allowClosed: true);
    }

    public Task<MoneyResult> AdjustAsync(long invoiceId, ObligationChange input, Guid actor, Guid key, CancellationToken ct)
    {
        if (input.Amount is 0 or > MaxMoney or < -MaxMoney || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
            return Task.FromResult(new MoneyResult(null, Invalid));
        return WriteAsync(invoiceId, input.BusinessDayId, actor, key, new { op = "obligation.adjust", input }, async (_, bill) =>
        {
            if (bill.Obligation + input.Amount < 0) return new(null, Excess);
            var row = new DieuChinhNghiaVu
            {
                HoaDonId = invoiceId,
                NgayBanId = input.BusinessDayId,
                SoTien = input.Amount,
                LyDo = input.Reason.Trim(),
                NguoiDuyetId = actor,
                TaoLuc = DateTime.UtcNow,
                KhoaYeuCau = key
            };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return new(row.Id);
        }, ct, allowClosed: true);
    }
}
