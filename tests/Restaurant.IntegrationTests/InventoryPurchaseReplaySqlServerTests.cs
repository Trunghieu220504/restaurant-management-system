using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.SalesDay.Application;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class InventoryPurchaseReplaySqlServerTests : DayLedgerSqlFixture
{
    [SqlServerFact]
    public async Task Draft_retry_returns_the_original_receipt_after_its_day_closes()
    {
        await using var scope = services!.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var stock = provider.GetRequiredService<IInventory>();
        var (ingredient, unit) = await MaterialAsync(provider, "BOX", expense: true);
        var input = new PurchaseInput(day, null, true, null, [new(ingredient, unit, 2, 5)]);
        var key = Guid.NewGuid();
        var draft = await stock.DraftAsync(input, actor, key, default);
        Assert.Null(draft.Error);
        Assert.Null((await stock.TransitionAsync(draft.Id!.Value, day, true, default)).Error);
        await BeginAsync(provider);
        var settled = await provider.GetRequiredService<IDaySettlement>().FinalizeAsync(day,
            new(490, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(settled.Error);

        Assert.Equal(draft, await stock.DraftAsync(input, actor, key, default));
        Assert.Equal("inventory.idempotency.conflict", (await stock.DraftAsync(input with { Note = "Changed" }, actor, key, default)).Error);
        Assert.Equal("salesday.day.not_open", (await stock.DraftAsync(input, actor, Guid.NewGuid(), default)).Error);
        Assert.Single(await stock.PurchasesAsync(day, default));
    }
}
