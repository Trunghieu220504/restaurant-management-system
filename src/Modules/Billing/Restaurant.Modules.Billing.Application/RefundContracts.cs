namespace Restaurant.Modules.Billing.Application;

public sealed record RefundRequest(Guid BusinessDayId, long Amount, string Reason);
public sealed record RefundDecision(Guid BusinessDayId, string? Reference = null);
public sealed record ObligationChange(Guid BusinessDayId, long Amount, string Reason);
public sealed record RefundView(long Id, long PaymentId, Guid BusinessDayId, decimal Amount, string Method, string Status,
    string Reason, Guid RequestedBy, Guid? ApprovedBy, Guid? PaidBy, string? Reference, DateTime CreatedAt, DateTime? PaidAt);
public sealed record PayoutDecision(Guid BusinessDayId, string Outcome, string? Reference = null);
public enum RefundAction { Approve, Reject, BeginPayout, Success, Unknown, ConfirmPaid, ConfirmUnpaid }
