namespace Restaurant.Modules.SalesDay.Contracts;

/// <summary>
/// Gate every write that belongs to a business day must pass (ADR-01). Call it inside the
/// unit of work of the write, before changing anything: it takes the day lock in Shared mode
/// for the current transaction, re-reads the day status after the lock, and only lets a day in
/// <see cref="SalesDayStatuses.Open"/> through. The lock is released when that transaction ends;
/// the contract itself never commits. Lock order stays day, order/session, invoice, payment, stock.
/// </summary>
public interface ISalesDayGate
{
    Task<SalesDayGateResult> RequireOpenAsync(Guid businessDayId, CancellationToken ct);
}

/// <summary>Exactly one of <see cref="Day"/> and <see cref="ErrorCode"/> is set.</summary>
public sealed record SalesDayGateResult(OpenBusinessDay? Day, string? ErrorCode)
{
    public bool IsOpen => Day is not null;
}

public sealed record OpenBusinessDay(Guid Id, DateOnly BusinessDate);

public static class SalesDayStatuses
{
    public const string Open = "DANG_BAN";
    public const string PendingClose = "CHO_CHOT";
    public const string Closed = "DA_CHOT";
}

public static class SalesDayErrorCodes
{
    public const string DayNotFound = "salesday.day.not_found";
    public const string DayNotOpen = "salesday.day.not_open";
}
