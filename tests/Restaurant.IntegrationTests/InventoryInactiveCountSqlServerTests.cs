using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.SalesDay.Application;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class InventoryInactiveCountSqlServerTests : DayLedgerSqlFixture
{
    [SqlServerFact]
    public Task Switching_to_expense_on_purchase_removes_the_empty_count_from_closing() => CheckClosingAsync(archive: false);

    [SqlServerFact]
    public Task Archiving_an_unused_ingredient_removes_the_empty_count_from_closing() => CheckClosingAsync(archive: true);

    private async Task CheckClosingAsync(bool archive)
    {
        await using var scope = services!.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var (ingredient, unit) = await MaterialAsync(provider, "EMPTY");
        var counts = provider.GetRequiredService<IStockCounts>();
        var empty = Assert.Single(await counts.ListAsync(day, default));

        var stock = provider.GetRequiredService<IInventory>();
        var changed = await stock.IngredientAsync(ingredient,
            new(day, "EMPTY", "Empty", unit, archive ? "THEO_DOI_TON" : "TINH_KHI_MUA", archive), default);
        Assert.Null(changed.Error);

        Assert.Empty(await counts.ListAsync(day, default));
        Assert.Equal("inventory.count.stale", (await counts.SaveAsync(day, ingredient, 0, empty.Version, false, actor, default)).Error);
        await BeginAsync(provider);
        var result = await provider.GetRequiredService<IDaySettlement>().FinalizeAsync(day,
            new(500, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(result.Error);
    }
}
