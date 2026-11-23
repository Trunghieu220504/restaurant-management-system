using Restaurant.Modules.Billing.Contracts;

namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>
/// In-memory <see cref="IInvoiceCharging"/> with the idempotency the contract promises: one invoice per order, one line per
/// portion, and a repeated key or an already charged portion changes nothing and reports <c>Replayed</c>.
/// Not used by create, add or cancel batch; it is here for the confirm step that comes next.
/// </summary>
public sealed class FakeInvoiceCharging(CallLog? log = null) : IInvoiceCharging
{
    private readonly Dictionary<long, long> invoiceByOrder = [];
    private readonly Dictionary<Guid, long> invoiceByKey = [];
    private readonly Dictionary<long, long> chargedPortions = [];
    private readonly HashSet<long> closedOrders = [];
    private long nextInvoiceId = 1;

    public int CallCount { get; private set; }

    /// <summary>Every portion charged so far with its amount.</summary>
    public IReadOnlyDictionary<long, long> ChargedPortions => chargedPortions;

    public int InvoiceCount => invoiceByOrder.Count;

    public void CloseInvoiceOf(long orderId) => closedOrders.Add(orderId);

    public Task<ChargeBatchResult> ChargeConfirmedBatchAsync(ChargeBatchRequest request, CancellationToken ct)
    {
        CallCount++;
        log?.Add($"billing.charge:{request.OrderId}:{request.BatchId}");
        if (invoiceByKey.TryGetValue(request.IdempotencyKey, out var earlier))
            return Task.FromResult(new ChargeBatchResult(earlier, null, Replayed: true));
        if (request.Lines.Count == 0 ||
            request.Lines.Any(x => x.PortionId <= 0 || x.Amount < 0 || string.IsNullOrWhiteSpace(x.Description)) ||
            request.Lines.Select(x => x.PortionId).Distinct().Count() != request.Lines.Count)
            return Task.FromResult(new ChargeBatchResult(null, BillingErrorCodes.LineInvalid));
        if (closedOrders.Contains(request.OrderId))
            return Task.FromResult(new ChargeBatchResult(null, BillingErrorCodes.InvoiceClosed));

        if (!invoiceByOrder.TryGetValue(request.OrderId, out var invoiceId))
            invoiceByOrder[request.OrderId] = invoiceId = nextInvoiceId++;
        var fresh = request.Lines.Where(x => !chargedPortions.ContainsKey(x.PortionId)).ToList();
        foreach (var line in fresh) chargedPortions[line.PortionId] = line.Amount;
        invoiceByKey[request.IdempotencyKey] = invoiceId;
        return Task.FromResult(new ChargeBatchResult(invoiceId, null, Replayed: fresh.Count == 0));
    }
}
