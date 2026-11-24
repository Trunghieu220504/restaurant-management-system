using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Modules;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Xunit;
using Stations = Restaurant.Modules.Fulfillment.Domain.Stations;

namespace Restaurant.IntegrationTests;

public sealed class FulfillmentIncidentSqlServerTests : IAsyncLifetime
{
    private readonly string database = "RestaurantIncidentTest_" + Guid.NewGuid().ToString("N");
    private readonly Guid actor = Guid.NewGuid();
    private readonly Guid day = Guid.NewGuid();
    private string? connection;
    private long ticketId;
    private long invoiceId;
    private FulfillmentDbContext Db() => new(new DbContextOptionsBuilder<FulfillmentDbContext>().UseSqlServer(connection,
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment")).Options);
    private BillingDbContext Billing() => new(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(connection,
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "billing")).Options);
    private FulfillmentIncidents Service(FulfillmentDbContext db) => new(db, new FulfillmentIncidentContext(), TimeProvider.System);

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = database }.ConnectionString;
        await using var days = new SalesDayDbContext(new DbContextOptionsBuilder<SalesDayDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "salesday")).Options);
        await days.Database.MigrateAsync();
        days.Days.Add(new BusinessDay
        {
            Id = day,
            BusinessDate = new(2026, 11, 25),
            OpenedAt = DateTimeOffset.UtcNow,
            OpenedBy = actor,
            RequestKey = Guid.NewGuid()
        });
        await days.SaveChangesAsync();
        await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);
        await sales.Database.MigrateAsync();
        var order = DonHang.Create("INCIDENT", OrderTypes.Takeaway, day, null, null, null, actor, DateTime.UtcNow).Value!;
        var batch = order.AddBatch(BatchSource.Staff, null, actor, Guid.NewGuid(),
            [new NewPortion(Guid.NewGuid(), Guid.NewGuid(), "Soup", "Bowl", 50000, Stations.Kitchen, null)], DateTime.UtcNow).Value!;
        Assert.True(order.ConfirmBatch(batch.SoDot, actor, DateTime.UtcNow).Succeeded);
        sales.Add(order);
        await sales.SaveChangesAsync();
        var portion = Assert.Single(batch.Portions).Id;
        await using var billing = Billing();
        await billing.Database.MigrateAsync();
        var invoice = new HoaDon { DonHangId = order.Id, MaHoaDon = "INCIDENT", NgayBanLapId = day, TaoLuc = DateTime.UtcNow };
        billing.Add(invoice);
        await billing.SaveChangesAsync();
        invoiceId = invoice.Id;
        billing.Add(new DongHoaDon { HoaDonId = invoiceId, ChiTietDonId = portion, TenMonChot = "Soup", ThanhTienGoc = 50000 });
        await billing.SaveChangesAsync();
        await using var db = Db();
        await db.Database.MigrateAsync();
        var ticket = PhieuCheBien.CreateInitial(portion, Stations.Kitchen, DateTime.UtcNow).Value!;
        ticket.Start(actor, DateTime.UtcNow);
        ticket.MarkReady(actor, DateTime.UtcNow);
        db.Add(ticket);
        await db.SaveChangesAsync();
        ticketId = ticket.Id;
    }

    public async Task DisposeAsync()
    {
        if (connection is null) return;
        if (new SqlConnectionStringBuilder(connection).InitialCatalog != database ||
            !database.StartsWith("RestaurantIncidentTest_", StringComparison.Ordinal)) throw new InvalidOperationException();
        SqlConnection.ClearAllPools();
        await using var db = Db();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task<IncidentView> Report()
    {
        await using var db = Db();
        var ticket = await db.Tickets.SingleAsync(x => x.Id == ticketId);
        var result = await Service(db).ReportAsync(ticketId, new(Convert.ToBase64String(ticket.Version), Stations.Kitchen, "Burnt", Guid.NewGuid()), actor, default);
        Assert.True(result.Succeeded, result.Error);
        return result.Value!;
    }

    [SqlServerFact]
    public async Task Pending_incident_blocks_serving_and_concurrent_remake_creates_one_attempt_without_new_revenue()
    {
        var incident = await Report();
        await using (var db = Db())
        {
            var ticket = await db.Tickets.SingleAsync();
            var result = await new FulfillmentService(db, TimeProvider.System).DeliverAsync(ticketId, Stations.Kitchen,
                Convert.ToBase64String(ticket.Version), Guid.NewGuid(), actor, default);
            Assert.Equal(FulfillmentErrors.IncidentPending, result.Error);
        }
        var command = new IncidentResolution(incident.Version, IncidentDecision.Remake, "Make again", Guid.NewGuid());
        async Task<FulfillmentResult<IncidentView>> Decide()
        {
            await using var db = Db();
            return await Service(db).ResolveAsync(incident.Id, command, actor, default);
        }
        var responses = await Task.WhenAll(Decide(), Decide());
        Assert.All(responses, r => Assert.True(r.Succeeded, r.Error));
        Assert.Equal(responses[0].Value!.RemakeTicketId, responses[1].Value!.RemakeTicketId);
        Assert.Equal(IncidentStatus.Approved, responses[0].Value!.Status);
        Assert.Equal(responses[0].Value!.Status, responses[1].Value!.Status);
        Assert.Equal(responses[0].Value!.Decision, responses[1].Value!.Decision);
        Assert.Equal(responses[0].Value!.Version, responses[1].Value!.Version);
        await using var check = Db();
        var tickets = await check.Tickets.OrderBy(x => x.Lan).ToArrayAsync();
        Assert.Equal(2, tickets.Length);
        Assert.Equal(TicketStatus.Failed, tickets[0].TrangThai);
        Assert.Equal(TicketStatus.New, tickets[1].TrangThai);
        Assert.Equal(tickets[0].ChiTietDonId, tickets[1].ChiTietDonId);
        var old = await new FulfillmentService(check, TimeProvider.System).DeliverAsync(ticketId, Stations.Kitchen,
            Convert.ToBase64String(tickets[0].Version), Guid.NewGuid(), actor, default);
        Assert.False(old.Succeeded);
        await using var bill = Billing();
        Assert.Equal(50000, await bill.Set<DongHoaDon>().SumAsync(x => x.ThanhTienGoc));
        Assert.Empty(await bill.Set<DieuChinhNghiaVu>().ToArrayAsync());
        Assert.Equal(2, await check.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Outbox").SingleAsync());
        await check.Database.ExecuteSqlRawAsync("UPDATE salesday.NgayBan SET Status='CHO_CHOT'");
        Assert.True((await Decide()).Succeeded);
    }

    [SqlServerFact]
    public async Task Cancellation_adjustment_commits_once_and_links_incident_without_refunding_payment()
    {
        var incident = await Report();
        await using var db = Db();
        var request = new IncidentResolution(incident.Version, IncidentDecision.CancelWithObligationAdjustment, "Cancel dish", Guid.NewGuid());
        await using (var existing = Billing())
        {
            existing.Add(new DieuChinhNghiaVu
            {
                HoaDonId = invoiceId,
                SoTien = 1,
                NgayBanId = day,
                NguoiDuyetId = actor,
                KhoaYeuCau = request.RequestKey,
                LyDo = "Unrelated manual adjustment",
                TaoLuc = DateTime.UtcNow
            });
            await existing.SaveChangesAsync();
        }
        var result = await Service(db).ResolveAsync(incident.Id, request, actor, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.True((await Service(db).ResolveAsync(incident.Id, request, actor, default)).Succeeded);
        Assert.Equal(TicketStatus.Cancelled, (await db.Tickets.AsNoTracking().SingleAsync()).TrangThai);
        await using var billing = Billing();
        var adjustment = Assert.Single(await billing.Set<DieuChinhNghiaVu>().Where(x => x.SuCoMonId != null).ToArrayAsync());
        Assert.Equal(-50000, adjustment.SoTien);
        Assert.Equal(incident.Id, adjustment.SuCoMonId);
        Assert.Empty(await billing.Set<HoanTien>().ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Billing_rejection_rolls_back_decision_ticket_and_outbox()
    {
        var incident = await Report();
        await using (var billing = Billing())
        {
            billing.Add(new MienThu
            {
                HoaDonId = invoiceId,
                NgayBanId = day,
                NguoiDuyetId = actor,
                TaoLuc = DateTime.UtcNow,
                KhoaYeuCau = Guid.NewGuid(),
                LyDo = "Already waived",
                SoTien = 50000
            });
            await billing.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            var result = await Service(db).ResolveAsync(incident.Id,
                new(incident.Version, IncidentDecision.CancelWithObligationAdjustment, "Cancel", Guid.NewGuid()), actor, default);
            Assert.Equal("billing.amount.exceeds_available", result.Error);
        }
        await using var check = Db();
        Assert.Equal(IncidentStatus.Pending, (await check.Incidents.SingleAsync()).TrangThai);
        Assert.Equal(TicketStatus.Ready, (await check.Tickets.SingleAsync()).TrangThai);
        Assert.Equal(0, await check.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Outbox").SingleAsync());
    }

    [SqlServerFact]
    public async Task Failure_after_billing_write_rolls_back_every_module()
    {
        var incident = await Report();
        await using (var db = Db())
        {
            var service = new FulfillmentIncidents(db, new FailAfterAdjustment(), TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveAsync(incident.Id,
                new(incident.Version, IncidentDecision.CancelWithObligationAdjustment, "Cancel", Guid.NewGuid()), actor, default));
        }
        await using var check = Db();
        Assert.Equal(IncidentStatus.Pending, (await check.Incidents.SingleAsync()).TrangThai);
        Assert.Equal(TicketStatus.Ready, (await check.Tickets.SingleAsync()).TrangThai);
        await using var billing = Billing();
        Assert.Empty(await billing.Set<DieuChinhNghiaVu>().ToArrayAsync());
        Assert.Equal(0, await check.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Outbox").SingleAsync());
    }

    private sealed class FailAfterAdjustment : IFulfillmentIncidentContext
    {
        private readonly FulfillmentIncidentContext inner = new();
        public Task<string?> RequireOpenAsync(System.Data.Common.DbTransaction tx, long portion, CancellationToken ct) => inner.RequireOpenAsync(tx, portion, ct);
        public async Task<string?> ReduceObligationAsync(System.Data.Common.DbTransaction tx, long portion, long incident,
            Guid actor, Guid key, string reason, CancellationToken ct)
        {
            Assert.Null(await inner.ReduceObligationAsync(tx, portion, incident, actor, key, reason, ct));
            throw new InvalidOperationException("Injected failure after Billing SaveChanges.");
        }
    }

    [SqlServerFact]
    public async Task Report_racing_delivery_cannot_commit_both_using_the_same_ticket_version()
    {
        await using var db = Db();
        var version = Convert.ToBase64String((await db.Tickets.SingleAsync()).Version);
        async Task<bool> ReportAttempt()
        {
            await using var reportDb = Db();
            return (await Service(reportDb).ReportAsync(ticketId, new(version, Stations.Kitchen, "Report", Guid.NewGuid()), actor, default)).Succeeded;
        }
        async Task<bool> DeliverAttempt()
        {
            await using var deliverDb = Db();
            return (await new FulfillmentService(deliverDb, TimeProvider.System).DeliverAsync(ticketId, Stations.Kitchen,
                version, Guid.NewGuid(), actor, default)).Succeeded;
        }
        var results = await Task.WhenAll(ReportAttempt(), DeliverAttempt());
        Assert.Single(results, success => success);
    }

    [SqlServerFact]
    public async Task Rejection_unblocks_serving_and_frozen_day_refuses_new_reports()
    {
        var incident = await Report();
        await using var db = Db();
        Assert.True((await Service(db).ResolveAsync(incident.Id, new(incident.Version, null, "No fault", Guid.NewGuid()), actor, default)).Succeeded);
        var ticket = await db.Tickets.AsNoTracking().SingleAsync();
        Assert.True((await new FulfillmentService(db, TimeProvider.System).DeliverAsync(ticketId, Stations.Kitchen,
            Convert.ToBase64String(ticket.Version), Guid.NewGuid(), actor, default)).Succeeded);
        await db.Database.ExecuteSqlRawAsync("UPDATE salesday.NgayBan SET Status='CHO_CHOT'");
        var current = await db.Tickets.AsNoTracking().SingleAsync();
        var denied = await Service(db).ReportAsync(ticketId,
            new(Convert.ToBase64String(current.Version), Stations.Kitchen, "Late report", Guid.NewGuid()), actor, default);
        Assert.Equal("salesday.day.not_open", denied.Error);
    }
}
