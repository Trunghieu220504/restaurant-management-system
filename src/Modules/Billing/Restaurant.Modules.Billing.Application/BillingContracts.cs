using System.Data.Common;

namespace Restaurant.Modules.Billing.Application;

public interface IBillingDayGate
{
    Task<bool> RequireOpenAsync(DbTransaction transaction, Guid dayId, CancellationToken ct);
}

public sealed record CollectPayment(Guid BusinessDayId, string Method, long Amount, long? Tendered, string? BankReference);
public sealed record TransferDecision(Guid BusinessDayId);
public sealed record WaiveBalance(Guid BusinessDayId, long Amount, string Reason);
public sealed record BillLine(long PortionId, string Description, decimal Amount);
public sealed record BillPayment(long Id, string Method, decimal Amount, decimal? Tendered, decimal? Change, string? BankReference, string Status,
    Guid BusinessDayId, Guid CollectedBy, DateTime CreatedAt, DateTime? ConfirmedAt);
public sealed record BillWaiver(long Id, decimal Amount, string Reason, Guid BusinessDayId, Guid ApprovedBy, DateTime CreatedAt);
public sealed record BillDocument(long Id, string Number, string Kind, DateTime CreatedAt);
public sealed record PrintableBill(string Number, string Kind, DateTime Issued, Guid Actor, string? StoreName,
    string? StoreAddress, long InvoiceId, long OrderId, string InvoiceNumber, Guid BusinessDayId,
    IReadOnlyList<BillLine> Lines, decimal Obligation, decimal NetCollected, decimal Due, IReadOnlyList<BillWaiver> Waivers, BillPayment? Payment)
{
    public RefundView? Refund { get; init; }
}
public sealed record InvoiceView(long Id, long OrderId, string Number, Guid BusinessDayId, decimal Obligation, decimal NetCollected,
    decimal Due, decimal Reserved, decimal Available, IReadOnlyList<BillLine> Lines, IReadOnlyList<BillPayment> Payments,
    IReadOnlyList<BillWaiver> Waivers, IReadOnlyList<BillDocument> Documents)
{
    public string PaymentStatus => Due == 0 ? "DA_THU_DU" : NetCollected > 0 ? "THU_MOT_PHAN" : "CHUA_THU";
}

public sealed record CloseOrderRequest(Guid BusinessDayId);
public interface ICloseOrder
{
    Task<MoneyResult> CloseAsync(long orderId, Guid day, CancellationToken ct);
}
public sealed record MoneyResult(long? ResourceId, string? Error = null, bool Replayed = false);

public interface IBillingLedger
{
    Task<IReadOnlyList<RefundView>> RefundsAsync(long invoiceId, CancellationToken ct);
    Task<MoneyResult> RequestRefundAsync(long paymentId, RefundRequest input, Guid actor, Guid key, CancellationToken ct);
    Task<MoneyResult> ChangeRefundAsync(long refundId, RefundAction action, RefundDecision input, Guid actor, Guid key, CancellationToken ct);
    Task<MoneyResult> AdjustAsync(long invoiceId, ObligationChange input, Guid actor, Guid key, CancellationToken ct);
    Task<InvoiceView?> FindAsync(long invoiceId, CancellationToken ct);
    Task<long?> ForOrderAsync(long orderId, CancellationToken ct);
    Task<string?> DocumentAsync(long documentId, CancellationToken ct);
    Task<MoneyResult> CollectAsync(long invoiceId, CollectPayment request, Guid actor, Guid key, CancellationToken ct);
    Task<MoneyResult> DecideAsync(long invoiceId, long paymentId, TransferDecision request, bool confirm, Guid actor, Guid key, CancellationToken ct);
    Task<MoneyResult> WaiveAsync(long invoiceId, WaiveBalance request, Guid actor, Guid key, CancellationToken ct);
}
