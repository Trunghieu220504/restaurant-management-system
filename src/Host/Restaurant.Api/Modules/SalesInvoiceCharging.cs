using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Billing.Contracts;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

internal sealed class SalesInvoiceCharging(SalesDbContext sales, IConfiguration configuration) : IInvoiceCharging
{
    public async Task<ChargeBatchResult> ChargeConfirmedBatchAsync(ChargeBatchRequest request, CancellationToken ct)
    {
        var transaction = sales.Database.CurrentTransaction ?? throw new InvalidOperationException("Begin the Sales transaction before charging.");
        var options = new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(sales.Database.GetDbConnection()).Options;
        await using var billing = new BillingDbContext(options);
        await billing.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        return await new InvoiceCharging(billing, configuration).ChargeConfirmedBatchAsync(request, ct);
    }
}
