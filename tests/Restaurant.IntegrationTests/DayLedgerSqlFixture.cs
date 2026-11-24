using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public abstract class DayLedgerSqlFixture : IAsyncLifetime
{
    protected readonly Guid day = Guid.NewGuid();
    protected readonly Guid actor = Guid.NewGuid();
    private readonly string database = "RestaurantDayLedger_" + Guid.NewGuid().ToString("N");
    protected ServiceProvider? services;

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
        await provider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<SalesDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<SeatingDbContext>().Database.MigrateAsync();
        days.Add(new BusinessDay
        {
            Id = day,
            BusinessDate = new(2026, 12, 10),
            OpeningCash = 500,
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

    protected async Task<(long Ingredient, long Unit)> MaterialAsync(IServiceProvider p, string code, string dimension = "DEM", bool expense = false)
    {
        var inventory = p.GetRequiredService<IInventory>();
        var unit = (await inventory.UnitAsync(new(code, code, dimension), default)).Id!.Value;
        var ingredient = (await inventory.IngredientAsync(null, new(day, code, code, unit,
            expense ? "TINH_KHI_MUA" : "THEO_DOI_TON", false), default)).Id!.Value;
        return (ingredient, unit);
    }

    protected async Task PurchaseAsync(IServiceProvider p, long item, long unit, decimal quantity, decimal price, bool drawer = false)
    {
        var inventory = p.GetRequiredService<IInventory>();
        var draft = await inventory.DraftAsync(new(day, null, drawer, null, [new(item, unit, quantity, price)]), actor, Guid.NewGuid(), default);
        Assert.Null(draft.Error);
        Assert.Null((await inventory.TransitionAsync(draft.Id!.Value, day, true, default)).Error);
    }

    protected async Task ApproveAsync(IServiceProvider p, long id, decimal quantity)
    {
        var counts = p.GetRequiredService<IStockCounts>();
        async Task<byte[]?> Version() => (await counts.ListAsync(day, default)).Single(x => x.IngredientId == id).Version;
        var old = await Version();
        Assert.Null((await counts.SaveAsync(day, id, quantity, old, false, actor, default)).Error);
        Assert.Equal("inventory.count.stale", (await counts.SaveAsync(day, id, null, old, true, actor, default)).Error);
        Assert.Null((await counts.SaveAsync(day, id, null, await Version(), true, actor, default)).Error);
    }

    protected async Task BeginAsync(IServiceProvider p) =>
        Assert.Null((await p.GetRequiredService<IDayClosing>().ChangeAsync(day, false, actor, Guid.NewGuid(), default)).Error);
}
