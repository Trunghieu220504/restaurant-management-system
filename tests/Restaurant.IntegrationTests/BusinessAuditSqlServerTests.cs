using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Xunit;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;
using Microsoft.EntityFrameworkCore.Storage;

namespace Restaurant.IntegrationTests;

public sealed class BusinessAuditSqlServerTests : IAsyncLifetime
{
    private readonly string database = "RestaurantBusinessAuditTest_" + Guid.NewGuid().ToString("N");
    private readonly Guid actor = Guid.NewGuid();
    private readonly Guid day = Guid.NewGuid();
    private ServiceProvider? services;
    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = database }.ConnectionString;
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connection }).Build());
        registrations.AddLogging().AddRestaurantModules();
        registrations.AddSingleton<IMenuPricing>(new MenuStub());
        services = registrations.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        await days.Database.MigrateAsync();
        await BusinessAuditTestSupport.MigrateAsync(connection);
        await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SeatingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>().Database.MigrateAsync();
        days.Days.Add(new BusinessDay
        {
            Id = day,
            BusinessDate = new(2026, 11, 25),
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
            var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            if (db.Database.GetDbConnection().Database != database || !database.StartsWith("RestaurantBusinessAuditTest_", StringComparison.Ordinal))
                throw new InvalidOperationException();
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync();
        }
        await services.DisposeAsync();
    }

    [SqlServerFact]
    public async Task Sales_commands_append_actor_day_and_entity_without_duplicate_audit_on_replay()
    {
        services!.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.ToString())], "test"))
        };
        await using var scope = services!.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IOrderCommands>();
        var order = await commands.CreateOrderAsync(new(OrderTypes.Takeaway, null, day, "Customer name never audited", actor), default);
        Assert.True(order.Succeeded, order.Error);
        var id = order.Value!.Id;
        var input = new AddBatchCommand(id, BatchSource.Staff, null, actor, Guid.NewGuid(),
            [new BatchLine(Guid.NewGuid(), Guid.NewGuid(), [], "private note not audited")]);
        var first = await commands.AddBatchAsync(input, default);
        Assert.True(first.Succeeded, first.Error);
        Assert.True((await commands.AddBatchAsync(input, default)).Succeeded);
        Assert.True((await commands.ConfirmBatchAsync(new(id, first.Value!.Number, actor), default)).Succeeded);
        var second = await commands.AddBatchAsync(input with { RequestKey = Guid.NewGuid() }, default);
        Assert.True((await commands.RejectBatchAsync(new(id, second.Value!.Number, actor, "Customer declined"), default)).Succeeded);
        var third = await commands.AddBatchAsync(input with { RequestKey = Guid.NewGuid() }, default);
        Assert.True((await commands.CancelBatchAsync(new(id, third.Value!.Number), default)).Succeeded);
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var rows = await audit.Entries.AsNoTracking().Where(x => x.SourceModule == "Sales").ToArrayAsync();
        Assert.Single(rows, x => x.Action == "sales.order.created");
        Assert.Equal(3, rows.Count(x => x.Action == "sales.batch.added"));
        Assert.Single(rows, x => x.Action == "sales.batch.confirmed");
        Assert.Single(rows, x => x.Action == "sales.batch.rejected");
        Assert.Single(rows, x => x.Action == "sales.batch.cancelled");
        Assert.All(rows, x => Assert.Equal(day, x.BusinessDayId));
        Assert.All(rows.Where(x => x.Action is "sales.order.created" or "sales.batch.added" or "sales.batch.confirmed"), x => Assert.Equal(actor, x.ActorId));
        Assert.DoesNotContain(rows, x => (x.Reason ?? "").Contains("private note") || x.EntityId.Contains("Customer"));
        var fourth = await commands.AddBatchAsync(input with { RequestKey = Guid.NewGuid() }, default);
        Assert.True(fourth.Succeeded);
        var sales = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var portion = await sales.Portions.AsNoTracking().SingleAsync(x => x.DotGoiMonId == fourth.Value!.BatchId);
        var staff = scope.ServiceProvider.GetRequiredService<IStaffPortions>();
        Assert.Null(await staff.ChangeAsync(portion.Id, new(Convert.ToBase64String(portion.Version), "No spice", "Requested"), false, actor, default));
        portion = await sales.Portions.AsNoTracking().SingleAsync(x => x.Id == portion.Id);
        Assert.Null(await staff.ChangeAsync(portion.Id, new(Convert.ToBase64String(portion.Version), null, "Cancel request"), true, actor, default));
        var portionAudit = await audit.Entries.AsNoTracking().Where(x => x.EntityType == "ChiTietDon").ToArrayAsync();
        Assert.Equal(2, portionAudit.Length);
        Assert.All(portionAudit, x => Assert.Equal(actor, x.ActorId));
        Assert.Contains(portionAudit, x => x.Action == "sales.portion.edited");
        Assert.Contains(portionAudit, x => x.Action == "sales.portion.cancelled");
    }

    [SqlServerFact]
    public async Task Seating_and_guest_commands_use_real_atomic_audit_without_storing_access_secrets()
    {
        await using var scope = services!.CreateAsyncScope();
        var http = services!.GetRequiredService<IHttpContextAccessor>();
        http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.ToString())], "test")) };
        var catalog = scope.ServiceProvider.GetRequiredService<ISeatingCatalog>();
        var sessions = scope.ServiceProvider.GetRequiredService<ISeatingSessions>();
        var guests = scope.ServiceProvider.GetRequiredService<IGuestAccess>();
        var area = (await catalog.CreateAreaAsync(new("A", "Area"), default)).Value!;
        var first = (await catalog.CreateTableAsync(new(area.Id, "T1", "First"), default)).Value!;
        var second = (await catalog.CreateTableAsync(new(area.Id, "T2", "Second"), default)).Value!;
        var openKey = Guid.NewGuid();
        var session = (await sessions.OpenAsync(first.Id, actor, openKey, default)).Value!;
        Assert.NotNull(session);
        Assert.Null((await sessions.OpenAsync(first.Id, actor, openKey, default)).Error);
        var invite = (await guests.InviteAsync(session.Id, actor, default)).Value!;
        // Guest semantics must not inherit the staff identity even if an authenticated principal is ambient.
        var token = (await guests.ExchangeAsync(new(first.PublicQrCode, invite.JoinCode), default)).Value!;
        Assert.NotNull(token);
        Assert.Null((await sessions.TransferAsync(session.Id, new(second.Id, "Move"), actor, Guid.NewGuid(), default)).Error);
        Assert.Null((await sessions.CloseAsync(session.Id, actor, Guid.NewGuid(), default)).Error);
        var rows = await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Entries.AsNoTracking().ToArrayAsync();
        Assert.Single(rows, x => x.Action == "area.create");
        Assert.Equal(2, rows.Count(x => x.Action == "table.create"));
        Assert.Single(rows, x => x.Action == "qr.invite");
        var exchange = Assert.Single(rows, x => x.Action == "qr.exchange");
        Assert.Equal("System", exchange.SourceModule);
        Assert.Null(exchange.ActorId);
        Assert.Equal(day, exchange.BusinessDayId);
        var serialized = System.Text.Json.JsonSerializer.Serialize(rows);
        Assert.DoesNotContain(invite.JoinCode, serialized);
        Assert.DoesNotContain(token.Token, serialized);
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_the_business_write_and_audit_written_before_it()
    {
        await using var scope = services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IBusinessAudit>();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var order = DonHang.Create("ROLLBACK", OrderTypes.Takeaway, day, null, null, null, actor, DateTime.UtcNow).Value!;
            db.Add(order);
            await db.SaveChangesAsync();
            await writer.AppendAsync(new("Sales", "sales.order.created", "DonHang", order.Id.ToString(), actor, day), default);
            await Assert.ThrowsAsync<ArgumentException>(() => writer.AppendAsync(
                new("Sales", "", "DonHang", order.Id.ToString(), actor, day), default));
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Orders.ToArrayAsync());
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Entries.AsNoTracking().ToArrayAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AppendAsync(new("Sales", "sales.order.created", "DonHang", "1", actor, day), default));
    }

    [SqlServerFact]
    public async Task Delayed_confirmation_for_cancelled_closed_order_is_acknowledged_without_ticket_or_audit()
    {
        await using var sales = scopeSales();
        var order = DonHang.Create("DELAYED", OrderTypes.Takeaway, day, null, null, null, actor, DateTime.UtcNow).Value!;
        var batch = order.AddBatch(BatchSource.Staff, null, actor, Guid.NewGuid(),
            [new NewPortion(Guid.NewGuid(), Guid.NewGuid(), "Soup", "Bowl", 50000, "BEP", null)], DateTime.UtcNow).Value!;
        var portionId = batch.Portions.Single().Id;
        Assert.True(order.CancelPortion(batch.SoDot, portionId).Succeeded);
        sales.Add(order);
        await sales.SaveChangesAsync();
        var persistedPortionId = await sales.Portions.Where(x => x.TrangThai == "DA_HUY").Select(x => x.Id).SingleAsync();
        await sales.Database.ExecuteSqlRawAsync("UPDATE salesday.NgayBan SET Status='CHO_CHOT'");

        await using var fulfillment = scopeFulfillment();
        var message = new StoredIntegrationEvent(Guid.NewGuid(), "batch:delayed", 1, "sales.batch_confirmed", 1,
            IntegrationEventJson.SerializePayload(new BatchConfirmed(order.Id, batch.Id, batch.SoDot,
                [new ConfirmedPortion(persistedPortionId, "BEP")])), DateTimeOffset.UtcNow, 1);
        var handler = new TestBatchConfirmedHandler(fulfillment, new FulfillmentIncidentContext(), TimeProvider.System);
        await handler.HandleAsync(message, default);

        Assert.Empty(await fulfillment.Tickets.ToArrayAsync());
        await using var audit = scopeAudit();
        Assert.Empty(await audit.Entries.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, await fulfillment.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Inbox").SingleAsync());
    }

    private SalesDbContext scopeSales() => new(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(
        services!.GetRequiredService<IConfiguration>().GetConnectionString("Restaurant"),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);

    private FulfillmentDbContext scopeFulfillment() => new(new DbContextOptionsBuilder<FulfillmentDbContext>().UseSqlServer(
        services!.GetRequiredService<IConfiguration>().GetConnectionString("Restaurant"),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment")).Options);

    private AuditDbContext scopeAudit() => new(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(
        services!.GetRequiredService<IConfiguration>().GetConnectionString("Restaurant"),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")).Options);

    private sealed class TestBatchConfirmedHandler(FulfillmentDbContext db, IFulfillmentIncidentContext context,
        TimeProvider clock)
    {
        public async Task HandleAsync(StoredIntegrationEvent message, CancellationToken ct)
        {
            var envelope = IntegrationEventJson.Read<BatchConfirmed>(message);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (!await InboxStore.TryRecordAsync(db.Database.GetDbConnection(), tx.GetDbTransaction(),
                    FulfillmentDbContext.Schema, "fulfillment.tickets", message.EventId, clock.GetUtcNow(), ct))
            {
                await tx.RollbackAsync(ct);
                return;
            }
            foreach (var portion in envelope.Payload.Portions)
            {
                var error = await context.RequireOpenAsync(tx.GetDbTransaction(), portion.PortionId, ct);
                if (error == "sales.portion.cancelled") continue;
                if (error is not null) throw new InvalidOperationException(error);
                var result = PhieuCheBien.CreateInitial(portion.PortionId, portion.Station, envelope.OccurredAt.UtcDateTime);
                db.Tickets.Add(result.Value!);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    private sealed class MenuStub : IMenuPricing
    {
        public Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct) => Task.FromResult(new MenuPricingResult(
            request.Lines.Select(x => new PricedMenuLine(x.LineNo, x.MenuItemId, "Soup", x.VariantId, "Bowl", 50000, "BEP", [], 50000)).ToArray(), []));
    }
}
