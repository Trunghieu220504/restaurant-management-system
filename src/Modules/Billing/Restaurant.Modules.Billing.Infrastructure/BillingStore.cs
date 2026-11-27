using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Modules.Billing.Infrastructure;

internal static class BillingStore
{
    internal static string Fingerprint(object input) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));

    // Transaction-owned locks also serialize the first insert, before there is a row to lock.
    internal static Task LockAsync(BillingDbContext db, string resource, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int;
            EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @r < 0 THROW 51001, 'Billing lock unavailable', 1;
            """, ct);

    internal static Task<HoaDon?> LockInvoiceAsync(BillingDbContext db, long id, CancellationToken ct) =>
        db.Set<HoaDon>().FromSqlInterpolated($"SELECT * FROM billing.HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct);

    internal static string Description(DongHoaDon row)
    {
        if (string.IsNullOrEmpty(row.NoiDungTuyChonChot)) return row.TenMonChot;
        return row.NoiDungTuyChonChot.StartsWith(row.TenMonChot, StringComparison.Ordinal)
            ? row.NoiDungTuyChonChot : $"{row.TenMonChot} · {row.NoiDungTuyChonChot}";
    }

    internal static async Task<InvoiceView> ReadAsync(BillingDbContext db, HoaDon invoice, CancellationToken ct)
    {
        var id = invoice.Id;
        var rows = await db.Set<DongHoaDon>().AsNoTracking().Where(x => x.HoaDonId == id).OrderBy(x => x.Id)
            .ToListAsync(ct);
        var lines = rows.Select(x => new BillLine(x.ChiTietDonId, Description(x), x.ThanhTienGoc)).ToArray();
        // Lock-based READ COMMITTED can otherwise scan another invoice's uncommitted payment to satisfy the ordering.
        // Seek the existing invoice index so this read stays on the invoice already locked by the caller.
        var invoicePayments = db.Set<ThanhToan>().FromSqlInterpolated($"""
            SELECT * FROM billing.ThanhToan WITH (FORCESEEK (IX_ThanhToan_HoaDonId (HoaDonId))) WHERE HoaDonId = {id}
            """);
        var payments = await invoicePayments.OrderBy(x => x.Id)
            .Select(x => new BillPayment(x.Id, x.PhuongThuc, x.SoTien, x.TienKhachDua, x.TienThua, x.MaThamChieuNganHang, x.TrangThai,
                x.NgayBanId, x.NguoiThuId, x.TaoLuc, x.XacNhanLuc)).ToListAsync(ct);
        var waivers = await db.Set<MienThu>().Where(x => x.HoaDonId == id).OrderBy(x => x.Id)
            .Select(x => new BillWaiver(x.Id, x.SoTien, x.LyDo, x.NgayBanId, x.NguoiDuyetId, x.TaoLuc)).ToListAsync(ct);
        var documents = await db.Set<ChungTu>().Where(x => x.HoaDonId == id).OrderBy(x => x.Id)
            .Select(x => new BillDocument(x.Id, x.SoChungTu, x.Loai, x.TaoLuc)).ToListAsync(ct);
        var adjustments = await db.Set<DieuChinhNghiaVu>().Where(x => x.HoaDonId == id).SumAsync(x => x.SoTien, ct);
        var refunds = await (from refund in db.Set<HoanTien>()
                             join payment in invoicePayments on refund.ThanhToanId equals payment.Id
                             where refund.TrangThai == "THANH_CONG"
                             select refund.SoTien).SumAsync(ct);
        var obligation = Math.Max(0, lines.Sum(x => x.Amount) + adjustments - waivers.Sum(x => x.Amount));
        var net = payments.Where(x => x.Status == "THANH_CONG").Sum(x => x.Amount) - refunds;
        var due = Math.Max(0, obligation - net);
        var reserved = payments.Where(x => x.Status == "CHO_XAC_NHAN").Sum(x => x.Amount);
        return new(id, invoice.DonHangId, invoice.MaHoaDon, invoice.NgayBanLapId, obligation, net, due,
            reserved, Math.Max(0, due - reserved), lines, payments, waivers, documents);
    }

    internal static async Task AddDocumentAsync(BillingDbContext db, HoaDon invoice, ThanhToan? payment,
        Guid actor, Guid documentDayId, string? storeName, string? storeAddress, CancellationToken ct)
    {
        var bill = await ReadAsync(db, invoice, ct);
        var number = Guid.NewGuid().ToString("N");
        var kind = payment is null ? "HOA_DON" : "PHIEU_THU";
        var issued = DateTime.UtcNow;
        var snapshot = new PrintableBill(number, kind, issued, actor, storeName, storeAddress, bill.Id, bill.OrderId,
            bill.Number, documentDayId, bill.Lines, bill.Obligation, bill.NetCollected, bill.Due,
            bill.Waivers, payment is null ? null : bill.Payments.Single(x => x.Id == payment.Id));
        db.Add(new ChungTu
        {
            HoaDonId = bill.Id,
            SoChungTu = number,
            Loai = kind,
            ThanhToanId = payment?.Id,
            NoiDungChot = JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web),
            TaoLuc = issued,
            NguoiLapId = actor
        });
    }
}
