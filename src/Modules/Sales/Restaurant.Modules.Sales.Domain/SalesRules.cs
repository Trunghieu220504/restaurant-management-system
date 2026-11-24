namespace Restaurant.Modules.Sales.Domain;

public static class OrderTypes
{
    public const string DineIn = "TAI_BAN";
    public const string Takeaway = "MANG_DI";
    public static readonly IReadOnlyList<string> All = [DineIn, Takeaway];
}

public static class OrderStatus
{
    public const string Open = "MO";
    public const string Closed = "DONG";
    public const string Cancelled = "HUY";
    public static readonly IReadOnlyList<string> All = [Open, Closed, Cancelled];
}

public static class BatchSource
{
    public const string Guest = "KHACH";
    public const string Staff = "NHAN_VIEN";
    public static readonly IReadOnlyList<string> All = [Guest, Staff];
}

public static class BatchStatus
{
    public const string Pending = "CHO_XAC_NHAN";
    public const string Confirmed = "DA_XAC_NHAN";
    public const string Rejected = "DA_TU_CHOI";
    public const string Cancelled = "DA_HUY";
    public static readonly IReadOnlyList<string> All = [Pending, Confirmed, Rejected, Cancelled];
}

public static class PortionStatus
{
    public const string Pending = "CHO_XAC_NHAN";
    public const string Confirmed = "DA_XAC_NHAN";
    public const string Cancelled = "DA_HUY";
    public static readonly IReadOnlyList<string> All = [Pending, Confirmed, Cancelled];
}

public static class Stations
{
    public const string Kitchen = "BEP";
    public const string Bar = "BAR";
    public static readonly IReadOnlyList<string> All = [Kitchen, Bar];
}

public static class OptionGroupKinds
{
    public const string Topping = "TOPPING";
    public const string Option = "TUY_CHON";
    public static readonly IReadOnlyList<string> All = [Topping, Option];
}

/// <summary>Stable business error codes, <c>sales.&lt;subject&gt;.&lt;reason&gt;</c>.</summary>
public static class SalesErrors
{
    public const string InputInvalid = "sales.input.invalid";
    public const string OrderNotOpen = "sales.order.not_open";
    public const string OrderSessionRequired = "sales.order.session_required";
    public const string OrderSessionForbidden = "sales.order.session_forbidden";
    public const string OrderHasPendingBatches = "sales.order.has_pending_batches";
    public const string OrderHasActivePortions = "sales.order.has_active_portions";
    public const string OrderNotFound = "sales.order.not_found";
    public const string OrderSessionHasOpenOrder = "sales.order.session_has_open_order";
    public const string OrderChanged = "sales.order.changed";
    public const string BatchNotFound = "sales.batch.not_found";
    public const string BatchNotPending = "sales.batch.not_pending";
    public const string BatchEmpty = "sales.batch.empty";
    public const string BatchGuestNeedsSession = "sales.batch.guest_needs_session";
    public const string BatchLineInvalid = "sales.batch.line_invalid";
    public const string PricingInconsistent = "sales.pricing.inconsistent";
    public const string PricingChanged = "sales.pricing.changed";
    public const string PortionNotFound = "sales.portion.not_found";
    public const string PortionAlreadyCancelled = "sales.portion.already_cancelled";
}

/// <summary>An allow-list of status moves. Everything not listed is forbidden.</summary>
public sealed class StateMachine
{
    private readonly HashSet<(string From, string To)> allowed;

    public StateMachine(IReadOnlyCollection<string> states, params (string From, string To)[] moves)
    {
        States = states;
        allowed = [.. moves];
        foreach (var (from, to) in moves)
            if (!states.Contains(from) || !states.Contains(to))
                throw new ArgumentException($"Unknown state in move {from} -> {to}.");
    }

    public IReadOnlyCollection<string> States { get; }

    public bool CanMove(string from, string to) => allowed.Contains((from, to));

    public bool IsTerminal(string state) => !allowed.Any(move => move.From == state);
}

public static class SalesStateMachines
{
    /// <summary>An open order is closed (paid and finished) or cancelled; both are final.</summary>
    public static readonly StateMachine Order = new(OrderStatus.All,
        (OrderStatus.Open, OrderStatus.Closed),
        (OrderStatus.Open, OrderStatus.Cancelled));

    /// <summary>Only a pending batch can be decided. A confirmed batch is reduced portion by portion.</summary>
    public static readonly StateMachine Batch = new(BatchStatus.All,
        (BatchStatus.Pending, BatchStatus.Confirmed),
        (BatchStatus.Pending, BatchStatus.Rejected),
        (BatchStatus.Pending, BatchStatus.Cancelled));

    /// <summary>A portion is confirmed with its batch and can be cancelled before or after confirmation.</summary>
    public static readonly StateMachine Portion = new(PortionStatus.All,
        (PortionStatus.Pending, PortionStatus.Confirmed),
        (PortionStatus.Pending, PortionStatus.Cancelled),
        (PortionStatus.Confirmed, PortionStatus.Cancelled));
}

public sealed class SalesResult
{
    private SalesResult(string? error) => Error = error;

    public string? Error { get; }
    public bool Succeeded => Error is null;

    public static SalesResult Ok() => new(null);
    public static SalesResult Fail(string error) => new(error);
}

public sealed class SalesResult<T> where T : class
{
    private SalesResult(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public string? Error { get; }
    public bool Succeeded => Error is null;

    public static SalesResult<T> Ok(T value) => new(value, null);
    public static SalesResult<T> Fail(string error) => new(null, error);
}
