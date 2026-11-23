using Restaurant.Modules.Seating.Contracts;

namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>In-memory <see cref="ISessionLookup"/>: sessions are added by the test and can be closed.</summary>
public sealed class FakeSessionLookup(CallLog? log = null) : ISessionLookup
{
    private readonly Dictionary<long, (SessionInfo Info, bool Closed)> sessions = [];

    public FakeSessionLookup AddOpen(long sessionId, Guid businessDayId, string tableLabel, long tableId = 1)
    {
        sessions[sessionId] = (new SessionInfo(sessionId, businessDayId, tableId, tableLabel, DateTimeOffset.UtcNow), false);
        return this;
    }

    public void Close(long sessionId) => sessions[sessionId] = (sessions[sessionId].Info, true);

    public Task<SessionCheckResult> CheckOpenAsync(long sessionId, CancellationToken ct)
    {
        log?.Add($"session.check:{sessionId}");
        if (!sessions.TryGetValue(sessionId, out var found))
            return Task.FromResult(new SessionCheckResult(null, SeatingErrorCodes.SessionNotFound));
        return Task.FromResult(found.Closed
            ? new SessionCheckResult(null, SeatingErrorCodes.SessionClosed)
            : new SessionCheckResult(found.Info, null));
    }
}
