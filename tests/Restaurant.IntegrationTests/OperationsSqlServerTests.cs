using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Modules;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.SqlServer;
using Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Shared.Contracts;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Restaurant.Modules.Sales.Contracts;

namespace Restaurant.IntegrationTests;

public sealed class OperationsSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantOperationsTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;
    private static readonly DateTime Now = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();

    private FulfillmentDbContext Db() => new(new DbContextOptionsBuilder<FulfillmentDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment")).Options);

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName ||
            !databaseName.StartsWith("RestaurantOperationsTest_", StringComparison.Ordinal)) throw new InvalidOperationException();
        SqlConnection.ClearAllPools();
        await using var db = Db();
        await db.Database.EnsureDeletedAsync();
    }

    [SqlServerFact]
    public async Task Ticket_transition_and_idempotent_replay_stage_one_event_and_reconnect_sees_current_state()
    {
        await using var db = Db();
        var ticket = PhieuCheBien.CreateInitial(42, Stations.Kitchen, Now).Value!;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var service = new FulfillmentService(db, TimeProvider.System, TestFulfillmentContext.Instance, TestBusinessAudit.Instance);
        var key = Guid.NewGuid();
        var version = Convert.ToBase64String(ticket.Version);
        var started = await service.StartAsync(ticket.Id, Stations.Kitchen, version, key, Actor, default);
        Assert.True(started.Succeeded);
        Assert.True((await service.StartAsync(ticket.Id, Stations.Kitchen, version, key, Actor, default)).Succeeded);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Outbox").SingleAsync());

        await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connectionString).Options);
        var snapshots = new OperationsSnapshotReader(db, sales);
        Assert.Equal(started.Value!.Version, Assert.Single((await snapshots.ReadAsync([Stations.Kitchen], null, default)).Tickets).Version);
        Assert.Empty((await snapshots.ReadAsync([Stations.Bar], null, default)).Tickets);

        var ready = await service.ReadyAsync(ticket.Id, Stations.Kitchen, started.Value.Version, Guid.NewGuid(), Actor, default);
        Assert.True(ready.Succeeded);
        var restored = Assert.Single((await snapshots.ReadAsync([Stations.Kitchen], null, default)).Tickets);
        Assert.Equal(TicketStatus.Ready, restored.Status);
        Assert.Equal(ready.Value!.Version, restored.Version);
        Assert.True((await service.DeliverAsync(ticket.Id, Stations.Kitchen, restored.Version, Guid.NewGuid(), Actor, default)).Succeeded);
        var afterDelivery = await snapshots.ReadAsync([Stations.Kitchen], null, default);
        Assert.Empty(afterDelivery.Tickets);
        var tombstone = Assert.Single(afterDelivery.Tombstones);
        Assert.Equal(ticket.Id, tombstone.Id);
        var terminalVersion = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(Convert.FromBase64String(tombstone.Version));
        var retriedVersion = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(Convert.FromBase64String(started.Value.Version));
        Assert.True(terminalVersion > retriedVersion);
    }

    [SqlServerFact]
    public async Task Confirmed_batch_stages_one_creation_event_per_ticket_and_replay_adds_nothing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connectionString }).Build());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBusinessAudit>(TestBusinessAudit.Instance);
        services.AddSingleton<Restaurant.Modules.Fulfillment.Application.IFulfillmentIncidentContext>(TestFulfillmentContext.Instance);
        services.AddFulfillmentModule();
        await using var provider = services.BuildServiceProvider();
        var payload = new BatchConfirmed(10, 20, 1, [new ConfirmedPortion(42, "BEP"), new ConfirmedPortion(43, "BAR")]);
        var message = new StoredIntegrationEvent(Guid.NewGuid(), "batch:20", 1, "sales.batch_confirmed", 1,
            IntegrationEventJson.SerializePayload(payload), DateTimeOffset.UtcNow, 1);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler>().Single();
            await handler.HandleAsync(message, default);
        }
        await using var db = Db();
        Assert.Equal(2, await db.Tickets.CountAsync());
        var delivered = new List<Restaurant.Modules.Fulfillment.Application.TicketView>();
        var dispatcher = Dispatcher((entry, _) =>
        {
            Assert.Equal(TicketEvents.Changed, entry.Type);
            delivered.Add(JsonSerializer.Deserialize<Restaurant.Modules.Fulfillment.Application.TicketView>(entry.Payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
            return Task.CompletedTask;
        });
        Assert.Equal(2, await dispatcher.RunOnceAsync(default));
        Assert.All(delivered, ticket => Assert.Equal(TicketStatus.New, ticket.Status));
        Assert.Equal(new long[] { 42, 43 }, delivered.Select(x => x.PortionId).Order());
    }

    [SqlServerFact]
    public async Task Rolled_back_ticket_and_outbox_are_not_delivered()
    {
        await using (var db = Db())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var ticket = PhieuCheBien.CreateInitial(43, Stations.Bar, Now).Value!;
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            TicketEvents.Stage(db, ticket, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        var delivered = 0;
        var dispatcher = Dispatcher((_, _) => { delivered++; return Task.CompletedTask; });
        Assert.Equal(0, await dispatcher.RunOnceAsync(default));
        Assert.Equal(0, delivered);
        await using var check = Db();
        Assert.Empty(await check.Tickets.ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Dispatch_waits_for_commit_and_failed_delivery_is_retryable()
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ticket = PhieuCheBien.CreateInitial(44, Stations.Bar, Now).Value!;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        TicketEvents.Stage(db, ticket, DateTimeOffset.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();
        var delivered = 0;
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher((_, _) => { Interlocked.Increment(ref delivered); return Task.CompletedTask; }, opened);
        var beforeCommit = dispatcher.RunOnceAsync(default);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref delivered));
        await tx.CommitAsync();
        await beforeCommit.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.RunOnceAsync(default);
        Assert.Equal(1, delivered);
        Assert.Equal(0, await dispatcher.RunOnceAsync(default));

        var service = new FulfillmentService(db, TimeProvider.System, TestFulfillmentContext.Instance, TestBusinessAudit.Instance);
        Assert.True((await service.StartAsync(ticket.Id, Stations.Bar, Convert.ToBase64String(ticket.Version), Guid.NewGuid(), Actor, default)).Succeeded);
        var failing = Dispatcher((_, _) => throw new IOException("Transport unavailable"));
        Assert.Equal(1, await failing.RunOnceAsync(default));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM fulfillment.Outbox WHERE ProcessedAt IS NULL AND NextAttemptAt IS NOT NULL AND LastError IS NOT NULL").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("UPDATE fulfillment.Outbox SET NextAttemptAt = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE ProcessedAt IS NULL");
        Assert.Equal(1, await dispatcher.RunOnceAsync(default));
        Assert.Equal(2, delivered);
    }

    [SqlServerFact]
    public async Task Handler_delivers_to_socket_once_and_stops_after_station_role_revocation()
    {
        await using (var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options))
            await sales.Database.MigrateAsync();
        var identity = new OperationsHubTests.IdentityStub();
        using var original = new ApiFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIdentityAuthentication>(identity);
            services.AddScoped(_ => new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connectionString).Options));
        }));
        using var http = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(identity.UserId, identity.SessionId,
            ["Kitchen"], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5));
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
        using var socket = await client.ConnectAsync(new Uri("ws://localhost/hubs/operations"), default);
        await OperationsHubTests.Send(socket, "{\"protocol\":\"json\",\"version\":1}");
        Assert.Equal("{}", await OperationsHubTests.Receive(socket));
        await OperationsHubTests.Send(socket, "{\"type\":1,\"invocationId\":\"1\",\"target\":\"JoinStation\",\"arguments\":[\"BEP\"]}");
        Assert.DoesNotContain("error", await OperationsHubTests.Receive(socket));
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler>().Single(x => x.EventType == TicketEvents.Changed);
        var ticket = new Restaurant.Modules.Fulfillment.Application.TicketView(1, 42, "BEP", "SAN_SANG", Now,
            null, null, null, null, null, null, "AAAAAAAAAAE=");
        var message = new StoredIntegrationEvent(Guid.NewGuid(), "ticket:1:0000000000000001", 1,
            TicketEvents.Changed, 1, IntegrationEventJson.SerializePayload(ticket), DateTimeOffset.UtcNow, 1);
        await handler.HandleAsync(message, default);
        using var received = JsonDocument.Parse(await OperationsHubTests.Receive(socket));
        Assert.Equal("TicketChanged", received.RootElement.GetProperty("target").GetString());
        Assert.Equal(message.EventId, received.RootElement.GetProperty("arguments")[0].GetProperty("eventId").GetGuid());
        identity.Roles = ["Bar"];
        await handler.HandleAsync(message with { EventId = Guid.NewGuid() }, default);
        await OperationsHubTests.Send(socket, "{\"type\":1,\"invocationId\":\"2\",\"target\":\"JoinStation\",\"arguments\":[\"BAR\"]}");
        using var completion = JsonDocument.Parse(await OperationsHubTests.Receive(socket));
        Assert.Equal(3, completion.RootElement.GetProperty("type").GetInt32());
        Assert.Equal("2", completion.RootElement.GetProperty("invocationId").GetString());
        socket.Abort();
    }

    private OutboxDispatcher Dispatcher(Func<OutboxMessage, CancellationToken, Task> deliver, TaskCompletionSource? opened = null) =>
        new(async ct =>
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            opened?.TrySetResult();
            return connection;
        }, "fulfillment", deliver, new OutboxDispatcherOptions(), TimeProvider.System);
}
