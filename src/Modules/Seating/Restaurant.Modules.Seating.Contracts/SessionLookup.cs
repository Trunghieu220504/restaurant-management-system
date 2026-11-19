namespace Restaurant.Modules.Seating.Contracts;

/// <summary>
/// Sales asks Seating whether a table session exists and is open, and gets the data Sales
/// snapshots on the order. Read-only. A session is bound to a business day and keeps its
/// identity when the table changes, so Sales must store the session id, not the table id.
/// </summary>
public interface ISessionLookup
{
    Task<SessionCheckResult> CheckOpenAsync(long sessionId, CancellationToken ct);
}

/// <summary>Exactly one of <see cref="Session"/> and <see cref="ErrorCode"/> is set.</summary>
public sealed record SessionCheckResult(SessionInfo? Session, string? ErrorCode)
{
    public bool IsOpen => Session is not null;
}

/// <param name="TableLabel">Current table label, to be copied as a snapshot onto the order.</param>
public sealed record SessionInfo(long SessionId, Guid BusinessDayId, long TableId, string TableLabel, DateTimeOffset OpenedAt);

public static class SeatingErrorCodes
{
    public const string SessionNotFound = "seating.session.not_found";
    public const string SessionClosed = "seating.session.closed";
}
