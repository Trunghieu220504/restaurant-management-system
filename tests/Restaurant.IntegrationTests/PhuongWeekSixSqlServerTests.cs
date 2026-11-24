using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Reporting.Application;
using Restaurant.Modules.SalesDay.Application;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekSixSqlServerTests : DayLedgerSqlFixture
{
    [SqlServerFact]
    public async Task Concurrent_refunds_reserve_collected_money_and_old_invoice_is_refunded_on_execution_day()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var db = p.GetRequiredService<BillingDbContext>();
        var ledger = p.GetRequiredService<IBillingLedger>();
        var invoice = new HoaDon { DonHangId = 42, NgayBanLapId = day, MaHoaDon = "HD42", TaoLuc = DateTime.UtcNow };
        db.Add(invoice);
        await db.SaveChangesAsync();
        db.Add(new DongHoaDon { HoaDonId = invoice.Id, ChiTietDonId = 7, TenMonChot = "Tea", ThanhTienGoc = 100 });
        await db.SaveChangesAsync();
        var collected = await ledger.CollectAsync(invoice.Id, new(day, "TIEN_MAT", 100, 100, null), actor, Guid.NewGuid(), default);
        Assert.Null(collected.Error);
        await db.Set<HoaDon>().Where(h => h.Id == invoice.Id).ExecuteUpdateAsync(s => s.SetProperty(h => h.DongLuc, DateTime.UtcNow));
        var settlement = p.GetRequiredService<IDaySettlement>();
        await BeginAsync(p);
        var first = (await settlement.FinalizeAsync(day, new(600, null, false, null), actor, Guid.NewGuid(), default)).Snapshot!;
        var next = (await p.GetRequiredService<IBusinessDays>().OpenAsync(new(first.BusinessDate.AddDays(1), 600), actor, Guid.NewGuid(), default)).Day!;
        var payment = collected.ResourceId!.Value;
        async Task<MoneyResult> Reserve(long amount)
        {
            await using var other = services!.CreateAsyncScope();
            return await other.ServiceProvider.GetRequiredService<IBillingLedger>().RequestRefundAsync(payment,
                new(next.Id, amount, "Customer request"), actor, Guid.NewGuid(), default);
        }
        var requests = await Task.WhenAll(Reserve(60), Reserve(60));
        Assert.All(requests, request => Assert.Null(request.Error));
        async Task<MoneyResult> Approve(MoneyResult request)
        {
            await using var other = services!.CreateAsyncScope();
            return await other.ServiceProvider.GetRequiredService<IBillingLedger>().ChangeRefundAsync(request.ResourceId!.Value,
                RefundAction.Approve, new(next.Id), actor, Guid.NewGuid(), default);
        }
        var race = await Task.WhenAll(requests.Select(Approve));
        var refund = Assert.Single(race, r => r.Error == null).ResourceId!.Value;
        Assert.Single(race, r => r.Error == "billing.amount.exceeds_available");
        var pending = requests.Single(r => r.ResourceId != refund);
        Task<MoneyResult> Transition(RefundAction action, Guid key) =>
            ledger.ChangeRefundAsync(refund, action, new(next.Id), actor, key, default);
        Assert.Null((await Transition(RefundAction.BeginPayout, Guid.NewGuid())).Error);
        Assert.Null((await Transition(RefundAction.Unknown, Guid.NewGuid())).Error);
        Assert.Equal("CHO_DOI_SOAT", (await ledger.RefundsAsync(invoice.Id, default)).Single(r => r.Id == refund).Status);
        Assert.Equal("billing.amount.exceeds_available", (await Approve(pending)).Error);
        Assert.Contains((await p.GetRequiredService<IDayClosing>().ChecklistAsync(next.Id, default)).Checklist!.Blockers,
            b => b.Code == "salesday.pending_refunds");
        Assert.Null((await ledger.ChangeRefundAsync(pending.ResourceId!.Value, RefundAction.Reject, new(next.Id), actor, Guid.NewGuid(), default)).Error);
        Assert.NotNull((await Transition(RefundAction.Success, Guid.NewGuid())).Error);
        var payKey = Guid.NewGuid();
        Assert.Null((await Transition(RefundAction.ConfirmPaid, payKey)).Error);
        Assert.True((await Transition(RefundAction.ConfirmPaid, payKey)).Replayed);
        var bill = (await ledger.FindAsync(invoice.Id, default))!;
        Assert.Equal(100, bill.Obligation);
        Assert.Equal(40, bill.NetCollected);
        Assert.Single(bill.Documents, d => d.Kind == "PHIEU_HOAN");
        Assert.Null((await ledger.AdjustAsync(invoice.Id, new(next.Id, -60, "Commercial adjustment"), actor, Guid.NewGuid(), default)).Error);
        Assert.Equal(0, (await ledger.FindAsync(invoice.Id, default))!.Due);
        Assert.Null((await p.GetRequiredService<IDayClosing>().ChangeAsync(next.Id, false, actor, Guid.NewGuid(), default)).Error);
        var second = await settlement.FinalizeAsync(next.Id, new(540, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(second.Error);
        var report = await p.GetRequiredService<IBusinessReports>().ReadAsync(first.BusinessDate, next.BusinessDate, default);
        Assert.Equal(2, report.Days.Count);
        Assert.Equal(100, report.Days[0].NetRevenue);
        Assert.Equal(0, report.Days[0].Refunds);
        Assert.Equal(-60, report.Days[1].NetRevenue);
        Assert.Equal(60, report.Days[1].Refunds);
        Assert.Equal(40, report.Methods.Single(m => m.Method == "TIEN_MAT").Net);
        Assert.Equal(1, Assert.Single(report.Items).Portions);
        Assert.Equal(100, (await settlement.SnapshotAsync(day, default))!.NetRevenue);
    }

    [SqlServerFact]
    public async Task Replacing_a_posted_receipt_and_adjusting_stock_are_audited_once_and_flow_into_report()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var audit = p.GetRequiredService<AuditDbContext>();
        await audit.Database.MigrateAsync();
        var corrections = p.GetRequiredService<IInventoryCorrections>();
        var stock = p.GetRequiredService<IInventory>();
        var (item, unit) = await MaterialAsync(p, "TEA");
        await PurchaseAsync(p, item, unit, 10, 5, true);
        var old = Assert.Single(await stock.PurchasesAsync(day, default));
        var input = new PurchaseCorrection(new(day, null, true, null, [new(item, unit, 12, 5)]), "Counted twelve on delivery");
        var editKey = Guid.NewGuid();
        var edit = await corrections.CorrectAsync(old.Id, input, actor, editKey, default);
        Assert.Null(edit.Error);
        Assert.Equal(edit, await corrections.CorrectAsync(old.Id, input, actor, editKey, default));
        Assert.Equal("HUY", (await stock.PurchaseAsync(old.Id, default))!.Header.Status);
        Assert.Equal(60, (await stock.PurchaseAsync(edit.Id!.Value, default))!.Total);
        var delta = new StockAdjustment(day, item, -2, -10, "Two damaged");
        var key = Guid.NewGuid();
        await audit.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER audit.FailInventoryAudit ON audit.NhatKy AFTER INSERT AS
            BEGIN THROW 51089, 'Injected inventory audit failure', 1; END
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => corrections.AdjustAsync(delta, actor, key, default));
        await audit.Database.ExecuteSqlRawAsync("DROP TRIGGER audit.FailInventoryAudit");
        var adjusted = await corrections.AdjustAsync(delta, actor, key, default);
        Assert.Null(adjusted.Error);
        Assert.Equal(adjusted, await corrections.AdjustAsync(delta, actor, key, default));
        Assert.Equal("inventory.idempotency.conflict", (await corrections.AdjustAsync(delta with { Value = -12 }, actor, key, default)).Error);
        Assert.Equal(2, await audit.Entries.CountAsync(a => a.BusinessDayId == day && a.ActorId == actor));
        await ApproveAsync(p, item, 4);
        await BeginAsync(p);
        var result = await p.GetRequiredService<IDaySettlement>().FinalizeAsync(day, new(440, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(result.Error);
        var snapshot = result.Snapshot!;
        var report = await p.GetRequiredService<IBusinessReports>().ReadAsync(snapshot.BusinessDate, snapshot.BusinessDate, default);
        Assert.Equal(30, report.Cost);
        Assert.Equal(snapshot.GrossProfit, report.GrossProfit);
        Assert.Equal(60, report.DrawerPurchases);
        Assert.Equal(20m, Assert.Single(report.Stock).ClosingValue);
        Assert.Equal(edit, await corrections.CorrectAsync(old.Id, input, actor, editKey, default));
        Assert.Equal(adjusted, await corrections.AdjustAsync(delta, actor, key, default));
        Assert.Equal("salesday.day.not_open", (await corrections.AdjustAsync(delta, actor, Guid.NewGuid(), default)).Error);
    }
}
