using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Infrastructure.Messaging;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Shared.Contracts;
using Restaurant.Api.Configuration;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SalesBillingAcceptanceSqlServerTests
{
    [SqlServerFact]
    public async Task Concurrent_confirmation_and_replayed_outbox_create_one_charge_and_one_initial_ticket()
    {
        var database = "RestaurantDemo_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")!) { InitialCatalog = database }.ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Restaurant"] = connection,
            ["DemoSeed:Password"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!"
        }).Build();
        try
        {
            await DemoSeedCommand.ExecuteAsync("--seed-demo", configuration, "Testing");
            await using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connection })));
            var services = factory.Services;
            await using var scope = services.CreateAsyncScope();
            var p = scope.ServiceProvider;
            var staff = (await p.GetRequiredService<IdentityDbContext>().Users.SingleAsync(u => u.UserName == "demo.staff")).Id;
            var day = (await p.GetRequiredService<IBusinessDays>().CurrentAsync(default))!;
            var item = (await p.GetRequiredService<ICatalogMenu>().GetMenuAsync(default)).Items.Single(i => i.Code == "HAI_DEMO_TEA");
            var orders = p.GetRequiredService<IOrderCommands>();
            var created = await orders.CreateOrderAsync(new("MANG_DI", null, day.Id, "QA acceptance", staff), default);
            Assert.Null(created.Error);
            var order = created.Value!;
            var added = await orders.AddBatchAsync(new(order.Id, "NHAN_VIEN", null, staff, Guid.NewGuid(),
                [new(item.Id, item.Variants.Single().Id, [], null)]), default);
            Assert.Null(added.Error);
            var batch = added.Value!;
            async Task<ServiceResult<BatchView>> Confirm()
            {
                await using var request = services.CreateAsyncScope();
                return await request.ServiceProvider.GetRequiredService<IOrderCommands>().ConfirmBatchAsync(new(order.Id, batch.Number, staff), default);
            }
            var salesDb = p.GetRequiredService<SalesDbContext>();
            await salesDb.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER sales.FailConfirmationOutbox ON sales.Outbox AFTER INSERT AS
                BEGIN THROW 51087, 'Injected confirmation Outbox failure', 1; END
                """);
            await Assert.ThrowsAsync<DbUpdateException>(() => Confirm());
            Assert.Null(await p.GetRequiredService<IBillingLedger>().ForOrderAsync(order.Id, default));
            Assert.Equal(0, await salesDb.Set<OutboxEntry>().CountAsync());
            await salesDb.Database.ExecuteSqlRawAsync("DROP TRIGGER sales.FailConfirmationOutbox");
            var confirmations = await Task.WhenAll(Confirm(), Confirm());
            Assert.Single(confirmations, result => result.Succeeded);
            Assert.Equal("sales.batch.not_pending", Assert.Single(confirmations, result => !result.Succeeded).Error);
            var ledger = p.GetRequiredService<IBillingLedger>();
            var invoiceId = await ledger.ForOrderAsync(order.Id, default);
            Assert.NotNull(invoiceId);
            var invoice = (await ledger.FindAsync(invoiceId.Value, default))!;
            Assert.Single(invoice.Lines);
            Assert.Equal(20000, invoice.Due);
            var persisted = Assert.Single(await p.GetRequiredService<SalesDbContext>().Set<OutboxEntry>().AsNoTracking()
                .Where(e => e.Type == "sales.batch_confirmed").ToArrayAsync());
            var message = new StoredIntegrationEvent(persisted.Id, persisted.AggregateId, persisted.AggregateVersion,
                persisted.Type, persisted.SchemaVersion, persisted.Payload, new DateTimeOffset(DateTime.SpecifyKind(persisted.OccurredAt, DateTimeKind.Utc)), 1);
            async Task Deliver()
            {
                await using var request = services.CreateAsyncScope();
                var handler = Assert.Single(request.ServiceProvider.GetServices<IIntegrationEventHandler>(), h => h.EventType == message.Type);
                await handler.HandleAsync(message, default);
            }
            await Task.WhenAll(Deliver(), Deliver());
            await Deliver();
            var fulfillment = p.GetRequiredService<FulfillmentDbContext>();
            var ticket = Assert.Single(await fulfillment.Tickets.AsNoTracking().ToArrayAsync());
            Assert.Equal(batch.Portions.Single().Id, ticket.ChiTietDonId);
            Assert.Equal(1, ticket.Lan);
            Assert.Equal(1, await fulfillment.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM fulfillment.Inbox").SingleAsync());
        }
        finally
        {
            await DemoSeedCommand.ExecuteAsync("--reset-demo", configuration, "Testing");
        }
    }

}
