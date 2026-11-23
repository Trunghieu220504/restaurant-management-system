using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Fulfillment.Infrastructure;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

internal sealed class SettledPortions(SalesDbContext sales) : IOrderSettlement
{
    private readonly SalesOrderSettlement orders = new(sales);

    public async Task<OrderProcessingState?> GetStateAsync(long orderId, CancellationToken ct)
    {
        var state = await orders.GetStateAsync(orderId, ct);
        if (state is null || state.NonFinalPortionCount == 0) return state;
        var confirmed = await SalesCloseChecks.ConfirmedPortionsAsync(sales, state.BusinessDayId, orderId, ct);
        var connection = sales.Database.GetDbConnection();
        var transaction = sales.Database.CurrentTransaction?.GetDbTransaction();
        await using var kitchen = new FulfillmentDbContext(new DbContextOptionsBuilder<FulfillmentDbContext>().UseSqlServer(connection).Options);
        if (transaction != null) await kitchen.Database.UseTransactionAsync(transaction, ct);
        var final = await TicketSettlement.FinalAsync(kitchen, confirmed, ct);
        var incidents = final.Where(p => p.AdjustmentIncidentId.HasValue).Select(p => p.AdjustmentIncidentId!.Value).ToArray();
        var count = final.Count - incidents.Length;
        if (incidents.Length > 0)
        {
            await using var billing = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(connection).Options);
            if (transaction != null) await billing.Database.UseTransactionAsync(transaction, ct);
            count += await BillingCloseChecks.AppliedIncidentsAsync(billing, state.BusinessDayId, orderId, incidents, ct);
        }
        return state with { NonFinalPortionCount = state.NonFinalPortionCount - count };
    }

    public Task<bool> PortionBelongsToOrderAsync(long orderId, long portionId, CancellationToken ct) =>
        orders.PortionBelongsToOrderAsync(orderId, portionId, ct);
}
