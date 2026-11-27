using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class BillingRefundReviewSqlServerTests : DayLedgerSqlFixture
{
    [SqlServerFact]
    public async Task Cash_receipt_can_commit_while_another_invoices_payment_is_uncommitted()
    {
        await using var scope = services!.CreateAsyncScope();
        var billing = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var connection = new SqlConnectionStringBuilder(billing.Database.GetConnectionString()!);
        var database = connection.InitialCatalog;
        Assert.StartsWith("RestaurantDayLedger_", database);
        connection.InitialCatalog = "master";
        await using (var admin = new SqlConnection(connection.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = admin.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{database}] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE";
            await command.ExecuteNonQueryAsync();
        }

        var held = new HoaDon { DonHangId = 1, MaHoaDon = "HD1", NgayBanLapId = day, TaoLuc = DateTime.UtcNow };
        var independent = new HoaDon { DonHangId = 2, MaHoaDon = "HD2", NgayBanLapId = day, TaoLuc = DateTime.UtcNow };
        billing.AddRange(held, independent);
        await billing.SaveChangesAsync();
        billing.Add(new DongHoaDon { HoaDonId = independent.Id, ChiTietDonId = 2, TenMonChot = "Tea", ThanhTienGoc = 100 });
        await billing.SaveChangesAsync();

        await using var transaction = await billing.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        billing.Add(new ThanhToan
        {
            HoaDonId = held.Id,
            NgayBanId = day,
            SoTien = 100,
            PhuongThuc = "TIEN_MAT",
            TienKhachDua = 100,
            TienThua = 0,
            TrangThai = "THANH_CONG",
            NguoiThuId = actor,
            TaoLuc = now,
            XacNhanLuc = now,
            KhoaYeuCau = Guid.NewGuid()
        });
        await billing.SaveChangesAsync();

        await using var request = services!.CreateAsyncScope();
        var ledger = request.ServiceProvider.GetRequiredService<IBillingLedger>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await ledger.CollectAsync(independent.Id, new(day, "TIEN_MAT", 100, 100, null), actor, Guid.NewGuid(), timeout.Token);
        Assert.Null(result.Error);
        var invoice = await ledger.FindAsync(independent.Id, timeout.Token);
        Assert.NotNull(invoice);
        Assert.Equal(0, invoice.Due);
        Assert.Equal(result.ResourceId, Assert.Single(invoice.Payments).Id);
        Assert.Equal("PHIEU_THU", Assert.Single(invoice.Documents).Kind);
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Refund_approval_does_not_record_a_bank_payout_reference()
    {
        await using var scope = services!.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var billing = provider.GetRequiredService<BillingDbContext>();
        var ledger = provider.GetRequiredService<IBillingLedger>();
        var invoice = new HoaDon { DonHangId = 27, MaHoaDon = "HD27", NgayBanLapId = day, TaoLuc = DateTime.UtcNow };
        billing.Add(invoice);
        await billing.SaveChangesAsync();
        billing.Add(new DongHoaDon { HoaDonId = invoice.Id, ChiTietDonId = 27, TenMonChot = "Tea", ThanhTienGoc = 100 });
        await billing.SaveChangesAsync();
        var payment = await ledger.CollectAsync(invoice.Id, new(day, "CHUYEN_KHOAN", 100, null, "IN-27"), actor, Guid.NewGuid(), default);
        Assert.Null(payment.Error);
        Assert.Null((await ledger.DecideAsync(invoice.Id, payment.ResourceId!.Value, new(day), true, actor, Guid.NewGuid(), default)).Error);
        var refund = await ledger.RequestRefundAsync(payment.ResourceId.Value, new(day, 60, "Partial refund"), actor, Guid.NewGuid(), default);
        Assert.Null(refund.Error);

        var reviewer = Guid.NewGuid();
        Assert.Null((await ledger.ChangeRefundAsync(refund.ResourceId!.Value, RefundAction.Approve,
            new(day, "REVIEW-NOTE"), reviewer, Guid.NewGuid(), default)).Error);
        var approved = Assert.Single(await ledger.RefundsAsync(invoice.Id, default));
        Assert.Equal("DA_DUYET", approved.Status);
        Assert.Equal(reviewer, approved.ApprovedBy);
        Assert.Null(approved.PaidBy);
        Assert.Null(approved.Reference);
        Assert.Equal("billing.input.invalid", (await ledger.ChangeRefundAsync(approved.Id, RefundAction.Success,
            new(day), actor, Guid.NewGuid(), default)).Error);

        Assert.Null((await ledger.ChangeRefundAsync(approved.Id, RefundAction.Success,
            new(day, "OUT-27"), actor, Guid.NewGuid(), default)).Error);
        var paid = Assert.Single(await ledger.RefundsAsync(invoice.Id, default));
        Assert.Equal("THANH_CONG", paid.Status);
        Assert.Equal(actor, paid.PaidBy);
        Assert.Equal("OUT-27", paid.Reference);
    }
}
