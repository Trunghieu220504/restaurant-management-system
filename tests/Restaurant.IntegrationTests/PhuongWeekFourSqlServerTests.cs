using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Contracts;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekFourSqlServerTests : IAsyncLifetime
{
    private readonly Guid day = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly string database = "RestaurantPhuongW4_" + Guid.NewGuid().ToString("N");
    private ServiceProvider? services;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Restaurant"] = new SqlConnectionStringBuilder(server) { InitialCatalog = database }.ConnectionString
        }).Build();
        services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging().AddRestaurantModules().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var days = provider.GetRequiredService<SalesDayDbContext>();
        await days.Database.MigrateAsync();
        await BusinessAuditTestSupport.MigrateAsync(days.Database.GetConnectionString()!);
        await provider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<SeatingDbContext>().Database.MigrateAsync();
        days.Add(new BusinessDay
        {
            Id = day,
            BusinessDate = new(2026, 12, 3),
            OpenedAt = DateTimeOffset.UtcNow,
            OpenedBy = actor,
            RequestKey = Guid.NewGuid()
        });
        await days.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is null) return;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
            Assert.Equal(database, db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
        }
        await services.DisposeAsync();
    }

    [SqlServerFact]
    public async Task Purchase_snapshots_survive_conversion_edits_and_stock_blocks_structural_changes()
    {
        await using var scope = services!.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<IInventory>();
        var kg = (await stock.UnitAsync(new("KG", "Kilogram", "KHOI_LUONG"), default)).Id!.Value;
        var bag = (await stock.UnitAsync(new("BAG", "Bag", "DEM"), default)).Id!.Value;
        var input = new IngredientInput(day, "RICE", "Rice", kg, "THEO_DOI_TON", false);
        var item = (await stock.IngredientAsync(null, input, default)).Id!.Value;
        Assert.Null((await stock.ConversionAsync(item, bag, new(day, 5), default)).Error);
        var request = new PurchaseInput(day, null, true, null, [new(item, bag, 2, 90000)]);
        var key = Guid.NewGuid();
        var id = (await stock.DraftAsync(request, actor, key, default)).Id!.Value;
        Assert.Equal(id, (await stock.DraftAsync(request, actor, key, default)).Id);
        Assert.Null((await stock.TransitionAsync(id, day, true, default)).Error);
        Assert.Null((await stock.ConversionAsync(item, bag, new(day, 8), default)).Error);
        var purchase = (await stock.PurchaseAsync(id, default))!;
        Assert.True(purchase.Header.CashDrawer);
        Assert.Equal("HIEU_LUC", purchase.Header.Status);
        Assert.Equal(180000, purchase.Total);
        Assert.Equal(10, Assert.Single(purchase.Lines).SoLuongChuan);
        Assert.Equal(5, Assert.Single(purchase.Lines).HeSoChot);
        foreach (var change in new[] { input with { Archived = true }, input with { Mode = "TINH_KHI_MUA" }, input with { BaseUnitId = bag } })
            Assert.Equal("inventory.ingredient.has_stock_or_movements", (await stock.IngredientAsync(item, change, default)).Error);
        var closing = scope.ServiceProvider.GetRequiredService<IDayClosing>();
        var result = await closing.ChangeAsync(day, false, actor, Guid.NewGuid(), default);
        Assert.Equal("salesday.close.blocked", result.Error);
        Assert.Contains(new CloseBlocker("salesday.unapproved_counts", 1), result.Checklist!.Blockers);
    }

    [SqlServerFact]
    public async Task Carried_stock_blocks_archive_even_without_a_current_day_purchase()
    {
        await using var scope = services!.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<IInventory>();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var unit = (await stock.UnitAsync(new("L", "Litre", "THE_TICH"), default)).Id!.Value;
        var input = new IngredientInput(day, "MILK", "Milk", unit, "THEO_DOI_TON", false);
        var item = (await stock.IngredientAsync(null, input, default)).Id!.Value;
        db.Add(new KiemKeTon
        {
            NgayBanId = Guid.NewGuid(),
            NguyenLieuId = item,
            CheDoChot = input.Mode,
            SoDemDuyet = 4,
            GiaTriTonCuoi = 80000,
            NguoiDuyetId = actor,
            DuyetLuc = DateTimeOffset.UtcNow.AddDays(-1),
            ChotLuc = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        Assert.Equal("inventory.ingredient.has_stock_or_movements", (await stock.IngredientAsync(item, input with { Archived = true }, default)).Error);
        Assert.Empty(await stock.PurchasesAsync(day, default));
    }

    [SqlServerFact]
    public async Task Draft_blocks_freeze_cancel_restores_writes_and_replays_do_not_reverse_newer_actions()
    {
        await using var scope = services!.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<IInventory>();
        var closing = scope.ServiceProvider.GetRequiredService<IDayClosing>();
        var unit = (await stock.UnitAsync(new("PC", "Piece", "DEM"), default)).Id!.Value;
        var item = (await stock.IngredientAsync(null, new(day, "BOX", "Box", unit, "TINH_KHI_MUA", false), default)).Id!.Value;
        var request = new PurchaseInput(day, null, false, null, [new(item, unit, 1, 50)]);
        var id = (await stock.DraftAsync(request, actor, Guid.NewGuid(), default)).Id!.Value;
        Assert.Contains(new CloseBlocker("salesday.draft_purchases", 1), (await closing.ChecklistAsync(day, default)).Checklist!.Blockers);
        Assert.Null((await stock.TransitionAsync(id, day, false, default)).Error);
        var key = Guid.NewGuid();
        Assert.Equal("CHO_CHOT", (await closing.ChangeAsync(day, false, actor, key, default)).Checklist!.Status);
        Assert.Equal("salesday.day.not_open", (await stock.DraftAsync(request, actor, Guid.NewGuid(), default)).Error);
        Assert.Equal("DANG_BAN", (await closing.ChangeAsync(day, true, actor, Guid.NewGuid(), default)).Checklist!.Status);
        Assert.Equal("CHO_CHOT", (await closing.ChangeAsync(day, false, actor, key, default)).Checklist!.Status);
        Assert.Null((await stock.DraftAsync(request, actor, Guid.NewGuid(), default)).Error);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<SalesDayDbContext>().Set<ClosingEntry>().CountAsync());
    }

    [SqlServerFact]
    public async Task Reordering_after_full_payment_reopens_balance_and_close_checks_both_modules()
    {
        await using var scope = services!.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var sales = provider.GetRequiredService<SalesDbContext>();
        var order = DonHang.Create("W4", OrderTypes.Takeaway, day, null, null, null, actor, DateTime.UtcNow).Value!;
        sales.Add(order);
        await sales.SaveChangesAsync();
        var ledger = provider.GetRequiredService<IBillingLedger>();
        var closing = provider.GetRequiredService<ICloseOrder>();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        async Task<(long Invoice, long Portion)> Charge(long amount)
        {
            // Load a fresh aggregate even after a close attempt cleared the tracked order.
            var tracked = await sales.Orders.Include(o => o.Dots).ThenInclude(d => d.Portions).SingleAsync(o => o.Id == order.Id);
            var batch = tracked.AddBatch(BatchSource.Staff, null, actor, Guid.NewGuid(),
                [new NewPortion(itemId, variantId, "Tea", "Large", amount, Stations.Bar, null)], DateTime.UtcNow).Value!;
            Assert.True(tracked.ConfirmBatch(batch.SoDot, actor, DateTime.UtcNow).Succeeded);
            await using var tx = await sales.Database.BeginTransactionAsync();
            await sales.SaveChangesAsync();
            var portion = Assert.Single(batch.Portions).Id;
            var result = await provider.GetRequiredService<IInvoiceCharging>().ChargeConfirmedBatchAsync(
                new(order.Id, batch.Id, day, Guid.NewGuid(), actor, [new(portion, "Tea", amount)]), default);
            Assert.Null(result.ErrorCode);
            await tx.CommitAsync();
            return (result.InvoiceId!.Value, portion);
        }
        var first = await Charge(100);
        Assert.Null((await ledger.CollectAsync(first.Invoice, new(day, "TIEN_MAT", 100, 100, null), actor, Guid.NewGuid(), default)).Error);
        Assert.Equal("DA_THU_DU", (await ledger.FindAsync(first.Invoice, default))!.PaymentStatus);
        var extra = await Charge(40);
        Assert.Equal(first.Invoice, extra.Invoice);
        var balance = (await ledger.FindAsync(first.Invoice, default))!;
        Assert.Equal("THU_MOT_PHAN", balance.PaymentStatus);
        Assert.Equal(40, balance.Due);
        Assert.Equal("sales.order.processing_incomplete", (await closing.CloseAsync(order.Id, day, default)).Error);
        // Minimal provider fixture for the ticket/incident read contract from InitialFulfillment.
        await sales.Database.ExecuteSqlRawAsync("CREATE SCHEMA fulfillment");
        await sales.Database.ExecuteSqlRawAsync("""
            CREATE TABLE fulfillment.PhieuCheBien(Id bigint PRIMARY KEY, ChiTietDonId bigint NOT NULL, Lan int NOT NULL, TrangThai varchar(32) NOT NULL);
            CREATE TABLE fulfillment.SuCoMon(Id bigint PRIMARY KEY, PhieuCheBienId bigint NOT NULL, TrangThai varchar(32) NOT NULL, QuyetDinh varchar(32));
            """);
        const long firstTicket = 1;
        const long extraTicket = 2;
        await sales.Database.ExecuteSqlInterpolatedAsync($"INSERT fulfillment.PhieuCheBien VALUES ({firstTicket},{first.Portion},1,'DA_GIAO'),({extraTicket},{extra.Portion},1,'DA_GIAO')");
        Assert.Equal("billing.invoice.unsettled", (await closing.CloseAsync(order.Id, day, default)).Error);
        Assert.Null((await ledger.CollectAsync(extra.Invoice, new(day, "TIEN_MAT", 40, 40, null), actor, Guid.NewGuid(), default)).Error);
        Assert.Null((await closing.CloseAsync(order.Id, day, default)).Error);
        Assert.True((await closing.CloseAsync(order.Id, day, default)).Replayed);
        Assert.Equal(1, await sales.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM audit.NhatKy WHERE Action='sales.order.closed'").SingleAsync());
        Assert.Equal(OrderStatus.Closed, (await sales.Orders.AsNoTracking().SingleAsync()).TrangThai);
    }
}
