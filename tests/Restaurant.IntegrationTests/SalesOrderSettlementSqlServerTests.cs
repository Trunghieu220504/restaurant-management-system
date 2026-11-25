using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>IOrderSettlement (Sales side of the HIE-3.1 / Billing contract) on a real SQL Server.</summary>
public sealed class SalesOrderSettlementSqlServerTests : IAsyncLifetime
{
    private static readonly Guid Day = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Staff = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private readonly string databaseName = "RestaurantSalesSettlementTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;
    private long nextSession = 100;

    private SalesDbContext CreateContext() => new(new DbContextOptionsBuilder<SalesDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName ||
            !databaseName.StartsWith("RestaurantSalesSettlementTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        SqlTestDatabase.ClearPool(connectionString);
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private static NewPortion Portion(string dish = "Pho") => new(CatalogId.Of(1), CatalogId.Of(2), dish, "Regular", 60000, Stations.Kitchen, null);

    /// <summary>Creates an open dine-in order with one batch per entry (the number is the portion count of that batch).</summary>
    private async Task<long> CreateOrderAsync(string code, params int[] portionsPerBatch)
    {
        var order = DonHang.Create(code, OrderTypes.DineIn, Day, nextSession++, "Table", null, Staff, Now).Value!;
        foreach (var count in portionsPerBatch)
            Assert.True(order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(),
                Enumerable.Range(0, count).Select(i => Portion("Dish" + i)).ToList(), Now).Succeeded);
        await using var db = CreateContext();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    /// <summary>Loads the order, applies a domain change, saves. Fails the test if the domain refuses.</summary>
    private async Task ChangeAsync(long orderId, Func<DonHang, SalesResult> change)
    {
        await using var db = CreateContext();
        var order = await db.Orders.Include(o => o.Dots).ThenInclude(d => d.Portions).SingleAsync(o => o.Id == orderId);
        var result = change(order);
        Assert.True(result.Succeeded, result.Error);
        await db.SaveChangesAsync();
    }

    private async Task<OrderProcessingState?> StateAsync(long orderId)
    {
        await using var db = CreateContext();
        return await new SalesOrderSettlement(db).GetStateAsync(orderId, default);
    }

    private async Task<bool> BelongsAsync(long orderId, long portionId)
    {
        await using var db = CreateContext();
        return await new SalesOrderSettlement(db).PortionBelongsToOrderAsync(orderId, portionId, default);
    }

    private async Task<long[]> PortionIdsAsync(long orderId)
    {
        await using var db = CreateContext();
        return await db.Portions.AsNoTracking()
            .Where(p => db.Batches.Any(d => d.Id == p.DotGoiMonId && d.DonHangId == orderId))
            .OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
    }

    [SqlServerFact]
    public async Task An_unknown_or_invalid_order_has_no_state()
    {
        await CreateOrderAsync("DH-1", 1);
        foreach (var id in new long[] { 999_999, 0, -1, long.MinValue })
            Assert.Null(await StateAsync(id));
    }

    [SqlServerFact]
    public async Task A_new_order_without_batches_is_open_and_settled()
    {
        var id = await CreateOrderAsync("DH-1");

        var state = (await StateAsync(id))!;

        Assert.Equal((id, Day, OrderStatuses.Open, 0, 0), (state.OrderId, state.BusinessDayId, state.Status, state.PendingBatchCount, state.NonFinalPortionCount));
        Assert.True(state.IsSettled);
    }

    [SqlServerFact]
    public async Task A_pending_batch_counts_as_waiting_with_all_its_portions_not_final()
    {
        var id = await CreateOrderAsync("DH-1", 2, 1);

        var state = (await StateAsync(id))!;

        Assert.Equal(2, state.PendingBatchCount);
        Assert.Equal(3, state.NonFinalPortionCount);
        Assert.False(state.IsSettled);
    }

    [SqlServerFact]
    public async Task A_confirmed_batch_has_no_pending_batch_but_its_portions_stay_non_final_until_served_or_cancelled()
    {
        var id = await CreateOrderAsync("DH-1", 2);
        await ChangeAsync(id, o => o.ConfirmBatch(1, Staff, Now));

        var state = (await StateAsync(id))!;

        Assert.Equal(0, state.PendingBatchCount);
        Assert.Equal(2, state.NonFinalPortionCount);
        Assert.False(state.IsSettled);
    }

    [SqlServerFact]
    public async Task Cancelling_every_portion_of_a_confirmed_batch_settles_the_order()
    {
        var id = await CreateOrderAsync("DH-1", 2);
        await ChangeAsync(id, o => o.ConfirmBatch(1, Staff, Now));
        var portions = await PortionIdsAsync(id);

        await ChangeAsync(id, o => o.CancelPortion(1, portions[0]));
        var half = (await StateAsync(id))!;
        await ChangeAsync(id, o => o.CancelPortion(1, portions[1]));
        var done = (await StateAsync(id))!;

        Assert.Equal(1, half.NonFinalPortionCount);
        Assert.False(half.IsSettled);
        Assert.Equal(0, done.NonFinalPortionCount);
        Assert.True(done.IsSettled);
    }

    [SqlServerFact]
    public async Task A_rejected_or_cancelled_batch_leaves_nothing_open()
    {
        var id = await CreateOrderAsync("DH-1", 2, 3);
        await ChangeAsync(id, o => o.RejectBatch(1, Staff, "Out of stock", Now));
        await ChangeAsync(id, o => o.CancelBatch(2));

        var state = (await StateAsync(id))!;

        Assert.Equal((0, 0), (state.PendingBatchCount, state.NonFinalPortionCount));
        Assert.True(state.IsSettled);
    }

    [SqlServerFact]
    public async Task Cancelling_the_last_portion_of_a_pending_batch_withdraws_the_batch()
    {
        var id = await CreateOrderAsync("DH-1", 1);
        var portion = (await PortionIdsAsync(id)).Single();

        await ChangeAsync(id, o => o.CancelPortion(1, portion));

        var state = (await StateAsync(id))!;
        Assert.Equal((0, 0), (state.PendingBatchCount, state.NonFinalPortionCount));
    }

    [SqlServerFact]
    public async Task Closed_and_cancelled_orders_report_their_final_status()
    {
        var closed = await CreateOrderAsync("DH-1");
        var cancelled = await CreateOrderAsync("DH-2", 1);
        await ChangeAsync(closed, o => o.Close(Now.AddMinutes(30)));
        await ChangeAsync(cancelled, o => o.Cancel(Now.AddMinutes(30)));

        var closedState = (await StateAsync(closed))!;
        var cancelledState = (await StateAsync(cancelled))!;

        Assert.Equal(OrderStatuses.Closed, closedState.Status);
        Assert.Equal(OrderStatuses.Cancelled, cancelledState.Status);
        Assert.True(cancelledState.IsSettled);
    }

    [SqlServerFact]
    public async Task Counts_never_mix_data_of_different_orders()
    {
        var a = await CreateOrderAsync("DH-1", 2);
        var b = await CreateOrderAsync("DH-2", 1, 1, 1);

        var stateA = (await StateAsync(a))!;
        var stateB = (await StateAsync(b))!;

        Assert.Equal((1, 2), (stateA.PendingBatchCount, stateA.NonFinalPortionCount));
        Assert.Equal((3, 3), (stateB.PendingBatchCount, stateB.NonFinalPortionCount));
    }

    [SqlServerFact]
    public async Task A_portion_belongs_only_to_the_order_that_owns_it()
    {
        var a = await CreateOrderAsync("DH-1", 2);
        var b = await CreateOrderAsync("DH-2", 1);
        var portionsA = await PortionIdsAsync(a);
        var portionB = (await PortionIdsAsync(b)).Single();

        foreach (var id in portionsA) Assert.True(await BelongsAsync(a, id));
        Assert.False(await BelongsAsync(b, portionsA[0]));
        Assert.False(await BelongsAsync(a, portionB));
        Assert.True(await BelongsAsync(b, portionB));
    }

    [SqlServerFact]
    public async Task A_cancelled_portion_still_belongs_to_its_order_and_bad_ids_never_match()
    {
        var a = await CreateOrderAsync("DH-1", 1);
        var portion = (await PortionIdsAsync(a)).Single();
        await ChangeAsync(a, o => o.CancelPortion(1, portion));

        Assert.True(await BelongsAsync(a, portion));
        Assert.False(await BelongsAsync(a, 999_999));
        Assert.False(await BelongsAsync(999_999, portion));
        Assert.False(await BelongsAsync(0, portion));
        Assert.False(await BelongsAsync(a, 0));
        Assert.False(await BelongsAsync(-1, -1));
    }

    [Fact]
    public async Task Invalid_ids_are_answered_without_touching_the_database()
    {
        // The connection string points nowhere: any query would throw, so this proves the guard clauses run first.
        await using var db = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=unused;Connect Timeout=1").Options);
        var settlement = new SalesOrderSettlement(db);

        Assert.Null(await settlement.GetStateAsync(0, default));
        Assert.False(await settlement.PortionBelongsToOrderAsync(0, 5, default));
        Assert.False(await settlement.PortionBelongsToOrderAsync(5, 0, default));
    }
}
