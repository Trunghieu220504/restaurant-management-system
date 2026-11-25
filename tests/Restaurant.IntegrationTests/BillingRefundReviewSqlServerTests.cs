using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class BillingRefundReviewSqlServerTests : DayLedgerSqlFixture
{
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
