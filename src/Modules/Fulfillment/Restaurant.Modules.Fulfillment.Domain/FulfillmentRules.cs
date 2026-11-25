namespace Restaurant.Modules.Fulfillment.Domain;

/// <summary>Ticket (PhieuCheBien) states. The transitions between them belong to HIE-4.1; this task only fixes the vocabulary.</summary>
public static class TicketStatus
{
    public const string New = "MOI";
    public const string InProgress = "DANG_LAM";
    public const string Ready = "SAN_SANG";
    public const string Served = "DA_GIAO";
    public const string Cancelled = "DA_HUY";
    public const string Failed = "LOI";
    public static readonly IReadOnlyList<string> All = [New, InProgress, Ready, Served, Cancelled, Failed];
}

public static class IncidentStatus
{
    public const string Pending = "CHO_DUYET";
    public const string Approved = "DA_DUYET";
    public const string Rejected = "TU_CHOI";
    public static readonly IReadOnlyList<string> All = [Pending, Approved, Rejected];
}

/// <summary>What an approved incident decided: remake the portion, or cancel it with or without a money effect.</summary>
public static class IncidentDecision
{
    public const string Remake = "LAM_LAI";
    public const string CancelBookkeepingOnly = "HUY_VAN_THU";
    public const string CancelWithObligationAdjustment = "HUY_GIAM_NGHIA_VU";
    public static readonly IReadOnlyList<string> All = [Remake, CancelBookkeepingOnly, CancelWithObligationAdjustment];
}

/// <summary>Same station codes as Sales portions (<c>TramChot</c>), repeated here because modules do not share domain types.</summary>
public static class Stations
{
    public const string Kitchen = "BEP";
    public const string Bar = "BAR";
    public static readonly IReadOnlyList<string> All = [Kitchen, Bar];
}

public static class FulfillmentErrors
{
    public const string InputInvalid = "fulfillment.input.invalid";
    public const string TicketNotFound = "fulfillment.ticket.not_found";
    public const string StationForbidden = "fulfillment.ticket.station_forbidden";
    public const string InvalidTransition = "fulfillment.ticket.invalid_transition";
    public const string AlreadyProcessed = "fulfillment.ticket.already_processed";
    public const string ConcurrencyConflict = "fulfillment.ticket.concurrency_conflict";
    public const string IdempotencyRequired = "fulfillment.idempotency.required";
    public const string IdempotencyConflict = "fulfillment.idempotency.conflict";
    public const string IncidentNotFound = "fulfillment.incident.not_found";
    public const string IncidentPending = "fulfillment.incident.pending";
    public const string IncidentAlreadyDecided = "fulfillment.incident.already_decided";
}

public sealed class FulfillmentResult<T> where T : class
{
    private FulfillmentResult(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public string? Error { get; }
    public bool Succeeded => Error is null;

    public static FulfillmentResult<T> Ok(T value) => new(value, null);
    public static FulfillmentResult<T> Fail(string error) => new(null, error);
}
