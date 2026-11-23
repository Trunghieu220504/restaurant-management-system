using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Inventory.Infrastructure;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;

namespace Restaurant.Api.Modules;

internal sealed class OpeningInventory : IOpeningInventory
{
    public async Task CarryAsync(DbTransaction transaction, Guid newDay, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(transaction.Connection!).Options;
        await using var inventory = new InventoryDbContext(options);
        await inventory.Database.UseTransactionAsync(transaction, ct);
        await StockCounts.InitializeAsync(inventory, newDay, ct);
    }
}
