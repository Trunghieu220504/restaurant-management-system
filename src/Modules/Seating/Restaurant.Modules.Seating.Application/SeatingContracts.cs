namespace Restaurant.Modules.Seating.Application;

/// <summary>Stable error codes of the Seating API, formatted as seating.&lt;subject&gt;.&lt;reason&gt;.</summary>
public static class SeatingErrors
{
    public const string InputInvalid = "seating.input.invalid";
    public const string AreaNotFound = "seating.area.not_found";
    public const string AreaCodeConflict = "seating.area.code_conflict";
    public const string AreaInactive = "seating.area.inactive";
    public const string AreaHasActiveTables = "seating.area.has_active_tables";
    public const string TableNotFound = "seating.table.not_found";
    public const string TableCodeConflict = "seating.table.code_conflict";
    public const string TableInactive = "seating.table.inactive";
    public const string TableOccupied = "seating.table.occupied";
    public const string TableInUse = "seating.table.in_use";
    public const string SessionNotFound = "seating.session.not_found";
    public const string SessionClosed = "seating.session.closed";
    public const string SessionHasOpenOrders = "seating.session.open_orders";
    public const string TransferSameTable = "seating.transfer.same_table";
    public const string ConcurrencyConflict = "seating.concurrency.conflict";
    public const string IdempotencyRequired = "seating.idempotency.required";
    public const string IdempotencyConflict = "seating.idempotency.conflict";

    /// <summary>Owned by SalesDay (see ISalesDayGate); reused so every module reports a closed day the same way.</summary>
    public const string DayNotOpen = "salesday.day.not_open";
}

public sealed record SeatingResult<T>(T? Value, string? Error, bool Replayed = false)
{
    public static SeatingResult<T> Ok(T value, bool replayed = false) => new(value, null, replayed);
    public static SeatingResult<T> Fail(string error) => new(default, error);
}

public sealed record AreaDto(long Id, string Code, string Name, bool Active, string Version);
public sealed record CreateAreaRequest(string Code, string Name);
public sealed record UpdateAreaRequest(string Name, string Version);

public sealed record TableDto(long Id, long AreaId, string Code, string Name, bool Active, string PublicQrCode, string Version);
public sealed record CreateTableRequest(long AreaId, string Code, string Name);
public sealed record UpdateTableRequest(long AreaId, string Name, string Version);

/// <summary>Row version of the entity being archived, as returned by the API.</summary>
public sealed record ArchiveRequest(string Version);

public sealed record BoardSessionDto(long Id, DateTimeOffset OpenedAt);
public sealed record BoardTableDto(long Id, string Code, string Name, string Status, BoardSessionDto? Session);
public sealed record BoardAreaDto(long Id, string Code, string Name, IReadOnlyList<BoardTableDto> Tables);

public sealed record SessionDto(
    long Id, long TableId, Guid BusinessDayId, DateTimeOffset OpenedAt, Guid OpenedBy,
    DateTimeOffset? ClosedAt, Guid? ClosedBy, string Version);

public sealed record TransferRequest(long ToTableId, string Reason);

public static class BoardStatuses
{
    public const string Free = "TRONG";
    public const string InUse = "DANG_DUNG";
}

/// <summary>Areas, tables and the table board.</summary>
public interface ISeatingCatalog
{
    Task<IReadOnlyList<AreaDto>> ListAreasAsync(CancellationToken ct);
    Task<SeatingResult<AreaDto>> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct);
    Task<SeatingResult<AreaDto>> UpdateAreaAsync(long id, UpdateAreaRequest request, CancellationToken ct);
    Task<SeatingResult<AreaDto>> ArchiveAreaAsync(long id, ArchiveRequest request, CancellationToken ct);

    Task<IReadOnlyList<TableDto>> ListTablesAsync(CancellationToken ct);
    Task<SeatingResult<TableDto>> CreateTableAsync(CreateTableRequest request, CancellationToken ct);
    Task<SeatingResult<TableDto>> UpdateTableAsync(long id, UpdateTableRequest request, CancellationToken ct);
    Task<SeatingResult<TableDto>> ArchiveTableAsync(long id, ArchiveRequest request, CancellationToken ct);

    Task<IReadOnlyList<BoardAreaDto>> BoardAsync(CancellationToken ct);
}

/// <summary>Open, transfer and close a table session. Every command is idempotent per (actor, key).</summary>
public interface ISeatingSessions
{
    Task<SeatingResult<SessionDto>> OpenAsync(long tableId, Guid actor, Guid key, CancellationToken ct);
    Task<SeatingResult<SessionDto>> TransferAsync(long sessionId, TransferRequest request, Guid actor, Guid key, CancellationToken ct);
    Task<SeatingResult<SessionDto>> CloseAsync(long sessionId, Guid actor, Guid key, CancellationToken ct);
}

/// <summary>
/// Port to the business day owned by SalesDay. The host supplies the adapter, so Seating never references
/// another module. Replace it with ISalesDayGate once that contract is implemented.
/// </summary>
public interface IBusinessDayDirectory
{
    /// <summary>The id of the day that is currently open for sales, or null when none is open.</summary>
    Task<Guid?> CurrentOpenDayAsync(CancellationToken ct);

    /// <summary>True only when the given day is the active day and is still open for sales.</summary>
    Task<bool> IsOpenAsync(Guid dayId, CancellationToken ct);
}

/// <summary>
/// Port to Sales: a session may only close when none of its orders is still open. Sales has no orders yet,
/// so the default answer is "none"; HIE-3.1 must replace it with an adapter over a Sales contract.
/// </summary>
public interface ISessionClosureGuard
{
    Task<bool> HasOpenOrdersAsync(long sessionId, CancellationToken ct);
}
