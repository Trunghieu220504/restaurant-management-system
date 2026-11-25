using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>
/// HIE-3.0 on a real SQL Server: the Sales Outbox written atomically with a business change, claimed with a lease,
/// retried with backoff, parked and requeued, and the consumer Inbox duplicate guard.
/// </summary>
public sealed class OutboxInboxSqlServerTests : IAsyncLifetime
{
    private const string Schema = "sales";
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private sealed record TestPayload(string Name, int Quantity);

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly string databaseName = "RestaurantOutboxTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;
    private ServiceProvider? provider;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        // Built through the real module registration so the DI wiring of IOutboxWriter is covered too.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connectionString })
            .Build());
        services.AddSalesModule();
        services.AddSingleton<IBusinessAudit>(TestBusinessAudit.Instance);
        provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
        // Sales does not consume events yet, so its migration has no Inbox; the guard is tested on a table with the agreed shape.
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE [sales].[Inbox] (
                [Consumer] varchar(100) NOT NULL, [EventId] uniqueidentifier NOT NULL, [ProcessedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_Inbox] PRIMARY KEY ([Consumer], [EventId]));
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null || provider is null) return;
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName ||
            !databaseName.StartsWith("RestaurantOutboxTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SalesDbContext>().Database.EnsureDeletedAsync();
        }
        await provider.DisposeAsync();
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static IntegrationEvent<TestPayload> NewEvent(string type = "sales.batch_confirmed", int quantity = 1,
        DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), at ?? T0, type, 1, new TestPayload("pho", quantity));

    /// <summary>Stages events and commits them the way a use case does: business change and event in one SaveChanges.</summary>
    private async Task StageAsync(params (IntegrationEvent<TestPayload> Event, string Aggregate, int Version)[] events)
    {
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        foreach (var item in events) await writer.EnqueueAsync(item.Event, item.Aggregate, item.Version, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [SqlServerFact]
    public async Task Event_exists_only_if_the_business_transaction_commits()
    {
        await using (var scope = provider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<IOutboxWriter>()
                .EnqueueAsync(NewEvent(), "batch-1", 1, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox]"));

        var committed = NewEvent(quantity: 3);
        await StageAsync((committed, "batch-1", 1));
        await using var connection = await OpenAsync();
        var claimed = Assert.Single(await OutboxStore.ClaimDueAsync(connection, Schema, T0, 10, TimeSpan.FromMinutes(1)));
        Assert.Equal(committed.EventId, claimed.EventId);
        Assert.Equal("batch-1", claimed.AggregateId);
        Assert.Equal(1, claimed.AggregateVersion);
        Assert.Equal("sales.batch_confirmed", claimed.Type);
        Assert.Equal(1, claimed.SchemaVersion);
        Assert.Equal(T0, claimed.OccurredAt);
        Assert.Equal(1, claimed.Attempts);
        var envelope = IntegrationEventJson.Read<TestPayload>(new StoredIntegrationEvent(claimed.EventId, claimed.AggregateId,
            claimed.AggregateVersion, claimed.Type, claimed.SchemaVersion, claimed.Payload, claimed.OccurredAt, claimed.Attempts));
        Assert.Equal(new TestPayload("pho", 3), envelope.Payload);
    }

    [SqlServerFact]
    public async Task The_same_fact_cannot_be_staged_twice_but_a_new_version_can()
    {
        await StageAsync((NewEvent(), "batch-7", 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => StageAsync((NewEvent(), "batch-7", 1)));
        await StageAsync((NewEvent(), "batch-7", 2), (NewEvent("sales.batch_cancelled"), "batch-7", 1));
        Assert.Equal(3, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox]"));
    }

    [SqlServerFact]
    public async Task Writer_rejects_invalid_aggregates_before_touching_the_database()
    {
        await using var scope = provider!.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        await Assert.ThrowsAsync<ArgumentException>(() => writer.EnqueueAsync(NewEvent(), "", 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.EnqueueAsync(NewEvent(), "bad id!", 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.EnqueueAsync(NewEvent(), new string('a', 65), 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => writer.EnqueueAsync(NewEvent(), "batch-1", 0, CancellationToken.None));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SalesDbContext>().ChangeTracker.Entries());
    }

    [SqlServerFact]
    public async Task Claim_leases_messages_so_two_dispatchers_never_share_one_until_the_lease_expires()
    {
        await StageAsync((NewEvent(), "a", 1), (NewEvent(), "b", 1), (NewEvent(), "c", 1));
        await using var first = await OpenAsync();
        await using var second = await OpenAsync();
        var lease = TimeSpan.FromMinutes(2);

        var one = await OutboxStore.ClaimDueAsync(first, Schema, T0, 2, lease);
        var two = await OutboxStore.ClaimDueAsync(second, Schema, T0, 10, lease);
        Assert.Equal(2, one.Count);
        Assert.Single(two);
        Assert.Empty(one.Select(x => x.EventId).Intersect(two.Select(x => x.EventId)));
        Assert.Empty(await OutboxStore.ClaimDueAsync(second, Schema, T0.AddMinutes(1), 10, lease));

        var again = await OutboxStore.ClaimDueAsync(second, Schema, T0.AddMinutes(3), 10, lease);
        Assert.Equal(3, again.Count);
        Assert.All(again, x => Assert.Equal(2, x.Attempts));
    }

    [SqlServerFact]
    public async Task A_message_still_inside_an_open_transaction_is_not_visible_to_the_dispatcher()
    {
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await scope.ServiceProvider.GetRequiredService<IOutboxWriter>()
            .EnqueueAsync(NewEvent(), "batch-9", 1, CancellationToken.None);
        await db.SaveChangesAsync();

        // READPAST skips the uncommitted row instead of waiting on it or reading it.
        await using var connection = await OpenAsync();
        Assert.Empty(await OutboxStore.ClaimDueAsync(connection, Schema, T0, 10, TimeSpan.FromMinutes(1)));
        await transaction.CommitAsync();
        Assert.Single(await OutboxStore.ClaimDueAsync(connection, Schema, T0, 10, TimeSpan.FromMinutes(1)));
    }

    [SqlServerFact]
    public async Task Dispatcher_delivers_in_occurrence_order_and_marks_processed()
    {
        var early = NewEvent(at: T0);
        var late = NewEvent(at: T0.AddSeconds(5));
        await StageAsync((late, "z", 1), (early, "a", 1));
        var clock = new FakeClock(T0.AddMinutes(1));
        var delivered = new List<Guid>();
        var dispatcher = new OutboxDispatcher(async _ => await OpenAsync(), Schema,
            (message, _) => { delivered.Add(message.EventId); return Task.CompletedTask; },
            new OutboxDispatcherOptions(), clock);

        Assert.Equal(2, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal([early.EventId, late.EventId], delivered);
        Assert.Equal(2, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox] WHERE [ProcessedAt] IS NOT NULL AND [LeaseUntil] IS NULL"));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(2, delivered.Count);
    }

    [SqlServerFact]
    public async Task Failed_delivery_backs_off_then_parks_and_an_operator_can_requeue_it()
    {
        await StageAsync((NewEvent(), "batch-1", 1));
        var clock = new FakeClock(T0);
        var failing = true;
        var attempts = 0;
        var options = new OutboxDispatcherOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromSeconds(10), MaxDelay = TimeSpan.FromMinutes(1) };
        var dispatcher = new OutboxDispatcher(async _ => await OpenAsync(), Schema, (_, _) =>
        {
            attempts++;
            return failing ? throw new InvalidOperationException("consumer down") : Task.CompletedTask;
        }, options, clock);

        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, attempts);
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None)); // backing off: not due yet
        clock.Now = T0.AddSeconds(10);
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(2, attempts);
        clock.Now = T0.AddSeconds(10 + 20); // the second failure doubled the delay
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(3, attempts);

        // The third attempt reached MaxAttempts: parked with its error, never picked up again.
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox] WHERE [NextAttemptAt] IS NULL AND [ProcessedAt] IS NULL AND [LastError] = 'InvalidOperationException: consumer down'"));
        clock.Now = T0.AddDays(1);
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(3, attempts);

        failing = false;
        await using (var connection = await OpenAsync())
        {
            Assert.Equal(1, await OutboxStore.RequeueParkedAsync(connection, Schema, clock.Now));
        }
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox] WHERE [ProcessedAt] IS NOT NULL AND [LastError] IS NULL AND [Attempts] = 1"));
    }

    [SqlServerFact]
    public async Task Shutdown_during_delivery_leaves_the_lease_so_the_message_is_delivered_again_later()
    {
        await StageAsync((NewEvent(), "batch-1", 1));
        var clock = new FakeClock(T0);
        using var cancellation = new CancellationTokenSource();
        var dispatcher = new OutboxDispatcher(async _ => await OpenAsync(), Schema, (_, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }, new OutboxDispatcherOptions { LeaseDuration = TimeSpan.FromMinutes(2) }, clock);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.RunOnceAsync(cancellation.Token));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox] WHERE [ProcessedAt] IS NULL AND [LastError] IS NULL AND [LeaseUntil] IS NOT NULL"));

        clock.Now = T0.AddMinutes(3);
        var redelivered = 0;
        var next = new OutboxDispatcher(async _ => await OpenAsync(), Schema,
            (_, _) => { redelivered++; return Task.CompletedTask; }, new OutboxDispatcherOptions(), clock);
        Assert.Equal(1, await next.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, redelivered);
    }

    [SqlServerFact]
    public async Task A_delivery_that_outlives_its_lease_is_reported_when_another_dispatcher_completed_the_message()
    {
        await StageAsync((NewEvent(), "batch-1", 1));
        var clock = new FakeClock(T0);
        var reported = new List<Guid>();
        var dispatcher = new OutboxDispatcher(async _ => await OpenAsync(), Schema, async (message, ct) =>
        {
            // The lease runs out and a second dispatcher claims and completes the same message while this one is still busy.
            clock.Now = T0.AddMinutes(3);
            await using var other = await OpenAsync();
            var again = Assert.Single(await OutboxStore.ClaimDueAsync(other, Schema, clock.Now, 10, TimeSpan.FromMinutes(1), ct));
            Assert.Equal(message.EventId, again.EventId);
            Assert.True(await OutboxStore.MarkProcessedAsync(other, Schema, again.EventId, clock.Now, ct));
        }, new OutboxDispatcherOptions { LeaseDuration = TimeSpan.FromMinutes(2) }, clock, message => reported.Add(message.EventId));

        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));

        Assert.Single(reported);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Outbox] WHERE [ProcessedAt] IS NOT NULL"));
    }

    [SqlServerFact]
    public async Task Complete_and_fail_ignore_messages_that_are_already_done()
    {
        await StageAsync((NewEvent(), "batch-1", 1));
        await using var connection = await OpenAsync();
        var message = Assert.Single(await OutboxStore.ClaimDueAsync(connection, Schema, T0, 10, TimeSpan.FromMinutes(1)));
        Assert.True(await OutboxStore.MarkProcessedAsync(connection, Schema, message.EventId, T0));
        Assert.False(await OutboxStore.MarkProcessedAsync(connection, Schema, message.EventId, T0));
        Assert.False(await OutboxStore.MarkFailedAsync(connection, Schema, message.EventId, "late failure", T0.AddMinutes(1)));
        Assert.False(await OutboxStore.MarkProcessedAsync(connection, Schema, Guid.NewGuid(), T0));
    }

    [SqlServerFact]
    public async Task Inbox_skips_a_repeated_delivery_and_forgets_it_when_the_work_rolls_back()
    {
        var eventId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        await using (var rolledBack = await connection.BeginTransactionAsync())
        {
            Assert.True(await InboxStore.TryRecordAsync(connection, rolledBack, Schema, "fulfillment.tickets", eventId, T0));
            await rolledBack.RollbackAsync();
        }
        await using (var committed = await connection.BeginTransactionAsync())
        {
            Assert.True(await InboxStore.TryRecordAsync(connection, committed, Schema, "fulfillment.tickets", eventId, T0));
            Assert.False(await InboxStore.TryRecordAsync(connection, committed, Schema, "fulfillment.tickets", eventId, T0));
            await committed.CommitAsync();
        }
        await using (var repeat = await connection.BeginTransactionAsync())
        {
            Assert.False(await InboxStore.TryRecordAsync(connection, repeat, Schema, "fulfillment.tickets", eventId, T0));
            // Another consumer keeps its own record of the same event.
            Assert.True(await InboxStore.TryRecordAsync(connection, repeat, Schema, "audit.log", eventId, T0));
            await repeat.RollbackAsync();
        }
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM [sales].[Inbox]"));
    }

    [SqlServerFact]
    public async Task Concurrent_deliveries_of_one_event_run_the_work_once()
    {
        var eventId = Guid.NewGuid();
        await using var a = await OpenAsync();
        await using var b = await OpenAsync();
        await using var ta = await a.BeginTransactionAsync();
        await using var tb = await b.BeginTransactionAsync();
        Assert.True(await InboxStore.TryRecordAsync(a, ta, Schema, "fulfillment.tickets", eventId, T0));

        // The second delivery waits on the first one's key lock and, once that commits, sees the marker.
        var second = InboxStore.TryRecordAsync(b, tb, Schema, "fulfillment.tickets", eventId, T0);
        await Task.Delay(300);
        Assert.False(second.IsCompleted);
        await ta.CommitAsync();
        Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(10)));
        await tb.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Helpers_reject_unsafe_or_invalid_arguments()
    {
        await using var connection = await OpenAsync();
        await using var other = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => OutboxStore.ClaimDueAsync(connection, "sales]; DROP TABLE x;--", T0, 1, TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => InboxStore.TryRecordAsync(connection, transaction, "", "c", Guid.NewGuid(), T0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OutboxStore.ClaimDueAsync(connection, Schema, T0, 0, TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OutboxStore.ClaimDueAsync(connection, Schema, T0, 501, TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OutboxStore.ClaimDueAsync(connection, Schema, T0, 1, TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OutboxStore.ClaimDueAsync(connection, Schema, T0, 1, TimeSpan.FromHours(2)));
        await Assert.ThrowsAsync<ArgumentException>(() => InboxStore.TryRecordAsync(other, transaction, Schema, "c", Guid.NewGuid(), T0));
        await Assert.ThrowsAsync<ArgumentException>(() => InboxStore.TryRecordAsync(connection, transaction, Schema, "c", Guid.Empty, T0));
        await Assert.ThrowsAsync<ArgumentException>(() => InboxStore.TryRecordAsync(connection, transaction, Schema, new string('c', 101), Guid.NewGuid(), T0));
        await transaction.RollbackAsync();
        await using var closed = new SqlConnection(connectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() => OutboxStore.ClaimDueAsync(closed, Schema, T0, 1, TimeSpan.FromSeconds(1)));
    }
}
