using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Billing.Contracts;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekThreeSqlServerTests : IAsyncLifetime
{
    private ServiceProvider services = null!;
    private readonly string database = "RestaurantPhuongW3_" + Guid.NewGuid().ToString("N");
    private readonly Guid day = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Restaurant"] = new SqlConnectionStringBuilder(supplied) { InitialCatalog = database }.ConnectionString,
            ["Billing:StoreName"] = "Test kitchen"
        }).Build();
        services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging().AddRestaurantModules().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        await days.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        days.Add(new BusinessDay { Id = day, BusinessDate = new(2026, 11, 23), OpenedBy = actor, OpenedAt = DateTimeOffset.UtcNow, RequestKey = Guid.NewGuid() });
        await days.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is null) return;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            Assert.Equal(database, db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
        }
        await services.DisposeAsync();
    }

    private async Task<ChargeBatchResult> ChargeAsync(ChargeBatchRequest request, bool rollback = false)
    {
        await using var scope = services.CreateAsyncScope();
        var sales = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        await using var tx = await sales.Database.BeginTransactionAsync();
        var result = await scope.ServiceProvider.GetRequiredService<IInvoiceCharging>().ChargeConfirmedBatchAsync(request, default);
        if (!rollback && result.IsCharged) await tx.CommitAsync();
        return result;
    }
    private ChargeBatchRequest Batch() => new(7, 8, day, Guid.NewGuid(), actor, [new(42, "Phở bò", 100000)]);

    [SqlServerFact]
    public async Task Confirm_joins_sales_transaction_replays_once_and_adds_new_portions()
    {
        var batch = Batch();
        await ChargeAsync(batch, rollback: true);
        await using var scope = services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IBillingLedger>();
        Assert.Null(await ledger.ForOrderAsync(batch.OrderId, default));
        var results = await Task.WhenAll(ChargeAsync(batch), ChargeAsync(batch));
        var id = Assert.Single(results, x => !x.Replayed).InvoiceId!.Value;
        Assert.Equal(id, Assert.Single(results, x => x.Replayed).InvoiceId);
        Assert.True((await ChargeAsync(batch with { IdempotencyKey = Guid.NewGuid() })).Replayed);
        Assert.Equal(BillingErrorCodes.IdempotencyConflict, (await ChargeAsync(batch with { Actor = Guid.NewGuid() })).ErrorCode);
        Assert.Equal(id, (await ChargeAsync(batch with { BatchId = 9, IdempotencyKey = Guid.NewGuid(), Lines = [new(43, "Trà", 20000)] })).InvoiceId);
        Assert.Equal(BillingErrorCodes.LineInvalid, (await ChargeAsync(batch with { BusinessDayId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid() })).ErrorCode);
        var bill = (await ledger.FindAsync(id, default))!;
        Assert.Equal(2, bill.Lines.Count);
        Assert.Equal(120000, bill.Due);
        Assert.Equal(2, bill.Documents.Count);
        using var original = JsonDocument.Parse((await ledger.DocumentAsync(bill.Documents[0].Id, default))!);
        Assert.Equal(100000, original.RootElement.GetProperty("due").GetDecimal());
    }

    [SqlServerFact]
    public async Task Two_cashiers_cannot_overcollect_and_replay_does_not_issue_another_receipt()
    {
        var invoice = (await ChargeAsync(Batch())).InvoiceId!.Value;
        var request = new CollectPayment(day, "TIEN_MAT", 70000, 100000, null);
        async Task<(Guid Key, MoneyResult Result)> Collect(Guid key)
        {
            await using var scope = services.CreateAsyncScope();
            return (key, await scope.ServiceProvider.GetRequiredService<IBillingLedger>().CollectAsync(invoice, request, actor, key, default));
        }
        var results = await Task.WhenAll(Collect(Guid.NewGuid()), Collect(Guid.NewGuid()));
        var winner = Assert.Single(results, x => x.Result.Error is null);
        Assert.Equal("billing.amount.exceeds_available", Assert.Single(results, x => x.Result.Error is not null).Result.Error);
        Assert.True((await Collect(winner.Key)).Result.Replayed);
        await using var scope = services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IBillingLedger>();
        Assert.Equal("billing.idempotency.conflict", (await ledger.CollectAsync(invoice, request with { Amount = 30000 }, actor, winner.Key, default)).Error);
        var bill = (await ledger.FindAsync(invoice, default))!;
        Assert.Equal(30000, bill.Due);
        Assert.Equal(30000, Assert.Single(bill.Payments).Change);
        var receipt = Assert.Single(bill.Documents, x => x.Kind == "PHIEU_THU");
        using var doc = JsonDocument.Parse((await ledger.DocumentAsync(receipt.Id, default))!);
        Assert.Equal(winner.Result.ResourceId, doc.RootElement.GetProperty("payment").GetProperty("id").GetInt64());
        Assert.Equal("Test kitchen", doc.RootElement.GetProperty("storeName").GetString());
    }

    [SqlServerFact]
    public async Task Pending_transfer_reserves_balance_confirmation_and_waiver_reconcile()
    {
        var id = (await ChargeAsync(Batch())).InvoiceId!.Value;
        await using var scope = services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IBillingLedger>();
        var request = new CollectPayment(day, "CHUYEN_KHOAN", 60000, null, "TEST-REF");
        var payment = (await ledger.CollectAsync(id, request, actor, Guid.NewGuid(), default)).ResourceId!.Value;
        var pending = (await ledger.FindAsync(id, default))!;
        Assert.Equal(0, pending.NetCollected);
        Assert.Equal(40000, pending.Available);
        Assert.NotNull((await ledger.WaiveAsync(id, new(day, 40001, "Test"), actor, Guid.NewGuid(), default)).Error);
        Assert.NotNull((await ledger.WaiveAsync(id, new(day, 40000, " "), actor, Guid.NewGuid(), default)).Error);
        var waiverKey = Guid.NewGuid();
        Assert.Null((await ledger.WaiveAsync(id, new(day, 40000, "Test"), actor, waiverKey, default)).Error);
        Assert.True((await ledger.WaiveAsync(id, new(day, 40000, "Test"), actor, waiverKey, default)).Replayed);
        var confirmKey = Guid.NewGuid();
        Assert.Null((await ledger.DecideAsync(id, payment, new(day), true, actor, confirmKey, default)).Error);
        Assert.True((await ledger.DecideAsync(id, payment, new(day), true, actor, confirmKey, default)).Replayed);
        var final = (await ledger.FindAsync(id, default))!;
        Assert.Equal(0, final.Due);
        Assert.Equal(60000, final.Obligation);
        Assert.Equal(60000, final.NetCollected);
        Assert.Single(final.Documents, x => x.Kind == "PHIEU_THU");
        Assert.Single(final.Waivers);
        await ChargeAsync(Batch() with { BatchId = 10, Lines = [new(44, "Bánh", 20000)] });
        var second = await ledger.CollectAsync(id, request with { Amount = 20000 }, actor, Guid.NewGuid(), default);
        Assert.Null((await ledger.DecideAsync(id, second.ResourceId!.Value, new(day), false, actor, Guid.NewGuid(), default)).Error);
        Assert.Equal(20000, (await ledger.FindAsync(id, default))!.Available);
    }

    [SqlServerFact]
    public async Task Signed_adjustments_successful_refunds_and_closed_day_replay_preserve_the_ledger()
    {
        var id = (await ChargeAsync(Batch())).InvoiceId!.Value;
        await using var scope = services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IBillingLedger>();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var cash = new CollectPayment(day, "TIEN_MAT", 80000, 80000, null);
        var key = Guid.NewGuid();
        var payment = (await ledger.CollectAsync(id, cash, actor, key, default)).ResourceId!.Value;
        db.Add(new DieuChinhNghiaVu
        {
            HoaDonId = id,
            NgayBanId = day,
            SoTien = -15000,
            LyDo = "Điều chỉnh",
            NguoiDuyetId = actor,
            KhoaYeuCau = Guid.NewGuid(),
            TaoLuc = DateTime.UtcNow
        });
        foreach (var state in new[] { "THANH_CONG", "CHO_DUYET" })
            db.Add(new HoanTien
            {
                ThanhToanId = payment,
                NgayBanId = day,
                SoTien = 5000,
                PhuongThuc = "TIEN_MAT",
                TrangThai = state,
                LyDo = "Kiểm thử",
                NguoiDeNghiId = actor,
                TaoLuc = DateTime.UtcNow,
                KhoaYeuCau = Guid.NewGuid()
            });
        await db.SaveChangesAsync();
        var bill = (await ledger.FindAsync(id, default))!;
        Assert.Equal(85000, bill.Obligation);
        Assert.Equal(75000, bill.NetCollected);
        Assert.Equal(10000, bill.Available);
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        await days.Days.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "DA_CHOT").SetProperty(x => x.ActiveSlot, (int?)null));
        Assert.True((await ledger.CollectAsync(id, cash, actor, key, default)).Replayed);
        Assert.Equal("salesday.day.not_open", (await ledger.WaiveAsync(id, new(day, 10000, "Test"), actor, Guid.NewGuid(), default)).Error);
    }

    [SqlServerFact]
    public async Task Api_enforces_collection_waiver_and_availability_permissions_and_returns_printable_json()
    {
        var invoice = (await ChargeAsync(Batch())).InvoiceId!.Value;
        var config = services.GetRequiredService<IConfiguration>();
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = config.GetConnectionString("Restaurant") })));
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        async Task LoginAsync(string role)
        {
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "week-three-" + role, Email = $"week-three-{role}@example.invalid" };
            var password = "Aa1!" + Guid.NewGuid().ToString("N");
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            if (role != "NoRole") Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            client.DefaultRequestHeaders.Authorization = null;
            using var login = await client.PostAsJsonAsync("/api/identity/login", new { userName = user.UserName, password });
            login.EnsureSuccessStatusCode();
            using var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("accessToken").GetString());
        }
        async Task<HttpResponseMessage> PostAsync(string suffix, object body, Guid? key = null)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/billing/invoices/{invoice}/{suffix}") { Content = JsonContent.Create(body) };
            if (key is not null) message.Headers.Add("Idempotency-Key", key.Value.ToString());
            return await client.SendAsync(message);
        }
        var cash = new CollectPayment(day, "TIEN_MAT", 30000, 50000, null);
        var waiver = new WaiveBalance(day, 70000, "Kiểm thử");
        var flag = new AvailabilityChange(day, "ITEM", Guid.NewGuid(), true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/public/menu")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("payments", cash)).StatusCode);
        await LoginAsync("NoRole");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/catalog/availability", flag)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("payments", cash, Guid.NewGuid())).StatusCode);
        await LoginAsync("Staff");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("payments", cash)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("payments", cash, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync("waivers", waiver, Guid.NewGuid())).StatusCode);
        await LoginAsync("Manager");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("waivers", waiver, Guid.NewGuid())).StatusCode);
        var view = await client.GetFromJsonAsync<InvoiceView>($"/api/billing/orders/7/invoice");
        Assert.Equal(0, view!.Due);
        Assert.Equal(30000, view.NetCollected);
        var receipt = Assert.Single(view.Documents, x => x.Kind == "PHIEU_THU");
        var snapshot = await client.GetFromJsonAsync<PrintableBill>($"/api/billing/documents/{receipt.Id}");
        Assert.Equal(receipt.Number, snapshot!.Number);
        Assert.Equal(20000, snapshot.Payment!.Change);
        Assert.Equal(70000, snapshot.Due);
    }

    [SqlServerFact]
    public async Task Sold_out_filters_item_variant_topping_and_option_for_one_day_only()
    {
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogMenu>();
        var availability = scope.ServiceProvider.GetRequiredService<IMenuAvailability>();
        var pricing = scope.ServiceProvider.GetRequiredService<IMenuPricing>();
        var group = (await catalog.CreateGroupAsync(new("Trà", 1), default)).Value!;
        var item = (await catalog.CreateItemAsync(new(group.Id, "TRA", "Trà", "BAR", new("STD", "Ly", 10000)), default)).Value!;
        var variant = item.Variants.Single();
        var options = new List<Guid>();
        foreach (var kind in new[] { "TOPPING", "TUY_CHON" })
        {
            var optionGroup = (await catalog.CreateOptionGroupAsync(new(kind, kind), default)).Value!;
            var option = (await catalog.CreateOptionAsync(optionGroup.Id, new("Thêm", 2000), default)).Value!;
            Assert.Null((await catalog.AssignOptionGroupAsync(item.Id, optionGroup.Id, new(1, 1), default)).Error);
            options.Add(option.Id);
        }
        var request = new MenuPricingRequest(day, [new(1, item.Id, variant.Id, options)]);
        Assert.Equal(14000, Assert.Single((await pricing.PriceAsync(request, default)).Lines).UnitPrice);
        foreach (var (kind, target) in new[] { ("ITEM", item.Id), ("VARIANT", variant.Id), ("TOPPING", options[0]), ("OPTION", options[1]) })
        {
            Assert.Null(await availability.SetAsync(new(day, kind, target, true), actor, default));
            Assert.Empty((await availability.PublicAsync(default)).Groups);
            Assert.False((await pricing.PriceAsync(request, default)).IsValid);
            Assert.Null(await availability.SetAsync(new(day, kind, target, false), actor, default));
            Assert.Single((await availability.PublicAsync(default)).Groups);
        }
        Assert.Equal("catalog.target.not_found", await availability.SetAsync(new(day, "TOPPING", options[1], true), actor, default));
        Assert.Null(await availability.SetAsync(new(day, "ITEM", item.Id, true), actor, default));
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Assert.Equal(actor, (await db.Set<HetMon>().AsNoTracking().SingleAsync()).NguoiCapNhatId);
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        await days.Days.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "DA_CHOT").SetProperty(x => x.ActiveSlot, (int?)null));
        days.Add(new BusinessDay { Id = Guid.NewGuid(), BusinessDate = new(2026, 11, 24), OpenedBy = actor, OpenedAt = DateTimeOffset.UtcNow, RequestKey = Guid.NewGuid() });
        await days.SaveChangesAsync();
        Assert.Single((await availability.PublicAsync(default)).Groups);
        Assert.Equal("salesday.day.not_open", await availability.SetAsync(new(day, "ITEM", item.Id, false), actor, default));
    }
}
