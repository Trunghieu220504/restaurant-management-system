namespace Restaurant.Modules.Sales.Contracts;

/// <summary>
/// Billing asks Sales whether an order is finished from the ordering side, and whether a
/// portion belongs to it. Read-only; Billing decides alone about money (receivable, refund,
/// payment), Sales decides alone about portions and batches. An order may be closed only
/// when this reports settled and Billing's own money checks also pass.
/// </summary>
public interface IOrderSettlement
{
    /// <summary>Null when the order does not exist.</summary>
    Task<OrderProcessingState?> GetStateAsync(long orderId, CancellationToken ct);

    /// <summary>True only when the portion exists and belongs to the order.</summary>
    Task<bool> PortionBelongsToOrderAsync(long orderId, long portionId, CancellationToken ct);
}

/// <param name="Status">One of <see cref="OrderStatuses"/>.</param>
/// <param name="PendingBatchCount">Batches still waiting for confirmation.</param>
/// <param name="NonFinalPortionCount">Portions not yet in a final state (served or cancelled).</param>
public sealed record OrderProcessingState(
    long OrderId,
    Guid BusinessDayId,
    string Status,
    int PendingBatchCount,
    int NonFinalPortionCount)
{
    /// <summary>No pending batch and every portion final. Zero portions counts as settled.</summary>
    public bool IsSettled => PendingBatchCount == 0 && NonFinalPortionCount == 0;
}

public static class OrderStatuses
{
    public const string Open = "MO";
    public const string Closed = "DONG";
    public const string Cancelled = "HUY";
}
