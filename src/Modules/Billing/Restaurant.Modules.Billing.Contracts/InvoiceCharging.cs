namespace Restaurant.Modules.Billing.Contracts;

/// <summary>
/// Sales tells Billing which portions just became confirmed, so Billing records the
/// obligation (one invoice per order, one invoice line per portion).
/// It is a synchronous critical write: the batch confirmation and this call must commit or
/// roll back together, in one SQL transaction owned by the caller's unit of work. The
/// contract has no transaction parameter and Billing never commits or opens its own
/// transaction here. If it returns an error, the caller must roll back the confirmation.
/// Repeating the same idempotency key (a Guid) or a portion already charged changes nothing
/// and returns <see cref="ChargeBatchResult.Replayed"/> = true.
/// </summary>
public interface IInvoiceCharging
{
    Task<ChargeBatchResult> ChargeConfirmedBatchAsync(ChargeBatchRequest request, CancellationToken ct);
}

/// <param name="BusinessDayId">Day of the order; Sales gates the day before calling, Billing does not re-gate it.</param>
/// <param name="Actor">Identity user id (Guid) of the confirming staff member.</param>
public sealed record ChargeBatchRequest(
    long OrderId,
    long BatchId,
    Guid BusinessDayId,
    Guid IdempotencyKey,
    Guid Actor,
    IReadOnlyList<ChargeLine> Lines);

/// <param name="PortionId">Sales order detail id; unique per invoice line.</param>
/// <param name="Description">Item name and option text snapshot printed on the invoice.</param>
/// <param name="Amount">Integer VND, at least 0 (the unit price from the confirmed pricing).</param>
public sealed record ChargeLine(long PortionId, string Description, long Amount);

/// <summary>Exactly one of <see cref="InvoiceId"/> and <see cref="ErrorCode"/> is set.</summary>
public sealed record ChargeBatchResult(long? InvoiceId, string? ErrorCode, bool Replayed = false)
{
    public bool IsCharged => InvoiceId is not null;
}

public static class BillingErrorCodes
{
    public const string InvoiceClosed = "billing.invoice.closed";
    public const string LineInvalid = "billing.line.invalid";
    public const string IdempotencyConflict = "billing.idempotency.conflict";
}
