namespace Restaurant.Modules.Sales.Application;

/// <param name="Type">TAI_BAN or MANG_DI.</param>
/// <param name="SessionId">Required for dine-in (the business day and table label come from the session), forbidden otherwise.</param>
/// <param name="BusinessDayId">Required for takeaway (no session carries the day), forbidden for dine-in.</param>
public sealed record CreateOrderCommand(string? Type, long? SessionId, Guid? BusinessDayId, string? CustomerName, Guid Actor);

/// <param name="Source">KHACH (guest, needs <paramref name="GuestGrantId"/>) or NHAN_VIEN (staff, needs <paramref name="StaffId"/>).</param>
/// <param name="RequestKey">Idempotency key: sending the same key again returns the batch already created.</param>
public sealed record AddBatchCommand(
    long OrderId, string? Source, Guid? GuestGrantId, Guid? StaffId, Guid RequestKey, IReadOnlyList<BatchLine>? Lines);

/// <summary>One portion asked for. A line is one portion; two plates of the same dish are two lines.</summary>
public sealed record BatchLine(Guid MenuItemId, Guid VariantId, IReadOnlyList<Guid>? OptionIds, string? Note);

public sealed record CancelBatchCommand(long OrderId, int BatchNumber);
public sealed record ConfirmBatchCommand(long OrderId, int BatchNumber, Guid Actor);
public sealed record RejectBatchCommand(long OrderId, int BatchNumber, Guid Actor, string? Reason);


public sealed record OrderView(
    long Id, string Code, string Type, Guid BusinessDayId, long? SessionId, string? TableLabel, string? CustomerName, string Status);

public sealed record BatchView(
    long OrderId, long BatchId, int Number, string Status, IReadOnlyList<PortionView> Portions, long Total);

/// <param name="UnitPrice">Variant price plus the surcharge of every chosen option, as priced when the batch was added.</param>
public sealed record PortionView(
    long Id, string MenuItemName, string VariantName, string Station, string? Note, long UnitPrice,
    IReadOnlyList<string> OptionNames, string Status);

/// <param name="Code">A Catalog error code for that line.</param>
public sealed record LineError(int LineNo, string Code);

/// <summary>
/// Either a value or an error code. <see cref="LineErrors"/> is filled when the menu rejected some lines
/// (error <c>sales.batch.line_invalid</c>), so the client can point at the exact portion.
/// </summary>
public sealed class ServiceResult<T> where T : class
{
    private ServiceResult(T? value, string? error, IReadOnlyList<LineError>? lineErrors, bool replayed)
    {
        Value = value;
        Error = error;
        LineErrors = lineErrors ?? [];
        Replayed = replayed;
    }

    public T? Value { get; }
    public string? Error { get; }
    public IReadOnlyList<LineError> LineErrors { get; }

    /// <summary>True when the request key had been used before and the earlier result is returned unchanged.</summary>
    public bool Replayed { get; }

    public bool Succeeded => Error is null;

    public static ServiceResult<T> Ok(T value, bool replayed = false) => new(value, null, null, replayed);
    public static ServiceResult<T> Fail(string error, IReadOnlyList<LineError>? lineErrors = null) =>
        new(null, error, lineErrors, false);
}
